using BenchmarkDotNet.Running;
using Serilog.Sinks.TriggerBuffer.Benchmarks;

// Run in Release mode, for example:
//   dotnet run --project benchmark/Serilog.Sinks.TriggerBuffer.Benchmarks -c Release -- --filter "*Baseline*"
BenchmarkSwitcher.FromAssembly(typeof(BaselineBenchmarks).Assembly).Run(args);
