using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AccessItEasy.Fody;

public partial class ModuleWeaver
{
    /// <summary>
    /// Determines the field accessor kind based on the method signature.
    /// </summary>
    private enum FieldAccessorKind
    {
        /// <summary>
        /// Getter: T Get(Target) or T Get([TypeMarker] object _)
        /// </summary>
        Getter,

        /// <summary>
        /// Setter: void Set(Target, T) or void Set([TypeMarker] object _, T)
        /// </summary>
        Setter,

        /// <summary>
        /// Reference: ref T GetRef(Target) or ref T GetRef([TypeMarker] object _)
        /// </summary>
        Reference
    }

    /// <summary>
    /// Weaves a field accessor method.
    /// Supports three forms:
    /// 1. Getter: T Get(Target) - returns the field value
    /// 2. Setter: void Set(Target, T) - sets the field value
    /// 3. Reference: ref T GetRef(Target) - returns a reference to the field
    /// </summary>
    private void WeaveFieldAccessor(MethodDefinition method, string? fieldName, bool isStatic)
    {
        if (string.IsNullOrEmpty(fieldName))
        {
            WriteError($"Field accessor {method.FullName} must specify a field name");
            return;
        }

        // Determine target type from first parameter
        if (method.Parameters.Count == 0)
        {
            WriteError($"Field accessor {method.FullName} must have at least one parameter");
            return;
        }

        TypeReference targetType = ResolveTargetType(method.Parameters[0]);

        var fieldRef = ResolveFieldReference(targetType, fieldName!);
        if (fieldRef == null)
        {
            WriteError($"Could not find field '{fieldName}' in type {targetType.FullName}");
            return;
        }

        // Determine accessor kind
        var kind = DetermineFieldAccessorKind(method);

        switch (kind)
        {
            case FieldAccessorKind.Getter:
                WeaveFieldGetter(method, fieldRef, targetType, isStatic);
                break;
            case FieldAccessorKind.Setter:
                WeaveFieldSetter(method, fieldRef, targetType, isStatic);
                break;
            case FieldAccessorKind.Reference:
                WeaveFieldReference(method, fieldRef, targetType, isStatic);
                break;
        }

        accessedTypes.Add(targetType);
        WriteDebug($"Weaved field {kind.ToString().ToLower()} accessor for {fieldName} in {method.FullName}");
    }

    /// <summary>
    /// Determines the field accessor kind based on the method signature.
    /// </summary>
    private FieldAccessorKind DetermineFieldAccessorKind(MethodDefinition method)
    {
        // Setter: void return type
        if (method.ReturnType == ModuleDefinition.TypeSystem.Void)
            return FieldAccessorKind.Setter;

        // Reference: by-ref return type
        if (method.ReturnType.IsByReference)
            return FieldAccessorKind.Reference;

        // Getter: non-void, non-byref return type
        return FieldAccessorKind.Getter;
    }

