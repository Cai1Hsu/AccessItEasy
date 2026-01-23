using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace AccessItEasy.Benchmarks;

[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 3)]
public class ConstructorBenchmarks
{
    // Reflection cached
    private ConstructorInfo _constructorInfo = null!;
    private object[] _argsBuffer = null!;

    // CreateDelegate - using compiled expression as constructor delegates need special handling
    private Func<int, BenchmarkConstructorTarget> _constructorDelegate = null!;

    // Expression tree compiled
    private Func<int, BenchmarkConstructorTarget> _expressionDelegate = null!;

    // IL Emit
    private Func<int, BenchmarkConstructorTarget> _ilDelegate = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Cache reflection
        _constructorInfo = typeof(BenchmarkConstructorTarget).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            [typeof(int)],
            null)!;
        _argsBuffer = new object[1];

        // Build expression tree delegate
        _expressionDelegate = BuildExpressionDelegate();

        // CreateDelegate for constructors uses expression tree
        _constructorDelegate = _expressionDelegate;

        // Build IL emit delegate
        _ilDelegate = BuildILDelegate();
    }

    #region PrivateAccessor

    [PrivateAccessor(PrivateAccessorKind.Constructor)]
    private static extern BenchmarkConstructorTarget PrivateAccessor_CreateInstance(int value);

    [PrivateAccessor(PrivateAccessorKind.Constructor)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static extern BenchmarkConstructorTarget PrivateAccessorInlined_CreateInstance(int value);

    #endregion

    #region UnsafeAccessor (.NET 8+)

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern BenchmarkConstructorTarget CreateInstanceUnsafe(int value);

    #endregion

    #region Benchmarks

    [Benchmark(Baseline = true)]
    public BenchmarkConstructorTarget DirectPublicFactory()
    {
        return BenchmarkConstructorTarget.CreatePublic(42);
    }

    [Benchmark]
    public BenchmarkConstructorTarget PrivateAccessor_Create()
    {
        return PrivateAccessor_CreateInstance(42);
    }

    [Benchmark]
    public BenchmarkConstructorTarget PrivateAccessorInlined_Create()
    {
        return PrivateAccessorInlined_CreateInstance(42);
    }

    [Benchmark]
    public BenchmarkConstructorTarget UnsafeAccessor_Create()
    {
        return CreateInstanceUnsafe(42);
    }

    [Benchmark]
    public BenchmarkConstructorTarget ReflectionCached_Create()
    {
        return (BenchmarkConstructorTarget)_constructorInfo.Invoke([42])!;
    }

    [Benchmark]
    public BenchmarkConstructorTarget ExpressionTree_Create()
    {
        return _expressionDelegate(42);
    }

    [Benchmark]
    public BenchmarkConstructorTarget ILEmit_Create()
    {
        return _ilDelegate(42);
    }

    [Benchmark]
    public BenchmarkConstructorTarget Delegate_Create()
    {
        return _constructorDelegate(42);
    }

    #endregion

    #region Helper Methods

    private Func<int, BenchmarkConstructorTarget> BuildExpressionDelegate()
    {
        var valueParam = Expression.Parameter(typeof(int), "value");
        var newExpr = Expression.New(_constructorInfo, valueParam);

        return Expression.Lambda<Func<int, BenchmarkConstructorTarget>>(newExpr, valueParam).Compile();
    }

    private Func<int, BenchmarkConstructorTarget> BuildILDelegate()
    {
        var method = new DynamicMethod(
            "CreateInstance_IL",
            typeof(BenchmarkConstructorTarget),
            [typeof(int)],
            typeof(ConstructorBenchmarks).Module,
            skipVisibility: true);

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Newobj, _constructorInfo);
        il.Emit(OpCodes.Ret);

        return method.CreateDelegate<Func<int, BenchmarkConstructorTarget>>();
    }

    #endregion
}
