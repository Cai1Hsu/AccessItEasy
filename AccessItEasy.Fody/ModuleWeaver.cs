using System.Runtime.CompilerServices;
using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;

namespace AccessItEasy.Fody;

public partial class ModuleWeaver : BaseModuleWeaver
{
    private static readonly string PrivateAccessorAttributeName = typeof(PrivateAccessorAttribute).FullName!;
    private static readonly string PrivateAccessorTypeAttributeName = typeof(PrivateAccessorTypeAttribute).FullName!;

    private MethodReference compilerGeneratedConstructor = null!;

    private MethodReference CompilerGeneratedConstructor => compilerGeneratedConstructor;

    private HashSet<AssemblyDefinition> accessedAssemblies = new();
    private HashSet<TypeReference> accessedTypes = new();

    public override void Execute()
    {
        compilerGeneratedConstructor = FindCompilerGeneratedAttributeConstructor();

        foreach (var type in ModuleDefinition.Types)
        {
            ProcessType(type);
        }

        foreach (var typeRef in accessedTypes)
        {
            var typeDef = typeRef.Resolve();
            var assemblyDefinition = typeDef.Module.Assembly;

            if (accessedAssemblies.Add(assemblyDefinition))
                WriteDebug($"Registered access to assembly {assemblyDefinition.Name.Name}");
        }

        generateIgnoreAccessChecksToAttributes();
    }

    private const string IgnoresAccessChecksToAttributeName = "System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute";

    private void generateIgnoreAccessChecksToAttributes()
    {
        if (accessedAssemblies.Count == 0)
        {
            WriteDebug("No external assemblies accessed, skipping IgnoresAccessChecksTo generation");
            return;
        }

        // Try to use the IgnoresAccessChecksToAttribute defined in this assembly
        // if not find, define a new one
        TypeReference ignoresAccessChecksToTypeRef = GetOrGenerateIgnoreAccessChecksToTypeRef();
        TypeDefinition ignoresAccessChecksToTypeDef = ignoresAccessChecksToTypeRef.Resolve();

        if (ignoresAccessChecksToTypeDef is null)
        {
            WriteError($"Could not resolve type {IgnoresAccessChecksToAttributeName}");
            return;
        }

        MethodDefinition? ignoresAccessChecksToCtorDef = ignoresAccessChecksToTypeDef
            .GetConstructors()
            .FirstOrDefault(c => c.Parameters.Count == 1 && c.Parameters[0].ParameterType.FullName == ModuleDefinition.TypeSystem.String.FullName);

        if (ignoresAccessChecksToCtorDef is null)
        {
            WriteError($"Could not find constructor for {IgnoresAccessChecksToAttributeName}");
            return;
        }

        // Import the constructor reference to ensure it's properly linked
        MethodReference ignoresAccessChecksToCtorRef = ModuleDefinition.ImportReference(ignoresAccessChecksToCtorDef);

        foreach (var assembly in accessedAssemblies)
        {
            string assemblyName = GetAssemblyName(assembly);

            WriteInfo($"Adding IgnoresAccessChecksTo attribute for assembly {assemblyName}");

            var attribute = new CustomAttribute(ignoresAccessChecksToCtorRef);
            attribute.ConstructorArguments.Add(new CustomAttributeArgument(
                ModuleDefinition.TypeSystem.String, assemblyName));

            ModuleDefinition.Assembly.CustomAttributes.Add(attribute);
        }

        WriteInfo($"Total IgnoresAccessChecksTo attributes added: {accessedAssemblies.Count}");
    }

    private static string GetAssemblyName(AssemblyDefinition assembly)
    {
        // assembly.Name.Name doesn't work so we parse FullName

        string fullName = assembly.Name.FullName;

        int commaIndex = fullName.IndexOf(',');
        if (commaIndex >= 0)
        {
            return fullName.Substring(0, commaIndex).Trim();
        }

        return fullName;
    }

    private void ProcessType(TypeDefinition type)
    {
        // Process nested types
        foreach (var nestedType in type.NestedTypes)
        {
            ProcessType(nestedType);
        }

        // Process methods
        foreach (var method in type.Methods)
        {
            ProcessMethod(method);
        }
    }

