using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace AccessItEasy.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        var config = DefaultConfig.Instance
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        // Run all benchmarks
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);

        // Alternative: run specific benchmark
        // BenchmarkRunner.Run<FieldAccessBenchmarks>(config);
        // BenchmarkRunner.Run<MethodCallBenchmarks>(config);
        // BenchmarkRunner.Run<ConstructorBenchmarks>(config);
        // BenchmarkRunner.Run<StaticFieldAccessBenchmarks>(config);
        // BenchmarkRunner.Run<StaticMethodCallBenchmarks>(config);
    }
}
