using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace AccessItEasy.Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3)]
public class FieldAccessBenchmarks
{
    private BenchmarkFieldTarget _target = null!;

    // Reflection cached
    private FieldInfo _fieldInfo = null!;

    // Delegate created via CreateDelegate (not applicable for fields directly, using expression)
    private Func<BenchmarkFieldTarget, int> _getterDelegate = null!;
    private Action<BenchmarkFieldTarget, int> _setterDelegate = null!;

    // Expression tree compiled
    private Func<BenchmarkFieldTarget, int> _expressionGetter = null!;
    private Action<BenchmarkFieldTarget, int> _expressionSetter = null!;

    // IL Emit
    private Func<BenchmarkFieldTarget, int> _ilGetter = null!;
    private Action<BenchmarkFieldTarget, int> _ilSetter = null!;

    [GlobalSetup]
    public void Setup()
    {
        _target = new BenchmarkFieldTarget();

        // Cache reflection
        _fieldInfo = typeof(BenchmarkFieldTarget).GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // Build expression tree getter/setter
        _expressionGetter = BuildExpressionGetter();
        _expressionSetter = BuildExpressionSetter();

        // Build delegate using CreateDelegate pattern (via expression for fields)
        _getterDelegate = _expressionGetter; // Fields don't have direct method handles
        _setterDelegate = _expressionSetter;

        // Build IL emit getter/setter
        _ilGetter = BuildILGetter();
        _ilSetter = BuildILSetter();
    }

    #region PrivateAccessor

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "_value")]
    private static extern int GetValue(BenchmarkFieldTarget target);

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "_value")]
    private static extern void SetValue(BenchmarkFieldTarget target, int value);

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "_value")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static extern int GetValueInlined(BenchmarkFieldTarget target);

    [PrivateAccessor(PrivateAccessorKind.Field, Name = "_value")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static extern void SetValueInlined(BenchmarkFieldTarget target, int value);

    #endregion

    #region UnsafeAccessor

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_value")]
    private static extern ref int GetValueUnsafe(BenchmarkFieldTarget target);

    #endregion

    #region Benchmarks - Get Field

    [Benchmark(Baseline = true)]
    public int DirectPublicAccess()
    {
        return _target.GetValue();
    }

    [Benchmark]
    public int PrivateAccessor_Get()
    {
        return GetValue(_target);
    }

    [Benchmark]
    public int PrivateAccessorInlined_Get()
    {
        return GetValueInlined(_target);
    }

    [Benchmark]
    public int UnsafeAccessor_Get()
    {
        return GetValueUnsafe(_target);
    }

    [Benchmark]
    public int ReflectionCached_Get()
    {
        return (int)_fieldInfo.GetValue(_target)!;
    }

    [Benchmark]
    public int ExpressionTree_Get()
    {
        return _expressionGetter(_target);
    }

    [Benchmark]
    public int ILEmit_Get()
    {
        return _ilGetter(_target);
    }

    [Benchmark]
    public int Delegate_Get()
    {
        return _getterDelegate(_target);
    }

    #endregion

    #region Benchmarks - Set Field

    [Benchmark]
    public void DirectPublicAccess_Set()
    {
        _target.SetValue(123);
    }

    [Benchmark]
    public void PrivateAccessor_Set()
    {
        SetValue(_target, 123);
    }

    [Benchmark]
    public void PrivateAccessorInlined_Set()
    {
        SetValueInlined(_target, 123);
    }

    [Benchmark]
    public void UnsafeAccessor_Set()
    {
        GetValueUnsafe(_target) = 123;
    }

    [Benchmark]
    public void ReflectionCached_Set()
    {
        _fieldInfo.SetValue(_target, 123);
    }

    [Benchmark]
    public void ExpressionTree_Set()
    {
        _expressionSetter(_target, 123);
    }

    [Benchmark]
    public void ILEmit_Set()
    {
        _ilSetter(_target, 123);
    }

    [Benchmark]
    public void Delegate_Set()
    {
        _setterDelegate(_target, 123);
    }

    #endregion

    #region Helper Methods

    private Func<BenchmarkFieldTarget, int> BuildExpressionGetter()
    {
        var param = Expression.Parameter(typeof(BenchmarkFieldTarget), "target");
        var field = Expression.Field(param, _fieldInfo);
        return Expression.Lambda<Func<BenchmarkFieldTarget, int>>(field, param).Compile();
    }

    private Action<BenchmarkFieldTarget, int> BuildExpressionSetter()
    {
        var targetParam = Expression.Parameter(typeof(BenchmarkFieldTarget), "target");
        var valueParam = Expression.Parameter(typeof(int), "value");
        var field = Expression.Field(targetParam, _fieldInfo);
        var assign = Expression.Assign(field, valueParam);
        return Expression.Lambda<Action<BenchmarkFieldTarget, int>>(assign, targetParam, valueParam).Compile();
    }

    private Func<BenchmarkFieldTarget, int> BuildILGetter()
    {
        var method = new DynamicMethod(
            "GetValue_IL",
            typeof(int),
            [typeof(BenchmarkFieldTarget)],
            typeof(FieldAccessBenchmarks).Module,
            skipVisibility: true);

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, _fieldInfo);
        il.Emit(OpCodes.Ret);

        return method.CreateDelegate<Func<BenchmarkFieldTarget, int>>();
    }

    private Action<BenchmarkFieldTarget, int> BuildILSetter()
    {
        var method = new DynamicMethod(
            "SetValue_IL",
            typeof(void),
            [typeof(BenchmarkFieldTarget), typeof(int)],
            typeof(FieldAccessBenchmarks).Module,
            skipVisibility: true);

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, _fieldInfo);
        il.Emit(OpCodes.Ret);

        return method.CreateDelegate<Action<BenchmarkFieldTarget, int>>();
    }

    #endregion
}
