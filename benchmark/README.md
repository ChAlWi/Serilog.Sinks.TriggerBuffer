# Benchmarks

This folder holds the benchmark project `Serilog.Sinks.TriggerBuffer.Benchmarks`. It measures what the trigger buffer
costs compared with a logger that has a sink that does nothing.

The method, the scenarios, the measurement conditions, the thresholds and the results are described in
[`docs/benchmarks.md`](../docs/benchmarks.md). Read that first. The numbers are measurements on one machine, not a
performance promise.

## What is here

| Part | Purpose |
| --- | --- |
| BenchmarkDotNet scenarios | Baseline (`BaselineBenchmarks`), hot path (`HotPathBenchmarks`), contention (`ContentionBenchmarks`) and trigger cost (`TriggerBenchmarks`) |
| Scenario D | Slow target, a custom measurement (`SlowTargetScenario`), started with `--slow-target` |
| `--verify` | Checks that the scenarios measure what they claim to, without measuring anything |

The project is part of the solution file, but it is **not** restored, built or run by `dotnet build`, `dotnet test` or
the CI workflow. It is run by hand in Release mode. Its only extra package is BenchmarkDotNet, see
[ADR 0006](../docs/adr/0006-benchmarkdotnet-for-performance-measurement.md).

## How to run

Run these from the repository root. Always use `-c Release`.

Check the scenarios (takes about a second):

```powershell
dotnet run --project benchmark/Serilog.Sinks.TriggerBuffer.Benchmarks -c Release -- --verify
```

Baseline, hot path, contention and trigger cost (BenchmarkDotNet, plan for an hour or more):

```powershell
dotnet run --project benchmark/Serilog.Sinks.TriggerBuffer.Benchmarks -c Release -- --filter "*Baseline*" "*HotPath*" "*Contention*" "*TriggerBenchmarks*"
```

Slow target (10 seconds per run, 3 runs per delay by default):

```powershell
dotnet run --project benchmark/Serilog.Sinks.TriggerBuffer.Benchmarks -c Release -- --slow-target
```

`--slow-target` accepts `--seconds <n>` and `--runs <n>`.

## Before you measure

Close other applications, use the "High performance" power plan and run on mains power. Scenario D needs at least six
free cores. The conditions that a result must list are in [`docs/benchmarks.md`](../docs/benchmarks.md).

BenchmarkDotNet writes its reports to a `BenchmarkDotNet.Artifacts` folder in the current directory. The folder is
ignored by git.
