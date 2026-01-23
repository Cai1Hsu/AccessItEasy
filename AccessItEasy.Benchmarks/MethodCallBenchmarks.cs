using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace AccessItEasy.Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3)]
public class MethodCallBenchmarks
{
    private BenchmarkMethodTarget _target = null!;

    // Reflection cached
    private MethodInfo _methodInfo = null!;
    private object[] _argsBuffer = null!;

    // CreateDelegate
    private Func<BenchmarkMethodTarget, int, int, int> _methodDelegate = null!;

    // Expression tree compiled
    private Func<BenchmarkMethodTarget, int, int, int> _expressionDelegate = null!;

    // IL Emit
    private Func<BenchmarkMethodTarget, int, int, int> _ilDelegate = null!;

    [GlobalSetup]
    public void Setup()
    {
        _target = new BenchmarkMethodTarget();

        // Cache reflection
        _methodInfo = typeof(BenchmarkMethodTarget).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)!;
        _argsBuffer = new object[2];

        // CreateDelegate - bind to open instance delegate
        _methodDelegate = BuildCreateDelegate();

        // Build expression tree delegate
        _expressionDelegate = BuildExpressionDelegate();

        // Build IL emit delegate
        _ilDelegate = BuildILDelegate();
    }

    #region PrivateAccessor

    [PrivateAccessor(PrivateAccessorKind.Method, Name = "Add")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static extern int CallAdd(BenchmarkMethodTarget target, int a, int b);

    [PrivateAccessor(PrivateAccessorKind.Method, Name = "Add")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static extern int CallAddInlined(BenchmarkMethodTarget target, int a, int b);

    #endregion

    #region UnsafeAccessor

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Add")]
    private static extern int CallAddUnsafe(BenchmarkMethodTarget target, int a, int b);

    #endregion

    #region Benchmarks

    [Benchmark]
    public int DirectPublicCall()
    {
        return _target.PublicAdd(3, 5);
    }

    [Benchmark(Baseline = true)]
    public int DirectPublicCall_NoInlining()
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        int add() => _target.PublicAdd(3, 5);

        return add();
    }

    [Benchmark]
    public int PrivateAccessor_Call()
    {
        return CallAdd(_target, 3, 5);
    }

    [Benchmark]
    public int PrivateAccessorInlined_Call()
    {
        return CallAddInlined(_target, 3, 5);
    }

    [Benchmark]
    public int UnsafeAccessor_Call()
    {
        return CallAddUnsafe(_target, 3, 5);
    }

    [Benchmark]
    public int ReflectionCached_Call()
    {
        return (int)_methodInfo.Invoke(_target, [3, 5])!;
    }

    [Benchmark]
    public int CreateDelegate_Call()
    {
        return _methodDelegate(_target, 3, 5);
    }

    [Benchmark]
    public int ExpressionTree_Call()
    {
        return _expressionDelegate(_target, 3, 5);
    }

    [Benchmark]
    public int ILEmit_Call()
    {
        return _ilDelegate(_target, 3, 5);
    }

    #endregion

    #region Helper Methods

    private Func<BenchmarkMethodTarget, int, int, int> BuildCreateDelegate()
    {
        // Create an open instance delegate
        return _methodInfo.CreateDelegate<Func<BenchmarkMethodTarget, int, int, int>>();
    }

    private Func<BenchmarkMethodTarget, int, int, int> BuildExpressionDelegate()
    {
        var targetParam = Expression.Parameter(typeof(BenchmarkMethodTarget), "target");
        var aParam = Expression.Parameter(typeof(int), "a");
        var bParam = Expression.Parameter(typeof(int), "b");

        var call = Expression.Call(targetParam, _methodInfo, aParam, bParam);

        return Expression.Lambda<Func<BenchmarkMethodTarget, int, int, int>>(
            call, targetParam, aParam, bParam).Compile();
    }

    private Func<BenchmarkMethodTarget, int, int, int> BuildILDelegate()
    {
        var method = new DynamicMethod(
            "CallAdd_IL",
            typeof(int),
            [typeof(BenchmarkMethodTarget), typeof(int), typeof(int)],
            typeof(MethodCallBenchmarks).Module,
            skipVisibility: true);

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Callvirt, _methodInfo);
        il.Emit(OpCodes.Ret);

        return method.CreateDelegate<Func<BenchmarkMethodTarget, int, int, int>>();
    }

    #endregion
}
