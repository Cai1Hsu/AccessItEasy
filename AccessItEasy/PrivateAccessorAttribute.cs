using System;

namespace AccessItEasy;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class PrivateAccessorAttribute : Attribute
{
    public PrivateAccessorKind Kind { get; }

    public string? Name { get; set; }

    public PrivateAccessorAttribute(PrivateAccessorKind kind)
    {
        Kind = kind;
    }
}