    private void ProcessMethod(MethodDefinition method)
    {
        var attribute = GetPrivateAccessorAttribute(method);
        if (attribute == null)
            return;

        if (!method.IsStatic)
        {
            WriteError($"PrivateAccessor method {method.FullName} must be static");
            return;
        }

        var kind = GetAccessorKind(attribute);
        var memberName = GetMemberName(attribute);

        WriteDebug($"Processing method {method.FullName} with kind {kind} and member name {memberName}");

        switch (kind)
        {
            case PrivateAccessorKind.Field:
                WeaveFieldAccessor(method, memberName, isStatic: false);
                break;

            case PrivateAccessorKind.StaticField:
                WeaveFieldAccessor(method, memberName, isStatic: true);
                break;

            case PrivateAccessorKind.Method:
                WeaveMethodAccessor(method, memberName, isStatic: false);
                break;

            case PrivateAccessorKind.StaticMethod:
                WeaveMethodAccessor(method, memberName, isStatic: true);
                break;

            case PrivateAccessorKind.Constructor:
                WeaveConstructorAccessor(method, memberName);
                break;
        }

        // Remove the attribute after processing so that the final assembly doesn't rely on AccessItEasy
        method.CustomAttributes.Remove(attribute);
        
        // Remove PrivateAccessorTypeAttribute from all parameters
        foreach (var parameter in method.Parameters)
        {
            var typeAttr = parameter.CustomAttributes.FirstOrDefault(
                a => a.AttributeType.FullName == PrivateAccessorTypeAttributeName);
            if (typeAttr != null)
            {
                parameter.CustomAttributes.Remove(typeAttr);
            }
        }
        
        // Remove PrivateAccessorTypeAttribute from return type
        var returnTypeAttr = method.MethodReturnType.CustomAttributes.FirstOrDefault(
            a => a.AttributeType.FullName == PrivateAccessorTypeAttributeName);
        if (returnTypeAttr != null)
        {
            method.MethodReturnType.CustomAttributes.Remove(returnTypeAttr);
        }
        
        method.CustomAttributes.Add(new CustomAttribute(CompilerGeneratedConstructor));
    }

    private CustomAttribute? GetPrivateAccessorAttribute(MethodDefinition method)
    {
        return method.CustomAttributes.FirstOrDefault(
            a => a.AttributeType.FullName == PrivateAccessorAttributeName);
    }

    private static PrivateAccessorKind GetAccessorKind(CustomAttribute attribute)
    {
        return (PrivateAccessorKind)(int)attribute.ConstructorArguments[0].Value;
    }

    private static string? GetMemberName(CustomAttribute attribute)
    {
        var nameProperty = attribute.Properties.FirstOrDefault(p => p.Name == "Name");

        if (nameProperty.Argument.Value is string name)
            return name;

        return null;
    }

    /// <summary>
    /// Resolves the actual target type from a parameter, considering PrivateAccessorTypeAttribute.
    /// </summary>
    private TypeReference ResolveTargetType(ParameterDefinition parameter)
    {
        var typeAttr = parameter.CustomAttributes.FirstOrDefault(
            a => a.AttributeType.FullName == PrivateAccessorTypeAttributeName);

        if (typeAttr != null)
        {
            var typeName = (string)typeAttr.ConstructorArguments[0].Value;
            var resolvedType = ResolveTypeFromName(typeName);
            if (resolvedType != null)
            {
                return ModuleDefinition.ImportReference(resolvedType);
            }
            WriteWarning($"Could not resolve type '{typeName}' specified in PrivateAccessorTypeAttribute");
        }

        return parameter.ParameterType;
    }

    /// <summary>
    /// Resolves the actual target type from return type, considering PrivateAccessorTypeAttribute.
    /// </summary>
    private TypeReference ResolveReturnType(MethodDefinition method)
    {
        var typeAttr = method.MethodReturnType.CustomAttributes.FirstOrDefault(
            a => a.AttributeType.FullName == PrivateAccessorTypeAttributeName);

        if (typeAttr != null)
        {
            var typeName = (string)typeAttr.ConstructorArguments[0].Value;
            var resolvedType = ResolveTypeFromName(typeName);
            if (resolvedType != null)
            {
                return ModuleDefinition.ImportReference(resolvedType);
            }
            WriteWarning($"Could not resolve type '{typeName}' specified in PrivateAccessorTypeAttribute");
        }

        return method.ReturnType;
    }

