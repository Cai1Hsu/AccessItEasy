#pragma warning disable

namespace AccessItEasy.Tests;

/// <summary>
/// Target class with private instance fields for testing field accessors.
/// </summary>
public class FieldTarget
{
    private int _intField = 42;
    private string _stringField = "hello";
    private object? _nullableField = null;

    public int GetIntFieldPublic() => _intField;
    public string GetStringFieldPublic() => _stringField;
    public object? GetNullableFieldPublic() => _nullableField;
}

/// <summary>
/// Target class with private static fields for testing static field accessors.
/// </summary>
public class StaticFieldTarget
{
    private static int _staticIntField = 100;
    private static string _staticStringField = "static hello";

    public static void ResetFields()
    {
        _staticIntField = 100;
        _staticStringField = "static hello";
    }

    public static int GetStaticIntFieldPublic() => _staticIntField;
    public static string GetStaticStringFieldPublic() => _staticStringField;
}

/// <summary>
/// Target class with private instance methods for testing method accessors.
/// </summary>
public class MethodTarget
{
    private int _state = 0;

    private void IncrementState()
    {
        _state++;
    }

    private int Add(int a, int b)
    {
        return a + b;
    }

    private string Concat(string a, string b, string c)
    {
        return a + b + c;
    }

    private void SetState(int value)
    {
        _state = value;
    }

    public int GetState() => _state;
}

/// <summary>
/// Target class with private static methods for testing static method accessors.
/// </summary>
public class StaticMethodTarget
{
    private static int _staticState = 0;

    private static void IncrementStaticState()
    {
        _staticState++;
    }

    private static int Multiply(int a, int b)
    {
        return a * b;
    }

    private static string FormatMessage(string template, int value)
    {
        return string.Format(template, value);
    }

    public static void ResetState()
    {
        _staticState = 0;
    }

    public static int GetStaticState() => _staticState;
}

/// <summary>
/// Target class with private constructor for testing constructor accessors.
/// </summary>
public class ConstructorTarget
{
    public int Value { get; }
    public string Name { get; }

    private ConstructorTarget()
    {
        Value = 0;
        Name = "default";
    }

    private ConstructorTarget(int value)
    {
        Value = value;
        Name = "int-constructor";
    }

    private ConstructorTarget(int value, string name)
    {
        Value = value;
        Name = name;
    }
}

/// <summary>
/// Internal class for testing PrivateAccessorType attribute.
/// </summary>
internal class InternalTarget
{
    private int _secret = 999;

    private InternalTarget() { }

    private InternalTarget(int secret)
    {
        _secret = secret;
    }

    public int GetSecret() => _secret;
}
