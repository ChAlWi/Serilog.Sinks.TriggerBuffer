# ADR 0001 — Dependency on Serilog

- **Status:** Accepted (2026-11-20), Serilog 4.4.0 pinned
- **Date:** 2026-11-20

## Context

This project is a Serilog sink (`ILogEventSink`). It needs the types `LogEvent`, `ILogEventSink`,
`LoggerSinkConfiguration` and `LogEventLevel`, which live in the `Serilog` package.

Target framework of the library: `netstandard2.0`. Moving to `net10.0` (or multi-targeting) is only done if a
concrete blocker appears.

## Alternatives

| # | Alternative | License / model | Commercial use | Assessment |
| - | ----------- | --------------- | -------------- | ---------- |
| 1 | `Serilog` (package) | Apache 2.0 (LICENSE in the `serilog/serilog` repo checked) | Yes | Required for a Serilog sink |
| 2 | Implement it ourselves (own abstraction instead of Serilog types) | Own code | Yes | Technically not possible: a sink must implement `ILogEventSink`, otherwise it is not a Serilog sink |
| 3 | `Microsoft.Extensions.Logging` as the base | MIT | Yes | Different ecosystem; the result would no longer be a Serilog sink, which contradicts the purpose of the project |

## Decision

Use `Serilog` (alternative 1).

- The version is pinned centrally in `Directory.Packages.props`.
- The minimum version must support `netstandard2.0`. A low minimum version lets consumers use more Serilog versions.

## Consequences

- A single runtime dependency in the library.
- NuGet license field of Serilog 4.4.0 confirmed: Apache-2.0.
- Transitive dependencies on `netstandard2.0` (from the nuspec): `System.Diagnostics.DiagnosticSource` 8.0.1 and
  `System.Threading.Channels` 8.0.0 (both Microsoft, MIT). They are unavoidable while Serilog is consumed via
  `netstandard2.0` and come with the Serilog package. Switching to `net10.0` would remove them, because Serilog
  pulls no additional packages there.
