using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AccessItEasy.Fody;

public partial class ModuleWeaver
{
    /// <summary>
    /// Weaves a field accessor method.
    /// For instance reference: ref T GetFieldRef(TTarget @this) => ref @this._field;
    /// For instance getter: T GetField(TTarget @this) => @this._field;
    /// For instance setter: void SetField(TTarget @this, T value) => @this._field = value;
    /// For static reference: ref T GetFieldRef([TypeMarker] object _) => ref Target._field;
    /// For static getter: T GetField([TypeMarker] object _) => Target._field;
    /// For static setter: void SetField([TypeMarker] object _, T value) => Target._field = value;
    /// </summary>
    private void WeaveFieldAccessor(MethodDefinition method, string? fieldName, bool isStatic)
    {
        if (string.IsNullOrEmpty(fieldName))
        {
            WriteError($"Field accessor {method.FullName} must specify a field name");
            return;
        }

        var il = method.Body.GetILProcessor();
        method.Body.Instructions.Clear();
        method.Body.Variables.Clear();

        // Determine target type from first parameter
        if (method.Parameters.Count == 0)
        {
            WriteError($"Field accessor {method.FullName} must have at least one parameter");
            return;
        }

        TypeReference targetType = ResolveTargetType(method.Parameters[0]);

        var targetTypeDef = targetType.Resolve();
        if (targetTypeDef == null)
        {
            WriteError($"Could not resolve target type {targetType.FullName}");
            return;
        }

        var field = FindField(targetTypeDef, fieldName!);
        if (field == null)
        {
            WriteError($"Could not find field '{fieldName}' in type {targetTypeDef.FullName}");
            return;
        }

        var fieldRef = ModuleDefinition.ImportReference(field);

        // Get the actual return type from PrivateAccessorType if present
        var targetReturnType = ResolveReturnType(method);

        // TODO: Handle by-ref returns for field references
        bool isGetter = method.ReturnType.FullName != "System.Void";

        if (isGetter)
        {
            // Getter: return @this._field or return Type._field
            if (!isStatic)
            {
                il.Append(il.Create(OpCodes.Ldarg_0));
                il.Append(il.Create(OpCodes.Ldfld, fieldRef));
            }
            else
            {
                il.Append(il.Create(OpCodes.Ldsfld, fieldRef));
            }
            
            // Box if field is value type but accessor returns object
            EmitBoxIfNeeded(il, targetReturnType, method.ReturnType);
            
            il.Append(il.Create(OpCodes.Ret));
        }
        else
        {
            // Setter: @this._field = value or Type._field = value
            // Get the actual field type for unboxing
            var fieldType = ResolveTargetType(method.Parameters[method.Parameters.Count - 1]);
            
            if (!isStatic)
            {
                // Instance setter: void Set(target, value) => target._field = value
                il.Append(il.Create(OpCodes.Ldarg_0));
                il.Append(il.Create(OpCodes.Ldarg_1));
                
                // Unbox/cast if accessor param is object but field is specific type
                EmitCastIfNeeded(il, method.Parameters[1].ParameterType, fieldType);
                
                il.Append(il.Create(OpCodes.Stfld, fieldRef));
            }
            else
            {
                // Static setter: void Set([TypeMarker] _, value) => Target._field = value
                // First param is type marker (index 0), second param is value (index 1)
                il.Append(il.Create(OpCodes.Ldarg_1));
                
                // Unbox/cast if accessor param is object but field is specific type
                EmitCastIfNeeded(il, method.Parameters[1].ParameterType, fieldType);
                
                il.Append(il.Create(OpCodes.Stsfld, fieldRef));
            }
            il.Append(il.Create(OpCodes.Ret));
        }

        method.Body.MaxStackSize = GetSafeMaxStackSize(1, method);

        accessedTypes.Add(targetType);

        WriteDebug($"Weaved field accessor for {fieldName} in {method.FullName}");
    }

    private static FieldDefinition? FindField(TypeDefinition type, string fieldName)
    {
        var current = type;
        while (current != null)
        {
            var field = current.Fields.FirstOrDefault(f => f.Name == fieldName);
            if (field != null)
                return field;

            current = current.BaseType?.Resolve();
        }
        return null;
    }
}
