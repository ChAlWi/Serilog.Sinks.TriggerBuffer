# ADR 0002 — Test framework and assertions

- **Status:** Accepted (2026-11-20), xunit.v3 4.0.3 and Shouldly 4.3.0 pinned, `Microsoft.NET.Test.Sdk` not used
- **Date:** 2026-11-20

## Context

Tests run on `net10.0`, the library under test targets `net8.0`. We need a test framework, a test host and an
assertion library. Commercially licensed libraries are excluded (precedent: FluentAssertions from v8 on).

## A. Test framework

| # | Alternative | License (LICENSE in repo checked) | Commercial use | Notes |
| - | ----------- | --------------------------------- | -------------- | ----- |
| 1 | `xunit.v3` | Apache 2.0 (`xunit/xunit`) | Yes | Common in Serilog projects, parallel by default, new v3 architecture |
| 2 | `NUnit` | MIT (`nunit/nunit`) | Yes | Established, many attributes |
| 3 | `MSTest` | MIT (`microsoft/testfx`) | Yes | Microsoft default, few extra packages |
| 4 | Implement it ourselves | Own code | Yes | Disproportionate: runner, discovery and IDE integration are missing |

**Decision:** xunit.v3.

## B. Test SDK

`Microsoft.NET.Test.Sdk` (MIT, confirmed via nuspec for 18.12.0) is **not** installed. xunit.v3 4.x runs on Microsoft
Testing Platform: the test project uses `OutputType=Exe`, and a `global.json` sets `test.runner` to
`Microsoft.Testing.Platform`. `dotnet test` works without the package. It can be added later if needed.

## C. Assertions

| # | Alternative | License (LICENSE in repo checked) | Commercial use | Notes |
| - | ----------- | --------------------------------- | -------------- | ----- |
| 1 | `Shouldly` | BSD 3-Clause (`shouldly/shouldly`) | Yes | Replacement for FluentAssertions |
| 2 | Built-in `Assert` methods of the framework | Same as the framework | Yes | No extra package; less readable failure messages |
| 3 | FluentAssertions | Commercial from v8 | No | Excluded immediately |

**Decision:** Shouldly.

## Decision

`xunit.v3` and `Shouldly`, in the test project only, versions pinned centrally in `Directory.Packages.props`.
No mocking library: a hand-written `ILogEventSink` collecting `LogEvent`s is enough as a test double. If a mocking
framework turns out to be necessary, it needs its own ADR.

## Consequences

- Two test packages, both in the test project only, with no effect on the shipped library.
- NuGet license fields confirmed: xunit.v3 4.0.3 Apache-2.0, Shouldly 4.3.0 BSD-3-Clause.
- Transitive test dependencies (test project only): from xunit.v3 among others `xunit.v3.*`,
  `Microsoft.Testing.Platform` 2.5.2 and `Microsoft.Testing.Extensions.Telemetry` with `Microsoft.ApplicationInsights`;
  from Shouldly `DiffEngine` and `EmptyFiles`. The licenses of these transitive packages have **not yet been checked
  individually**. The telemetry extension may send usage data during test runs; the opt-out
  (`TESTINGPLATFORM_TELEMETRY_OPTOUT=1`) is still to be verified.
