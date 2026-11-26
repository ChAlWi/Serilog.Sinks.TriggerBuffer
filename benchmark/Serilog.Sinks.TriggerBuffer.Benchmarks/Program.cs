using BenchmarkDotNet.Running;
using Serilog.Sinks.TriggerBuffer.Benchmarks;

// Run in Release mode, for example:
//   dotnet run --project benchmark/Serilog.Sinks.TriggerBuffer.Benchmarks -c Release -- --filter "*Baseline*"
// Check that the scenarios measure what they claim to measure, without measuring anything:
//   dotnet run --project benchmark/Serilog.Sinks.TriggerBuffer.Benchmarks -c Release -- --verify
// Scenario D (slow target) is a custom measurement and not a BenchmarkDotNet run. Seconds per run and runs per delay
// are optional:
//   dotnet run --project benchmark/Serilog.Sinks.TriggerBuffer.Benchmarks -c Release -- --slow-target --runs 3
if (args.Contains("--verify"))
{
    return Verifier.Run();
}

if (args.Contains("--slow-target"))
{
    return SlowTargetScenario.Run(args);
}

BenchmarkSwitcher.FromAssembly(typeof(BaselineBenchmarks).Assembly).Run(args);
return 0;
