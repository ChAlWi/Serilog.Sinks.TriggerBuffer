# ADR 0004 — Serilog.Settings.Configuration for configuration tests

- **Status:** Accepted (2026-11-20)
- **Date:** 2026-11-20

## Context

The product requirements demand that the sink can be configured from C# and from `appsettings.json`, including a
nested target sink (acceptance criterion AK-17: both ways must lead to the same behavior). JSON configuration of
Serilog is implemented by the package `Serilog.Settings.Configuration`, which discovers sink extension methods and
binds configuration values to their parameters.

To prove AK-17 we must run that real binding against our extension method. The package is therefore needed in the
**test project only**. The library itself does not reference it. It only exposes a normal extension method with
`Action<LoggerSinkConfiguration>` for the nested target and bindable parameter types (`LogEventLevel`, `int`,
`TimeSpan`, `bool`).

Findings (2026-11-20):

- Latest stable versions: 10.0.1 and 11.0.0. Version 10.0.1 targets `net8.0`, `net9.0`, `net10.0`,
  `netstandard2.0` and `net462`, and depends on Serilog 4.3.0 or newer. Version 11.0.0 depends on Serilog 4.4.0.
- Dependencies of 10.0.1: `Microsoft.Extensions.Configuration.Binder` 10.0.0, `Microsoft.Extensions.DependencyModel`
  10.0.0, `Serilog` 4.3.0 (all other than Serilog by Microsoft, MIT).
- To build a configuration from JSON in a test, a configuration source package such as
  `Microsoft.Extensions.Configuration.Json` (or the in-memory provider) is also needed. Which exact package is
  required, and its license, is **not yet verified** and is to be checked at installation.

## Alternatives

| # | Alternative | License / model | Commercial use | Assessment |
| - | ----------- | --------------- | -------------- | ---------- |
| 1 | `Serilog.Settings.Configuration` 10.0.1 | Apache 2.0 (NuGet license field and LICENSE in `serilog/serilog-settings-configuration` checked) | Yes | The real implementation used by applications, so the only way to prove AK-17 |
| 2 | `Serilog.Settings.Configuration` 11.0.0 | Apache 2.0 (NuGet license field) | Yes | Same package, newest version. Requires Serilog 4.4.0 or newer, so it forces our test project onto the newest dependency line without a need |
| 3 | Implement it ourselves (own reflection-based binder in the tests) | Own code | Yes | Would test our own binder, not the one consumers use. Does not prove AK-17 |
| 4 | No automated test, documented manual check only | None | Yes | Contradicts the acceptance criteria. Regressions in parameter names or types would go unnoticed |

## Decision (proposal)

Alternative 1: add `Serilog.Settings.Configuration` 10.0.1 to the test project and pin it centrally in
`Directory.Packages.props`.

- 10.0.1 is the lowest stable version that works with Serilog 4.4.0 and the `net10.0` test project while keeping the
  dependency set moderate. Moving to 11.0.0 later is a version bump, not a new decision.
- `Microsoft.Extensions.Configuration.Json` 10.0.0 is added to the test project as the JSON configuration source, so
  the tests read real `appsettings.json`-style content. Its license is MIT (NuGet license field checked), as are its
  transitive Microsoft packages `Microsoft.Extensions.Configuration`, `...Configuration.FileExtensions`,
  `Microsoft.Extensions.FileProviders.Abstractions` and `System.Text.Json` (dependencies from the nuspec; the
  license fields of the last three transitive packages are not individually checked).
- The library project does not get this dependency.

## Consequences

- Three or four additional packages in the test project, none shipped with the library.
- The parameter names of our public extension method become part of the JSON configuration surface and are covered by
  AK-17.
- Settings.Configuration finds extension methods by assembly name convention. The assembly name
  `Serilog.Sinks.TriggerBuffer` follows the convention. Whether additional configuration (`Using`) is needed is to be
  clarified by the test.