    /// <summary>
    /// Resolves a type from a type name string, supporting generic types.
    /// Supports formats like:
    /// - "System.String" (simple type)
    /// - "System.Collections.Generic.List`1[System.Int32]" (generic type)
    /// - "System.Collections.Generic.Dictionary`2[System.String,System.Int32]" (multi-arg generic)
    /// </summary>
    private TypeReference? ResolveTypeFromName(string fullTypeName)
    {
        // Check if this is a generic instantiation (contains '[')
        int bracketIndex = fullTypeName.IndexOf('[');
        if (bracketIndex > 0)
        {
            return ResolveGenericTypeFromName(fullTypeName, bracketIndex);
        }

        // Simple type - find the TypeDefinition and import it
        var typeDef = FindTypeDefinitionInAssemblies(fullTypeName);
        return typeDef != null ? ModuleDefinition.ImportReference(typeDef) : null;
    }

    /// <summary>
    /// Resolves a generic type from a name like "System.Collections.Generic.List`1[System.Int32]"
    /// </summary>
    private TypeReference? ResolveGenericTypeFromName(string fullTypeName, int bracketIndex)
    {
        // Extract the generic type definition name (e.g., "System.Collections.Generic.List`1")
        string genericDefName = fullTypeName.Substring(0, bracketIndex);
        
        // Find the generic type definition
        var genericTypeDef = FindTypeDefinitionInAssemblies(genericDefName);
        if (genericTypeDef == null)
        {
            WriteWarning($"Could not resolve generic type definition '{genericDefName}'");
            return null;
        }

        // Parse the generic arguments from the brackets
        string argsSection = fullTypeName.Substring(bracketIndex + 1, fullTypeName.Length - bracketIndex - 2);
        var argNames = ParseGenericArguments(argsSection);

        // Resolve each generic argument
        var genericArgs = new List<TypeReference>();
        foreach (var argName in argNames)
        {
            var argType = ResolveTypeFromName(argName);
            if (argType == null)
            {
                WriteWarning($"Could not resolve generic argument type '{argName}'");
                return null;
            }
            // Import the argument type to ensure it's properly referenced
            genericArgs.Add(ModuleDefinition.ImportReference(argType));
        }

        // Create the generic instance type
        var genericInstance = new GenericInstanceType(ModuleDefinition.ImportReference(genericTypeDef));
        foreach (var arg in genericArgs)
        {
            genericInstance.GenericArguments.Add(arg);
        }

        return genericInstance;
    }

    /// <summary>
    /// Parses generic arguments from a string like "System.String,System.Int32" or nested like "System.Collections.Generic.List`1[System.Int32],System.String"
    /// </summary>
    private static List<string> ParseGenericArguments(string argsSection)
    {
        var args = new List<string>();
        int depth = 0;
        int start = 0;

        for (int i = 0; i < argsSection.Length; i++)
        {
            char c = argsSection[i];
            if (c == '[')
                depth++;
            else if (c == ']')
                depth--;
            else if (c == ',' && depth == 0)
            {
                args.Add(argsSection.Substring(start, i - start).Trim());
                start = i + 1;
            }
        }

        // Add the last argument
        if (start < argsSection.Length)
        {
            args.Add(argsSection.Substring(start).Trim());
        }

        return args;
    }

    private TypeDefinition? FindTypeDefinitionInAssemblies(string fullTypeName)
    {
        // Search in current module
        var type = ModuleDefinition.GetType(fullTypeName);
        if (type != null)
            return type;

        // Search in referenced assemblies
        foreach (var reference in ModuleDefinition.AssemblyReferences)
        {
            try
            {
                var assembly = ModuleDefinition.AssemblyResolver.Resolve(reference);
                type = assembly.MainModule.GetType(fullTypeName);
                if (type != null)
                    return type;
            }
            catch
            {
                // Continue searching
            }
        }

        return null;
    }

