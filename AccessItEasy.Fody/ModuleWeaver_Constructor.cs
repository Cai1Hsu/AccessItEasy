using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AccessItEasy.Fody;

public partial class ModuleWeaver
{
    /// <summary>
    /// Weaves a constructor accessor.
    /// Example: T CreateInstance(int arg) => new T(arg);
    /// </summary>
    private void WeaveConstructorAccessor(MethodDefinition method, string? _)
    {
        var il = method.Body.GetILProcessor();
        method.Body.Instructions.Clear();
        method.Body.Variables.Clear();

        // Target type comes from return type or explicit specification
        var targetType = ResolveReturnType(method);
        var targetTypeDef = targetType.Resolve();

        if (targetTypeDef == null)
        {
            WriteError($"Could not resolve target type for constructor accessor {method.FullName}");
            return;
        }

        var expectedParamTypes = method.Parameters
            .Select(p => ResolveTargetType(p))
            .ToList();

        var constructor = FindConstructor(targetTypeDef, expectedParamTypes, method, targetType);
        if (constructor == null)
        {
            WriteError($"Could not find matching constructor in type {targetTypeDef.FullName}");
            return;
        }

        // Create the constructor reference with proper generic arguments if needed
        var ctorRef = CreateConstructorReference(constructor, targetType);

        // Load all arguments
        for (int i = 0; i < method.Parameters.Count; i++)
        {
            il.Append(IlLoadArg(il, i));
        }

        // Call constructor
        il.Append(il.Create(OpCodes.Newobj, ctorRef));
        il.Append(il.Create(OpCodes.Ret));

        method.Body.MaxStackSize = GetSafeMaxStackSize(null, method);

        accessedTypes.Add(targetType);

        WriteDebug($"Weaved constructor accessor in {method.FullName}");
    }

    /// <summary>
    /// Creates a constructor reference with proper generic type arguments resolved.
    /// For a constructor like Box&lt;T&gt;(T), when called for Box&lt;int&gt;, this creates
    /// a reference that becomes: Box&lt;int&gt;::.ctor(!0) which the runtime resolves correctly.
    /// </summary>
    private MethodReference CreateConstructorReference(MethodDefinition constructor, TypeReference targetType)
    {
        // If the target type is a generic instance (e.g., Box<int>), we need to bind the constructor to that instance
        if (targetType is GenericInstanceType genericTargetType)
        {
            // The declaring type should be the generic instance, not the definition
            var ctorRef = new MethodReference(".ctor", constructor.ReturnType)
            {
                DeclaringType = ModuleDefinition.ImportReference(genericTargetType),
                HasThis = constructor.HasThis,
                ExplicitThis = constructor.ExplicitThis,
                CallingConvention = constructor.CallingConvention,
            };

            // Copy parameters as-is (keeping generic parameter references like !0)
            foreach (var param in constructor.Parameters)
            {
                ctorRef.Parameters.Add(new ParameterDefinition(param.Name, param.Attributes, param.ParameterType));
            }

            return ModuleDefinition.ImportReference(ctorRef);
        }

        // For non-generic types, just import directly
        return ModuleDefinition.ImportReference(constructor);
    }

    private MethodDefinition? FindConstructor(TypeDefinition type, List<TypeReference> paramTypes,
        MethodDefinition accessorMethod, TypeReference targetType)
    {
        foreach (var method in type.Methods)
        {
            if (!method.IsConstructor || method.IsStatic)
                continue;

            if (method.Parameters.Count != paramTypes.Count)
                continue;

            // match parameter types with generic context
            if (!IsParameterMatches(paramTypes, method.Parameters, accessorMethod, targetType))
                continue;

            return method;
        }

        return null;
    }
}
