# ADR 0005 — Serilog.Sinks.Http for the DurableHttp integration check

- **Status:** Accepted (2026-11-20), Serilog.Sinks.Http 9.2.1 pinned
- **Date:** 2026-11-20

## Context

The reference target of the sink is a DurableHttp variant from `Serilog.Sinks.Http`. Acceptance criterion AK-18
requires that construction, configuration binding and regular shutdown work with the agreed package version. The
requirements explicitly do **not** require an end-to-end test against a real HTTP receiver and do not claim
successful HTTP delivery.

`Serilog.Sinks.Http` is needed in the **test project only**. The library does not reference it. The library stays
independent of any concrete sink.

Findings (2026-11-20):

- Latest stable version: 9.2.1. License Apache-2.0 (NuGet license field and LICENSE in
  `FantasticFiasco/serilog-sinks-http` checked).
- Target frameworks: `net462`, `netstandard2.0`, `netstandard2.1`. There is no `net8.0` asset, so it is consumed via
  `netstandard2.0`.
- Dependencies: `Serilog.Sinks.File` 7.0.0 (Apache-2.0) and `Microsoft.Extensions.Configuration.Abstractions`
  10.0.1 (MIT).
- The assembly contains the durable variants `DurableHttpUsingFileSizeRolledBuffers` and
  `DurableHttpUsingTimeRolledBuffers`. Parameter names found in the assembly include `requestUri`,
  `bufferBaseFileName`, `bufferFileSizeLimitBytes`, `retainedBufferFileCountLimit`, `textFormatter`,
  `batchFormatter`, `httpClient`, `period` and `restrictedToMinimumLevel`. These come from the assembly strings and
  are not verified against the actual method signatures.
- No `IAsyncDisposable` or `DisposeAsync` was found in the assembly. The durable sink is presumably closed through
  `IDisposable`. This is relevant for the shutdown behavior and has to be confirmed by the test.

## Reference variant and open point

- **Variant (confirmed 2026-11-20):** `DurableHttpUsingFileSizeRolledBuffers`. The time-rolled variant is not
  covered by the integration test.
- **Version (confirmed 2026-11-20):** 9.2.1, matching the real applications.

## Alternatives

| # | Alternative | License / model | Commercial use | Assessment |
| - | ----------- | --------------- | -------------- | ---------- |
| 1 | `Serilog.Sinks.Http` 9.2.1 in the test project | Apache 2.0 | Yes | The real reference target. Proves AK-18 for the agreed version. Writes buffer files, so tests must use a temporary directory and must not contact a real endpoint |
| 2 | Implement it ourselves (own fake "durable" sink in the tests) | Own code | Yes | Proves nothing about the real package. Not sufficient for AK-18, but still the right test double for the functional tests (AK-01 to AK-16) |
| 3 | No automated test, documentation only | None | Yes | Contradicts AK-18. Incompatibilities (construction, closing, configuration binding) would go unnoticed |

Alternatives to the HTTP sink itself are out of scope: the requirements name this package.

## Decision (proposal)

Alternative 1 for AK-18, with alternative 2 as the test double for all functional tests.

- Add `Serilog.Sinks.Http` to the test project, pinned centrally in `Directory.Packages.props`, in the version
  confirmed as the open point above (candidate: 9.2.1).
- The integration test uses `DurableHttpUsingFileSizeRolledBuffers`.
- The integration test uses a temporary buffer directory and a stubbed HTTP client (the sink accepts an `httpClient`),
  so that no network access is needed and no delivery is claimed.
- The library project does not get this dependency.

## Consequences

- Test project gains `Serilog.Sinks.Http` and its transitive packages `Serilog.Sinks.File` and
  `Microsoft.Extensions.Configuration.Abstractions`. Licenses are Apache 2.0 and MIT as checked above.
- The test for AK-18 is the place where the shutdown behavior against a real durable sink is observed (see the
  `IAsyncDisposable` note in the context).
- A change of the reference version or variant requires updating the test and this ADR.
- The file-size rolled variant writes buffer files with a size limit (`bufferFileSizeLimitBytes`,
  `retainedBufferFileCountLimit`). The test sets both to small values in its temporary directory.