    /// <summary>
    /// Check if two types match, considering generic type parameters.
    /// </summary>
    /// <param name="accessorParamType">The parameter type from the accessor method (e.g., T, int, List&lt;T&gt;)</param>
    /// <param name="targetParamType">The parameter type from the target method (e.g., !0, int, !0)</param>
    /// <param name="accessorMethod">The accessor method being weaved</param>
    /// <param name="targetDeclaringType">The declaring type of the target method</param>
    private static bool TypesMatch(TypeReference accessorParamType, TypeReference targetParamType, 
        MethodDefinition accessorMethod, TypeReference targetDeclaringType)
    {
        // Direct match - handles simple types like int, string, etc.
        if (accessorParamType.FullName == targetParamType.FullName)
            return true;

        // Handle target parameter being a generic parameter (e.g., !0, !T)
        if (targetParamType is GenericParameter targetGenericParam)
        {
            // Get the generic argument from the accessor's target type that corresponds to this parameter
            var resolvedType = ResolveGenericParameter(targetGenericParam, accessorParamType, accessorMethod, targetDeclaringType);
            if (resolvedType != null)
                return resolvedType.FullName == accessorParamType.FullName;
        }

        // Handle both being generic instances (e.g., List<int> vs List<!0>)
        if (accessorParamType is GenericInstanceType accessorGeneric && targetParamType is GenericInstanceType targetGeneric)
        {
            // Element types must match (e.g., both are List`1)
            if (accessorGeneric.ElementType.FullName != targetGeneric.ElementType.FullName)
                return false;

            // Check generic argument count
            if (accessorGeneric.GenericArguments.Count != targetGeneric.GenericArguments.Count)
                return false;

            // Check each generic argument
            for (int i = 0; i < accessorGeneric.GenericArguments.Count; i++)
            {
                if (!TypesMatch(accessorGeneric.GenericArguments[i], targetGeneric.GenericArguments[i], 
                    accessorMethod, targetDeclaringType))
                    return false;
            }
            return true;
        }

        // Handle accessor param being a generic parameter (e.g., method in GenericHelper<T> uses T)
        if (accessorParamType is GenericParameter accessorGenericParam)
        {
            // If target also expects a generic parameter at the same position, they match
            if (targetParamType is GenericParameter)
            {
                // Both are generic parameters - check if they represent the same position
                return accessorGenericParam.Position == ((GenericParameter)targetParamType).Position &&
                       accessorGenericParam.Type == ((GenericParameter)targetParamType).Type;
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves what type a generic parameter should be based on the context.
    /// </summary>
    private static TypeReference? ResolveGenericParameter(GenericParameter targetGenericParam, 
        TypeReference accessorParamType, MethodDefinition accessorMethod, TypeReference targetDeclaringType)
    {
        // For type generic parameters (e.g., !0, !T from class GenericClass<T>)
        if (targetGenericParam.Type == GenericParameterType.Type)
        {
            int position = targetGenericParam.Position;
            
            // First, check if targetDeclaringType is a generic instance (e.g., Box<int>)
            // If so, we can resolve the generic parameter to the concrete type argument
            if (targetDeclaringType is GenericInstanceType genericInstanceTarget)
            {
                if (position < genericInstanceTarget.GenericArguments.Count)
                {
                    return genericInstanceTarget.GenericArguments[position];
                }
            }
            
            // Check if the accessor's declaring type provides the generic argument
            var accessorDeclaringType = accessorMethod.DeclaringType;
            if (accessorDeclaringType.HasGenericParameters)
            {
                // The accessor is in a generic type, check if it has a matching generic parameter
                if (position < accessorDeclaringType.GenericParameters.Count)
                {
                    // Return the corresponding generic parameter from the accessor's declaring type
                    return accessorDeclaringType.GenericParameters[position];
                }
            }
            
            // If target is a generic instance type (e.g., List<int>), get the argument
            // This happens when we're calling a method on a concrete generic type
            return accessorParamType; // The accessor provides the concrete type
        }
        
        // For method generic parameters (e.g., !!0, !!T from method GenericMethod<T>)
        if (targetGenericParam.Type == GenericParameterType.Method)
        {
            // Check if accessor method has generic parameters
            if (accessorMethod.HasGenericParameters)
            {
                int position = targetGenericParam.Position;
                if (position < accessorMethod.GenericParameters.Count)
                {
                    return accessorMethod.GenericParameters[position];
                }
            }
            return accessorParamType;
        }

        return null;
    }

    /// <summary>
    /// Simple type match without generic context - for backward compatibility.
    /// </summary>
    private static bool TypesMatch(TypeReference a, TypeReference b)
    {
        return a.FullName == b.FullName;
    }

    private static int GetSafeMaxStackSize(int? hint, MethodDefinition method)
    {
        var count = hint ?? method.Parameters.Count;

        count = Math.Max(count, 8); // 8 is the default max stack size for most methods

        // align it to next multiple of 4
        return (count + 3) & ~3;
    }

    private static Instruction IlLoadArg(ILProcessor il, int index)
    {
        switch (index)
        {
            case 0:
                return il.Create(OpCodes.Ldarg_0);
            case 1:
                return il.Create(OpCodes.Ldarg_1);
            case 2:
                return il.Create(OpCodes.Ldarg_2);
            case 3:
                return il.Create(OpCodes.Ldarg_3);
            case { } when index <= byte.MaxValue:
                return il.Create(OpCodes.Ldarg_S, (byte)index);
            default:
                return il.Create(OpCodes.Ldarg, index);
        }
    }

    private static bool IsParameterMatches(IReadOnlyList<TypeReference> paramTypes, Collection<ParameterDefinition> parameters)
    {
        if (paramTypes.Count != parameters.Count)
            return false;

        for (int i = 0; i < paramTypes.Count; i++)
        {
            if (!TypesMatch(paramTypes[i], parameters[i].ParameterType))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Check if parameter types match with generic type resolution context.
    /// </summary>
    private static bool IsParameterMatches(IReadOnlyList<TypeReference> accessorParamTypes, 
        Collection<ParameterDefinition> targetParams, MethodDefinition accessorMethod, TypeReference targetDeclaringType)
    {
        if (accessorParamTypes.Count != targetParams.Count)
            return false;

        for (int i = 0; i < accessorParamTypes.Count; i++)
        {
            if (!TypesMatch(accessorParamTypes[i], targetParams[i].ParameterType, accessorMethod, targetDeclaringType))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Emits IL to cast/unbox a value if the source type differs from the target type.
    /// Used when accessor parameter is object but target expects a specific type.
    /// </summary>
    private void EmitCastIfNeeded(ILProcessor il, TypeReference sourceType, TypeReference targetType)
    {
        // No cast needed if types are the same
        if (sourceType.FullName == targetType.FullName)
            return;

        // No cast needed for generic parameters - they're resolved at runtime
        if (sourceType is GenericParameter || targetType is GenericParameter)
            return;

        var targetTypeRef = ModuleDefinition.ImportReference(targetType);

        // If source is object and target is a value type, use unbox.any
        if (sourceType.FullName == "System.Object" && targetType.Resolve()?.IsValueType == true)
        {
            il.Append(il.Create(OpCodes.Unbox_Any, targetTypeRef));
        }
        // If source is object and target is a reference type, use castclass
        else if (sourceType.FullName == "System.Object")
        {
            il.Append(il.Create(OpCodes.Castclass, targetTypeRef));
        }
    }

    /// <summary>
    /// Emits IL to box a value if the source is a value type but accessor returns object.
    /// </summary>
    private void EmitBoxIfNeeded(ILProcessor il, TypeReference sourceType, TypeReference targetReturnType)
    {
        // No boxing needed if types are the same
        if (sourceType.FullName == targetReturnType.FullName)
            return;

        // If target return type is object and source is a value type, box it
        if (targetReturnType.FullName == "System.Object" && sourceType.Resolve()?.IsValueType == true)
        {
            il.Append(il.Create(OpCodes.Box, ModuleDefinition.ImportReference(sourceType)));
        }
    }

    public override IEnumerable<string> GetAssembliesForScanning()
    {
        yield return "mscorlib";
        yield return "System";
        yield return "System.Runtime";
        yield return "netstandard";
    }

    /// <summary>
    /// Finds the CompilerGeneratedAttribute constructor from referenced assemblies
    /// instead of using typeof() which would get the version from the weaver's runtime.
    /// </summary>
    private MethodReference FindCompilerGeneratedAttributeConstructor()
    {
        var compilerGeneratedType = FindTypeDefinitionInAssemblies("System.Runtime.CompilerServices.CompilerGeneratedAttribute");
        if (compilerGeneratedType == null)
        {
            throw new WeavingException("Could not find CompilerGeneratedAttribute in referenced assemblies");
        }

        var ctor = compilerGeneratedType.Methods.FirstOrDefault(m => m.IsConstructor && m.Parameters.Count == 0);
        if (ctor == null)
        {
            throw new WeavingException("Could not find parameterless constructor for CompilerGeneratedAttribute");
        }

        return ModuleDefinition.ImportReference(ctor);
    }

    /// <summary>
    /// Finds a type from referenced assemblies by full name.
    /// This is used instead of typeof() to avoid version mismatch issues.
    /// </summary>
    private TypeReference FindTypeInReferencedAssemblies(string fullTypeName)
    {
        var typeDef = FindTypeDefinitionInAssemblies(fullTypeName);
        if (typeDef == null)
        {
            throw new WeavingException($"Could not find type {fullTypeName} in referenced assemblies");
        }
        return ModuleDefinition.ImportReference(typeDef);
    }
}
