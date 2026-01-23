using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace AccessItEasy.Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3)]
public class StaticFieldAccessBenchmarks
{
    // Reflection cached
    private FieldInfo _fieldInfo = null!;

    // Expression tree compiled
    private Func<int> _expressionGetter = null!;
    private Action<int> _expressionSetter = null!;

    // IL Emit
    private Func<int> _ilGetter = null!;
    private Action<int> _ilSetter = null!;

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkStaticFieldTarget.Reset();

        // Cache reflection
        _fieldInfo = typeof(BenchmarkStaticFieldTarget).GetField("_staticValue", BindingFlags.NonPublic | BindingFlags.Static)!;

        // Build expression tree getter/setter
        _expressionGetter = BuildExpressionGetter();
        _expressionSetter = BuildExpressionSetter();

        // Build IL emit getter/setter
        _ilGetter = BuildILGetter();
        _ilSetter = BuildILSetter();
    }

    #region PrivateAccessor

    const string targetTypeName = "AccessItEasy.Benchmarks.BenchmarkStaticFieldTarget";

    [PrivateAccessor(PrivateAccessorKind.StaticField, Name = "_staticValue")]
    private static extern int GetStaticValue([PrivateAccessorType(targetTypeName)] object? _);

    [PrivateAccessor(PrivateAccessorKind.StaticField, Name = "_staticValue")]
    private static extern void SetStaticValue([PrivateAccessorType(targetTypeName)] object? _, int value);

    [PrivateAccessor(PrivateAccessorKind.StaticField, Name = "_staticValue")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static extern int GetStaticValueInlined([PrivateAccessorType(targetTypeName)] object? _);

    [PrivateAccessor(PrivateAccessorKind.StaticField, Name = "_staticValue")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static extern void SetStaticValueInlined([PrivateAccessorType(targetTypeName)] object? _, int value);

    #endregion

    #region UnsafeAccessor

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "_staticValue")]
    private static extern ref int GetStaticValueUnsafe(BenchmarkStaticFieldTarget? _);

    #endregion

    #region Benchmarks - Get Static Field

    [Benchmark(Baseline = true)]
    public int DirectPublicAccess()
    {
        return BenchmarkStaticFieldTarget.GetStaticValue();
    }

    [Benchmark]
    public int PrivateAccessor_Get()
    {
        return GetStaticValue(null);
    }

    [Benchmark]
    public int PrivateAccessorInlined_Get()
    {
        return GetStaticValueInlined(null);
    }

    [Benchmark]
    public int UnsafeAccessor_Get()
    {
        return GetStaticValueUnsafe(null);
    }

    [Benchmark]
    public int ReflectionCached_Get()
    {
        return (int)_fieldInfo.GetValue(null)!;
    }

    [Benchmark]
    public int ExpressionTree_Get()
    {
        return _expressionGetter();
    }

    [Benchmark]
    public int ILEmit_Get()
    {
        return _ilGetter();
    }

    #endregion

    #region Benchmarks - Set Static Field

    [Benchmark]
    public void DirectPublicAccess_Set()
    {
        BenchmarkStaticFieldTarget.SetStaticValue(123);
    }

    [Benchmark]
    public void PrivateAccessor_Set()
    {
        SetStaticValue(null, 123);
    }

    [Benchmark]
    public void PrivateAccessorInlined_Set()
    {
        SetStaticValueInlined(null, 123);
    }

    [Benchmark]
    public void UnsafeAccessor_Set()
    {
        GetStaticValueUnsafe(null) = 123;
    }

    [Benchmark]
    public void ReflectionCached_Set()
    {
        _fieldInfo.SetValue(null, 123);
    }

    [Benchmark]
    public void ExpressionTree_Set()
    {
        _expressionSetter(123);
    }

    [Benchmark]
    public void ILEmit_Set()
    {
        _ilSetter(123);
    }

    #endregion

    #region Helper Methods

    private Func<int> BuildExpressionGetter()
    {
        var field = Expression.Field(null, _fieldInfo);
        return Expression.Lambda<Func<int>>(field).Compile();
    }

    private Action<int> BuildExpressionSetter()
    {
        var valueParam = Expression.Parameter(typeof(int), "value");
        var field = Expression.Field(null, _fieldInfo);
        var assign = Expression.Assign(field, valueParam);
        return Expression.Lambda<Action<int>>(assign, valueParam).Compile();
    }

    private Func<int> BuildILGetter()
    {
        var method = new DynamicMethod(
            "GetStaticValue_IL",
            typeof(int),
            Type.EmptyTypes,
            typeof(StaticFieldAccessBenchmarks).Module,
            skipVisibility: true);

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldsfld, _fieldInfo);
        il.Emit(OpCodes.Ret);

        return method.CreateDelegate<Func<int>>();
    }

    private Action<int> BuildILSetter()
    {
        var method = new DynamicMethod(
            "SetStaticValue_IL",
            typeof(void),
            [typeof(int)],
            typeof(StaticFieldAccessBenchmarks).Module,
            skipVisibility: true);

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Stsfld, _fieldInfo);
        il.Emit(OpCodes.Ret);

        return method.CreateDelegate<Action<int>>();
    }

    #endregion
}
