using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AccessItEasy.Fody;

public partial class ModuleWeaver
{
    /// <summary>
    /// Weaves a method accessor.
    /// Example: void CallFoo(TTarget @this, int arg) => @this.Foo(arg);
    /// For static: void CallBar([TypeMarker] object _, int arg) => Target.Bar(arg);
    /// </summary>
    private void WeaveMethodAccessor(MethodDefinition method, string? targetMethodName, bool isStatic)
    {
        if (string.IsNullOrEmpty(targetMethodName))
        {
            WriteError($"Method accessor {method.FullName} must specify a method name");
            return;
        }

        if (targetMethodName is ".ctor")
        {
            WriteError($"Method accessor {method.FullName} cannot target a constructor. Use Constructor accessor instead.");
            return;
        }

        var il = method.Body.GetILProcessor();
        method.Body.Instructions.Clear();
        method.Body.Variables.Clear();

        TypeReference targetType;
        int paramOffset;

        if (isStatic)
        {
            if (method.Parameters.Count == 0)
            {
                WriteError($"Static method accessor {method.FullName} must have at least one parameter for type specification");
                return;
            }
            // First parameter is always the type marker - extract type but don't forward
            targetType = ResolveTargetType(method.Parameters[0]);
            paramOffset = 1; // Skip the type marker parameter
        }
        else
        {
            if (method.Parameters.Count == 0)
            {
                WriteError($"Instance method accessor {method.FullName} must have at least one parameter for 'this'");
                return;
            }
            targetType = ResolveTargetType(method.Parameters[0]);
            paramOffset = 1; // Skip 'this' parameter
        }

        var targetTypeDef = targetType.Resolve();
        if (targetTypeDef == null)
        {
            WriteError($"Could not resolve target type {targetType.FullName}");
            return;
        }

        // Build expected parameter types for the target method
        // Skip first parameter (type marker for static, 'this' for instance)
        var expectedParamTypes = method.Parameters.Skip(1)
            .Select(p => ResolveTargetType(p))
            .ToList();

        var targetMethod = FindMethod(targetTypeDef, targetMethodName!, expectedParamTypes, isStatic, method, targetType);
        if (targetMethod == null)
        {
            WriteError($"Could not find method '{targetMethodName}' in type {targetTypeDef.FullName}");
            return;
        }

        // Create the method reference with proper generic arguments if needed
        var targetMethodRef = CreateMethodReference(targetMethod, targetType);

        // Load 'this' for instance methods (with cast if needed)
        if (!isStatic)
        {
            il.Append(IlLoadArg(il, 0));
            // Cast to target type if accessor param type is different (e.g., object -> TargetType)
            EmitCastIfNeeded(il, method.Parameters[0].ParameterType, targetType);
        }

        // Load all other arguments (with unbox/cast if needed)
        for (int i = paramOffset; i < method.Parameters.Count; i++)
        {
            il.Append(IlLoadArg(il, i));
            
            var accessorParamType = method.Parameters[i].ParameterType;
            var resolvedTargetParamType = expectedParamTypes[i - paramOffset];
            
            // Emit unbox or cast if accessor param type differs from target param type
            EmitCastIfNeeded(il, accessorParamType, resolvedTargetParamType);
        }

        // Call the target method
        if (isStatic)
        {
            il.Append(il.Create(OpCodes.Call, targetMethodRef));
        }
        else
        {
            // Use callvirt for instance methods to handle virtual dispatch
            il.Append(il.Create(OpCodes.Callvirt, targetMethodRef));
        }

        // Box return value if needed (target returns value type but accessor returns object)
        var targetReturnType = ResolveReturnType(method);
        EmitBoxIfNeeded(il, targetReturnType, method.ReturnType);

        il.Append(il.Create(OpCodes.Ret));

        method.Body.MaxStackSize = GetSafeMaxStackSize(null, method);

        accessedTypes.Add(targetType);

        WriteDebug($"Weaved method accessor for {targetMethodName} in {method.FullName}");
    }

    /// <summary>
    /// Creates a method reference with proper generic type arguments resolved.
    /// For a method like List&lt;T&gt;.Add(T), when called on List&lt;int&gt;, this creates
    /// a reference that becomes: List&lt;int&gt;::Add(!0) which the runtime resolves correctly.
    /// </summary>
    private MethodReference CreateMethodReference(MethodDefinition targetMethod, TypeReference targetType)
    {
        // If the target type is a generic instance (e.g., List<int>), we need to bind the method to that instance
        if (targetType is GenericInstanceType genericTargetType)
        {
            // The declaring type should be the generic instance, not the definition
            var methodRef = new MethodReference(targetMethod.Name, targetMethod.ReturnType)
            {
                DeclaringType = ModuleDefinition.ImportReference(genericTargetType),
                HasThis = targetMethod.HasThis,
                ExplicitThis = targetMethod.ExplicitThis,
                CallingConvention = targetMethod.CallingConvention,
            };
            
            // Copy parameters as-is (keeping generic parameter references like !0)
            foreach (var param in targetMethod.Parameters)
            {
                methodRef.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
            }
            
            // Copy generic parameters if the method itself is generic (not just the containing type)
            foreach (var gp in targetMethod.GenericParameters)
            {
                methodRef.GenericParameters.Add(new GenericParameter(gp.Name, methodRef));
            }
            
            return ModuleDefinition.ImportReference(methodRef);
        }
        
        // For non-generic types, just import directly
        return ModuleDefinition.ImportReference(targetMethod);
    }

    private MethodDefinition? FindMethod(TypeDefinition type, string methodName, List<TypeReference> paramTypes, 
        bool isStatic, MethodDefinition accessorMethod, TypeReference targetType)
    {
        var current = type;

        while (current != null)
        {
            foreach (var method in current.Methods)
            {
                if (method.Name != methodName)
                    continue;

                if (method.IsStatic != isStatic)
                    continue;

                if (method.Parameters.Count != paramTypes.Count)
                    continue;

                // match parameter types with generic context
                if (!IsParameterMatches(paramTypes, method.Parameters, accessorMethod, targetType))
                    continue;

                return method;
            }

            current = current.BaseType?.Resolve();
        }
        return null;
    }
}
