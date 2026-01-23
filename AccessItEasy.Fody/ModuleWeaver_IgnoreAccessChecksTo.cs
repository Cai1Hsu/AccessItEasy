using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AccessItEasy.Fody;

public partial class ModuleWeaver
{
    public TypeReference GetOrGenerateIgnoreAccessChecksToTypeRef()
    {
        // First check if the type is already defined in the current module
        var existingType = ModuleDefinition.Types.FirstOrDefault(
            t => t.FullName == IgnoresAccessChecksToAttributeName);
        if (existingType != null)
            return existingType;

        // Also check if it's referenced from another assembly
        if (ModuleDefinition.TryGetTypeReference(IgnoresAccessChecksToAttributeName, out var existingTypeRef))
            return existingTypeRef;

        // [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
        // internal sealed class IgnoresAccessChecksToAttribute : Attribute
        // {
        //     public IgnoresAccessChecksToAttribute(string assemblyName)
        //     {
        //         AssemblyName = assemblyName;
        //     }

        //     public string AssemblyName { get; }
        // }

        // Internal sealed class IgnoresAccessChecksToAttribute : Attribute
        var type = new TypeDefinition(
            "System.Runtime.CompilerServices",
            "IgnoresAccessChecksToAttribute",
            // private auto ansi sealed beforefieldinit
            TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit,
            ModuleDefinition.ImportReference(typeof(Attribute)));

        // Note: [AttributeUsage] is not strictly required for the attribute to work
        // and adding it can cause resolution issues in some target frameworks

        var assemblyNameBackingField = new FieldDefinition("<AssemblyName>k__BackingField", FieldAttributes.Private | FieldAttributes.InitOnly, ModuleDefinition.TypeSystem.String);

        // public string AssemblyName { get; }
        var getter = new MethodDefinition(
            "get_AssemblyName",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
            ModuleDefinition.TypeSystem.String);

        var getterIl = getter.Body.GetILProcessor();
        getterIl.Append(Instruction.Create(OpCodes.Ldarg_0));
        getterIl.Append(Instruction.Create(OpCodes.Ldfld, assemblyNameBackingField));
        getterIl.Append(Instruction.Create(OpCodes.Ret));

        var property = new PropertyDefinition(
            "AssemblyName",
            PropertyAttributes.None,
            ModuleDefinition.TypeSystem.String)
        {
            GetMethod = getter
        };

        type.Fields.Add(assemblyNameBackingField);
        type.Methods.Add(getter);  // Important: Add the getter method to the type's methods
        type.Properties.Add(property);

        // public IgnoresAccessChecksToAttribute(string assemblyName)
        var ctor = new MethodDefinition(
            ".ctor",
            // public hidebysig specialname rtspecialname
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            ModuleDefinition.TypeSystem.Void);

        var param = new ParameterDefinition("assemblyName", ParameterAttributes.None, ModuleDefinition.TypeSystem.String);
        ctor.Parameters.Add(param);

        // Constructor body
        var il = ctor.Body.GetILProcessor();

        var attributeCtor = typeof(Attribute).GetConstructor(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null);
        var attributeCtorRef = ModuleDefinition.ImportReference(attributeCtor);

        il.Append(Instruction.Create(OpCodes.Ldarg_0));
        il.Append(Instruction.Create(OpCodes.Call, attributeCtorRef));
        il.Append(Instruction.Create(OpCodes.Ldarg_0));
        il.Append(Instruction.Create(OpCodes.Ldarg_1));
        il.Append(Instruction.Create(OpCodes.Stfld, assemblyNameBackingField));
        il.Append(Instruction.Create(OpCodes.Ret));

        type.Methods.Add(ctor);

        // Add the type to the module
        ModuleDefinition.Types.Add(type);

        return type;
    }
}
