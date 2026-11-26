# ADR 0006 — BenchmarkDotNet for performance measurement

- **Status:** Accepted (2026-11-23), BenchmarkDotNet 0.15.8 pinned
- **Date:** 2026-11-23

## Context

The README states that there is no measured performance. Two design questions are open and need data: whether the
per-event work on the buffering path is worth making cheaper, and whether the lock that is held during the handover
blocks other logging threads badly enough to hand over outside the lock. The work is tracked in issue #3.

The measurements need warm-up, repetitions, statistics and allocation numbers per operation. Differences of a few
nanoseconds must be told apart from noise.

`BenchmarkDotNet` is needed in a **separate benchmark project only**. The library and the test project do not
reference it. The benchmark project is not packed and not published.

Findings (2026-11-23):

- Latest stable version: 0.15.8. License MIT (NuGet license field and `LICENSE.md` in `dotnet/BenchmarkDotNet`
  checked). Newer versions on nuget.org (0.16.0-preview.1 and 0.16.0-preview.2) are previews and are not used.
- Dependencies for `net8.0` and later (from the nuspec): `BenchmarkDotNet.Annotations`, `CommandLineParser`,
  `Gee.External.Capstone`, `Iced`, `Microsoft.CodeAnalysis.CSharp`, `Microsoft.Diagnostics.Runtime`,
  `Microsoft.Diagnostics.Tracing.TraceEvent`, `Microsoft.DotNet.PlatformAbstractions`, `Perfolizer`,
  `System.Management`. The licenses of these transitive packages were not checked individually. The project is
  maintained by the .NET Foundation, and the packages stay out of the published package.
- It provides a memory diagnoser (allocations per operation), configurable warm-up and iteration counts, and reports
  mean, error and standard deviation.

## Alternatives

| # | Alternative | License / model | Commercial use | Assessment |
| - | ----------- | --------------- | -------------- | ---------- |
| 1 | `BenchmarkDotNet` 0.15.8 in a separate project | MIT | Yes | Standard tool. Handles warm-up, statistics and allocation counting. Many transitive packages, which is acceptable because they stay out of the library and the test project |
| 2 | Own harness based on `Stopwatch` | Own code | Yes | No dependency, but unreliable for nanosecond measurements: no controlled warm-up, no JIT handling, no statistics, no allocation numbers. Not sufficient for the cheaper hot path question |
| 3 | `NBomber` or a load-test tool | Apache 2.0 | Yes | Aimed at load tests of services, not at micro-measurements of a method |
| 4 | No measurement | None | Yes | The two design questions stay open and the README stays without data |

## Decision

Alternative 1.

- Add `BenchmarkDotNet` to a new project under `benchmark/`, pinned centrally in `Directory.Packages.props`.
- The benchmark project is part of the solution so that it opens with the rest of the code, but it is not built by
  the solution build used in CI, and it is not a test project, so `dotnet test` does not run it.
- It is run manually in Release mode.
- The project is not packable. The package never reaches the published `Serilog.Sinks.TriggerBuffer` package.
- Scenario D (slow target, latency percentiles with several threads) does not fit the BenchmarkDotNet model well and
  is built as a custom measurement inside the same project. It uses no further package.

## Consequences

- The solution gains a project with a larger dependency set. Restore time grows by that amount.
- Benchmark numbers depend on the machine. The results document names the hardware, the runtime and the conditions.
- The benchmark project targets `net10.0` like the test project, because the measurements run on .NET 10. The
  library under test still targets `net8.0`. The results document names the measurement runtime.
- A change of the version requires updating this ADR.
