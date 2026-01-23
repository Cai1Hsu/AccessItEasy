#pragma warning disable

using System.Runtime.CompilerServices;

namespace AccessItEasy.Benchmarks;

/// <summary>
/// Target class with private instance field for benchmarking field access.
/// </summary>
public class BenchmarkFieldTarget
{
    private int _value = 42;

    public int GetValue() => _value;
    public void SetValue(int value) => _value = value;
}

/// <summary>
/// Target class with private static field for benchmarking static field access.
/// </summary>
public class BenchmarkStaticFieldTarget
{
    private static int _staticValue = 100;

    public static int GetStaticValue() => _staticValue;
    public static void SetStaticValue(int value) => _staticValue = value;
    public static void Reset() => _staticValue = 100;
}

/// <summary>
/// Target class with private instance method for benchmarking method calls.
/// </summary>
public class BenchmarkMethodTarget
{
    private int Add(int a, int b) => a + b;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PublicAdd(int a, int b) => a + b;
}

/// <summary>
/// Target class with private static method for benchmarking static method calls.
/// </summary>
public class BenchmarkStaticMethodTarget
{
    private static int Multiply(int a, int b) => a * b;

    public static int PublicMultiply(int a, int b) => a * b;
}

/// <summary>
/// Target class with private constructor for benchmarking constructor access.
/// </summary>
public class BenchmarkConstructorTarget
{
    public int Value { get; }

    private BenchmarkConstructorTarget(int value)
    {
        Value = value;
    }

    public static BenchmarkConstructorTarget CreatePublic(int value) => new(value);
}