    /// <summary>
    /// Weaves a field getter accessor.
    /// Instance: T Get(Target target) => target._field;
    /// Static: T Get([TypeMarker] object _) => Target._field;
    /// </summary>
    private void WeaveFieldGetter(MethodDefinition method, FieldReference fieldRef, TypeReference targetType, bool isStatic)
    {
        var il = method.Body.GetILProcessor();
        method.Body.Instructions.Clear();
        method.Body.Variables.Clear();

        // Get the actual return type from PrivateAccessorType if present
        var targetReturnType = ResolveReturnType(method);

        if (!isStatic)
        {
            // Instance getter: return target._field
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ldfld, fieldRef));
        }
        else
        {
            // Static getter: return Target._field
            il.Append(il.Create(OpCodes.Ldsfld, fieldRef));
        }

        // Box if field is value type but accessor returns object
        EmitBoxIfNeeded(il, targetReturnType, method.ReturnType);

        il.Append(il.Create(OpCodes.Ret));
        method.Body.MaxStackSize = GetSafeMaxStackSize(1, method);
    }

    /// <summary>
    /// Weaves a field setter accessor.
    /// Instance: void Set(Target target, T value) => target._field = value;
    /// Static: void Set([TypeMarker] object _, T value) => Target._field = value;
    /// </summary>
    private void WeaveFieldSetter(MethodDefinition method, FieldReference fieldRef, TypeReference targetType, bool isStatic)
    {
        var il = method.Body.GetILProcessor();
        method.Body.Instructions.Clear();
        method.Body.Variables.Clear();

        // Get the actual field type for unboxing
        var fieldType = ResolveTargetType(method.Parameters[method.Parameters.Count - 1]);

        if (!isStatic)
        {
            // Instance setter: target._field = value
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ldarg_1));

            // Unbox/cast if accessor param is object but field is specific type
            EmitCastIfNeeded(il, method.Parameters[1].ParameterType, fieldType);

            il.Append(il.Create(OpCodes.Stfld, fieldRef));
        }
        else
        {
            // Static setter: Target._field = value
            // First param is type marker (index 0), second param is value (index 1)
            il.Append(il.Create(OpCodes.Ldarg_1));

            // Unbox/cast if accessor param is object but field is specific type
            EmitCastIfNeeded(il, method.Parameters[1].ParameterType, fieldType);

            il.Append(il.Create(OpCodes.Stsfld, fieldRef));
        }

        il.Append(il.Create(OpCodes.Ret));
        method.Body.MaxStackSize = GetSafeMaxStackSize(2, method);
    }

    /// <summary>
    /// Weaves a field reference accessor.
    /// Instance: ref T GetRef(Target target) => ref target._field;
    /// Static: ref T GetRef([TypeMarker] object _) => ref Target._field;
    /// 
    /// Note: Reference accessors require exact type matching. The return type must exactly match
    /// the field type - no base class, subclass, or PrivateAccessorTypeAttribute type substitution is allowed.
    /// </summary>
    private void WeaveFieldReference(MethodDefinition method, FieldReference fieldRef, TypeReference targetType, bool isStatic)
    {
        var il = method.Body.GetILProcessor();
        method.Body.Instructions.Clear();
        method.Body.Variables.Clear();

        // Get the element type of the by-ref return type
        var returnType = method.ReturnType;
        if (!returnType.IsByReference)
        {
            WriteError($"Field reference accessor {method.FullName} must have a by-ref return type");
            return;
        }

        var byRefReturnType = (ByReferenceType)returnType;
        var elementType = byRefReturnType.ElementType;

        // Validate exact type match - reference accessors don't support type conversion
        var fieldType = fieldRef.FieldType;
        if (elementType.FullName != fieldType.FullName)
        {
            WriteError($"Field reference accessor {method.FullName} return type 'ref {elementType.FullName}' must exactly match field type '{fieldType.FullName}'. Reference accessors do not support type conversion.");
            return;
        }

        // Check if PrivateAccessorTypeAttribute is used on return type (not allowed for reference)
        var returnTypeAttr = method.MethodReturnType.CustomAttributes.FirstOrDefault(
            a => a.AttributeType.FullName == PrivateAccessorTypeAttributeName);
        if (returnTypeAttr != null)
        {
            WriteError($"Field reference accessor {method.FullName} cannot use PrivateAccessorTypeAttribute on return type. Reference accessors require exact type matching.");
            return;
        }

        if (!isStatic)
        {
            // Instance reference: return ref target._field
            // ldarg.0
            // ldflda fieldRef
            // ret
            il.Append(il.Create(OpCodes.Ldarg_0));
            il.Append(il.Create(OpCodes.Ldflda, fieldRef));
        }
        else
        {
            // Static reference: return ref Target._field
            // ldsflda fieldRef
            // ret
            il.Append(il.Create(OpCodes.Ldsflda, fieldRef));
        }

        il.Append(il.Create(OpCodes.Ret));
        method.Body.MaxStackSize = GetSafeMaxStackSize(1, method);
    }

    private FieldReference? ResolveFieldReference(TypeReference declaringType, string fieldName)
    {
        var currentRef = declaringType;
        var currentDef = declaringType.Resolve();

        while (currentRef is not null && currentDef is not null)
        {
            var fieldDef = currentDef.Fields.FirstOrDefault(f => f.Name == fieldName);

            if (fieldDef != null)
                return ImportField(fieldDef, currentDef);

            if (currentDef.BaseType is null)
                break;

            currentRef = currentDef.BaseType; // FIXME: using definition loses generic arguments
            currentDef = currentRef?.Resolve();
        }

        return null;

        FieldReference ImportField(FieldDefinition field, TypeReference declaringTypeRef)
        {
            var fieldTypeRef = ModuleDefinition.ImportReference(field.FieldType);
            declaringTypeRef = ModuleDefinition.ImportReference(declaringType);

            var fieldRef = new FieldReference(field.Name, fieldTypeRef, declaringTypeRef);

            return ModuleDefinition.ImportReference(fieldRef);
        }
    }
}
