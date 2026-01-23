using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace AccessItEasy.Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3)]
public class StaticMethodCallBenchmarks
{
    // Reflection cached
    private MethodInfo _methodInfo = null!;

    // CreateDelegate
    private Func<int, int, int> _methodDelegate = null!;

    // Expression tree compiled
    private Func<int, int, int> _expressionDelegate = null!;

    // IL Emit
    private Func<int, int, int> _ilDelegate = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Cache reflection
        _methodInfo = typeof(BenchmarkStaticMethodTarget).GetMethod("Multiply", BindingFlags.NonPublic | BindingFlags.Static)!;

        // CreateDelegate
        _methodDelegate = _methodInfo.CreateDelegate<Func<int, int, int>>();

        // Build expression tree delegate
        _expressionDelegate = BuildExpressionDelegate();

        // Build IL emit delegate
        _ilDelegate = BuildILDelegate();
    }

    #region PrivateAccessor

    const string targetTypeName = "AccessItEasy.Benchmarks.BenchmarkStaticMethodTarget";

    [PrivateAccessor(PrivateAccessorKind.StaticMethod, Name = "Multiply")]
    private static extern int CallMultiply([PrivateAccessorType(targetTypeName)] object? _, int a, int b);

    [PrivateAccessor(PrivateAccessorKind.StaticMethod, Name = "Multiply")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static extern int CallMultiplyInlined([PrivateAccessorType(targetTypeName)] object? _, int a, int b);
    #endregion

    #region UnsafeAccessor

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "Multiply")]
    private static extern int CallMultiplyUnsafe(BenchmarkStaticMethodTarget? _, int a, int b);

    #endregion

    #region Benchmarks

    [Benchmark(Baseline = true)]
    public int DirectPublicCall()
    {
        return BenchmarkStaticMethodTarget.PublicMultiply(6, 7);
    }

    [Benchmark]
    public int PrivateAccessor_Call()
    {
        return CallMultiply(null, 6, 7);
    }

    [Benchmark]
    public int PrivateAccessorInlined_Call()
    {
        return CallMultiplyInlined(null, 6, 7);
    }

    [Benchmark]
    public int UnsafeAccessor_Call()
    {
        return CallMultiplyUnsafe(null, 6, 7);
    }

    [Benchmark]
    public int ReflectionCached_Call()
    {
        return (int)_methodInfo.Invoke(null, [6, 7])!;
    }

    [Benchmark]
    public int CreateDelegate_Call()
    {
        return _methodDelegate(6, 7);
    }

    [Benchmark]
    public int ExpressionTree_Call()
    {
        return _expressionDelegate(6, 7);
    }

    [Benchmark]
    public int ILEmit_Call()
    {
        return _ilDelegate(6, 7);
    }

    #endregion

    #region Helper Methods

    private Func<int, int, int> BuildExpressionDelegate()
    {
        var aParam = Expression.Parameter(typeof(int), "a");
        var bParam = Expression.Parameter(typeof(int), "b");

        var call = Expression.Call(_methodInfo, aParam, bParam);

        return Expression.Lambda<Func<int, int, int>>(call, aParam, bParam).Compile();
    }

    private Func<int, int, int> BuildILDelegate()
    {
        var method = new DynamicMethod(
            "CallMultiply_IL",
            typeof(int),
            [typeof(int), typeof(int)],
            typeof(StaticMethodCallBenchmarks).Module,
            skipVisibility: true);

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Call, _methodInfo);
        il.Emit(OpCodes.Ret);

        return method.CreateDelegate<Func<int, int, int>>();
    }

    #endregion
}
