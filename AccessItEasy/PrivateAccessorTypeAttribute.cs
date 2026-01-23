using System;

namespace AccessItEasy;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = false, Inherited = false)]
public sealed class PrivateAccessorTypeAttribute : Attribute
{
    public PrivateAccessorTypeAttribute(string typeName)
    {
        TypeName = typeName;
    }

    public string TypeName { get; }
}
