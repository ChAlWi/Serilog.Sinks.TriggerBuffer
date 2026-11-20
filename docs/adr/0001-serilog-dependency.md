# ADR 0001 — Dependency on Serilog

- **Status:** Accepted (2026-11-20), Serilog 4.4.0 pinned
- **Date:** 2026-11-20

## Context

This project is a Serilog sink (`ILogEventSink`). It needs the types `LogEvent`, `ILogEventSink`,
`LoggerSinkConfiguration` and `LogEventLevel`, which live in the `Serilog` package.

Target framework of the library: `net8.0`. The product requirements ask for consumers on .NET 8 and newer and
exclude .NET Framework, so `netstandard2.0` would add polyfills and extra packages without a benefit. A `net8.0`
library also runs in .NET 9 and 10 applications.

## Alternatives

| # | Alternative | License / model | Commercial use | Assessment |
| - | ----------- | --------------- | -------------- | ---------- |
| 1 | `Serilog` (package) | Apache 2.0 (LICENSE in the `serilog/serilog` repo checked) | Yes | Required for a Serilog sink |
| 2 | Implement it ourselves (own abstraction instead of Serilog types) | Own code | Yes | Technically not possible: a sink must implement `ILogEventSink`, otherwise it is not a Serilog sink |
| 3 | `Microsoft.Extensions.Logging` as the base | MIT | Yes | Different ecosystem; the result would no longer be a Serilog sink, which contradicts the purpose of the project |

## Decision

Use `Serilog` (alternative 1).

- The version is pinned centrally in `Directory.Packages.props`.
- The minimum version must support `net8.0`. A low minimum version lets consumers use more Serilog versions.

## Consequences

- A single runtime dependency in the library.
- NuGet license field of Serilog 4.4.0 confirmed: Apache-2.0.
- On `net8.0` the Serilog 4.4.0 package has no further package dependencies (checked in the nuspec). Only the
  `netstandard2.0` and .NET Framework assets pull `System.Diagnostics.DiagnosticSource` and
  `System.Threading.Channels`, which is one of the reasons for targeting `net8.0`.
- `TimeProvider` and `IAsyncDisposable` are part of .NET 8, so no polyfill package is needed (see ADR 0003).
