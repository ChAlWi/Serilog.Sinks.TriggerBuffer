# ADR 0003 — Time source and async disposal on netstandard2.0

- **Status:** Rejected (2026-11-20). The library targets `net8.0` (alternative 4), where `TimeProvider` and
  `IAsyncDisposable` are part of the framework. No package is added. Kept as a record of the analysis.
- **Date:** 2026-11-20

## Context

The library targets `netstandard2.0` (see ADR 0001). The sink needs two things that `netstandard2.0` does not
provide out of the box:

1. **A replaceable, monotonic time source.** `MaximumBufferAge` requires a timestamp at intake and an age check at
   flush time. Age tests must run deterministically without real waiting. The system clock is not suitable because
   it can jump.
2. **Asynchronous disposal.** The sink wraps a target sink and owns its lifecycle. The target may implement
   `IAsyncDisposable`. `System.IAsyncDisposable` does not exist in `netstandard2.0`.

Findings from inspecting the packages (2026-11-20):

- `Serilog` 4.4.0: the `netstandard2.0` assembly contains no `IAsyncDisposable` or `DisposeAsync`; the `net8.0`
  assembly does. A .NET 8+ application restores the `net8.0` asset at runtime, so async disposal can work there even
  though the library compiles against the `netstandard2.0` asset. **This still has to be proven by a test**
  (dispose the logger via `DisposeAsync` and observe the wrapped sink).
- `Microsoft.Bcl.TimeProvider` 8.0.1 provides `TimeProvider` for `netstandard2.0` and forwards to the inbox type
  on .NET 8+. On `netstandard2.0` it depends on `Microsoft.Bcl.AsyncInterfaces` 8.0.0, which provides
  `IAsyncDisposable`. So one decision covers both needs.

## Alternatives

| # | Alternative | License / model | Commercial use | Assessment |
| - | ----------- | --------------- | -------------- | ---------- |
| 1 | `Microsoft.Bcl.TimeProvider` 8.0.1 (brings `Microsoft.Bcl.AsyncInterfaces` 8.0.0) | MIT, both (NuGet license field and `dotnet/runtime` LICENSE checked) | Yes | Standard abstraction, forwards to inbox types on .NET 8+, solves both needs |
| 2 | Implement it ourselves: internal clock (`Func<long>` over `Stopwatch.GetTimestamp`) and no async disposal | Own code | Yes | No package. Time part is trivial. Without `IAsyncDisposable` the sink cannot close an async-only target asynchronously |
| 3 | Multi-target `net8.0` (and `netstandard2.0` only if still wanted) | None | Yes | Inbox `TimeProvider` and `IAsyncDisposable` on `net8.0`. A package would only be referenced for `netstandard2.0`, if kept at all |
| 4 | Single target `net8.0` | None | Yes | Simplest. Drops .NET Framework reach, which the PRD already excludes |

## Decision (proposal)

Alternative 1: reference `Microsoft.Bcl.TimeProvider` 8.0.1 in the library project and pin it centrally in
`Directory.Packages.props`.

- Pin the lowest sensible version (8.0.1) so consumers are not forced to newer runtimes' packages. The newest
  stable release is 11.0.0.
- `Microsoft.Bcl.AsyncInterfaces` is accepted as a transitive dependency of that package. We use its
  `IAsyncDisposable` explicitly, so it is named here.
- The test project targets `net10.0`, where `TimeProvider` is inbox. The fake time source for tests is a small
  hand-written `TimeProvider` subclass, so `Microsoft.Extensions.TimeProvider.Testing` is **not** needed.
- `required` and `init` still need our own internal polyfill attributes (no package).

## Consequences

- The library gets two MIT-licensed Microsoft packages on `netstandard2.0` in addition to Serilog and its two
  transitive packages (ADR 0001).
- Consumers on .NET 8+ receive type-forwarding assemblies only, no duplicate types.
- If the test mentioned above shows that async disposal of the target does not work through Serilog on
  `netstandard2.0`, fall back to alternative 3 or 4. That is the concrete blocker that triggers the move described
  in ADR 0001.
