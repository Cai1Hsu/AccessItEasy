using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

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

        // Find Attribute type from referenced assemblies instead of using typeof()
        var attributeType = FindTypeInReferencedAssemblies("System.Attribute");

        // Internal sealed class IgnoresAccessChecksToAttribute : Attribute
        var type = new TypeDefinition(
            "System.Runtime.CompilerServices",
            "IgnoresAccessChecksToAttribute",
            // private auto ansi sealed beforefieldinit
            TypeAttributes.Class | TypeAttributes.NotPublic | TypeAttributes.Sealed | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit,
            attributeType);

        // Find AttributeUsageAttribute constructor from referenced assemblies
        var attributeUsageType = FindTypeDefinitionInAssemblies("System.AttributeUsageAttribute");
        if (attributeUsageType == null)
        {
            throw new WeavingException("Could not find AttributeUsageAttribute in referenced assemblies");
        }
        var attributeUsageCtor = attributeUsageType.GetConstructors()
            .FirstOrDefault(c => c.Parameters.Count == 1);
        if (attributeUsageCtor == null)
        {
            throw new WeavingException("Could not find AttributeUsageAttribute constructor");
        }
        var attributeUsageCtorRef = ModuleDefinition.ImportReference(attributeUsageCtor);

        // AttributeTargets type reference import fails in some cases, so we manually create it
        // [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
        var blob = new byte[] { 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x54, 0x02, 0x0d, 0x41, 0x6c, 0x6c, 0x6f, 0x77, 0x4d, 0x75, 0x6c, 0x74, 0x69, 0x70, 0x6c, 0x65, 0x01 };

        type.CustomAttributes.Add(new CustomAttribute(attributeUsageCtorRef, blob));

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

        // Constructor body - find Attribute constructor from referenced assemblies
        var il = ctor.Body.GetILProcessor();

        var attributeTypeDef = attributeType.Resolve();
        var attributeBaseCtor = attributeTypeDef.GetConstructors()
            .FirstOrDefault(c => c.Parameters.Count == 0 && !c.IsStatic);
        if (attributeBaseCtor == null)
        {
            throw new WeavingException("Could not find parameterless constructor for System.Attribute");
        }
        var attributeCtorRef = ModuleDefinition.ImportReference(attributeBaseCtor);

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
