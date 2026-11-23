using BenchmarkDotNet.Running;
using Serilog.Sinks.TriggerBuffer.Benchmarks;

// Run in Release mode, for example:
//   dotnet run --project benchmark/Serilog.Sinks.TriggerBuffer.Benchmarks -c Release -- --filter "*Baseline*"
// Check that the scenarios measure what they claim to measure, without measuring anything:
//   dotnet run --project benchmark/Serilog.Sinks.TriggerBuffer.Benchmarks -c Release -- --verify
if (args.Contains("--verify"))
{
    return Verifier.Run();
}

BenchmarkSwitcher.FromAssembly(typeof(BaselineBenchmarks).Assembly).Run(args);
return 0;
