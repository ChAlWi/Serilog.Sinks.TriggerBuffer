# Serilog.Sinks.TriggerBuffer

[![CI](https://github.com/ChAlWi/Serilog.Sinks.TriggerBuffer/actions/workflows/ci.yml/badge.svg?branch=develop)](https://github.com/ChAlWi/Serilog.Sinks.TriggerBuffer/actions/workflows/ci.yml)
[![Main](https://github.com/ChAlWi/Serilog.Sinks.TriggerBuffer/actions/workflows/main.yml/badge.svg?branch=main)](https://github.com/ChAlWi/Serilog.Sinks.TriggerBuffer/actions/workflows/main.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Serilog.Sinks.TriggerBuffer)](https://www.nuget.org/packages/Serilog.Sinks.TriggerBuffer)

A Serilog sink that keeps recent low-level log events in memory and forwards them to a target sink only when an event
at a trigger level (by default `Error`) arrives. During normal operation the target receives nothing from the buffered
levels. When something goes wrong, it receives the context that led up to the error, followed by the error itself.

It reduces what is transmitted and stored centrally. It does **not** reduce what Serilog creates: the events to buffer
must still be produced and passed to this sink.

> **Status:** version 1 is feature complete. The package is not published to a feed yet, so reference the project
> until the publishing path is decided.

## Quick start

```csharp
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose() // see "Required setup" below
    .WriteTo.TriggerBuffer(t => t.Console())
    .CreateLogger();

Log.Debug("Loading the order");       // buffered
Log.Information("Order loaded");      // buffered
Log.Error("Payment failed");          // the console receives the two events above, then this one

Log.CloseAndFlush();
```

The target is configured in a nested action, so any sink can be the target:
`t => t.Console()`, `t => t.File("log.txt")`, `t => t.DurableHttpUsingFileSizeRolledBuffers(...)` and so on.
The target sinks come from their own packages (for example `Serilog.Sinks.Console`). This package depends only on
`Serilog`.

## Required setup

Serilog's own minimum level is `Information` unless you change it. Events below that level never reach any sink, so a
sink-level buffer for `Debug` or `Verbose` stays empty. Set `MinimumLevel` to the lowest level you want to buffer:

```csharp
new LoggerConfiguration().MinimumLevel.Debug() /* ... */
```

Filters that sit in front of this sink can drop events the same way. A level restriction inside the target
(for example `restrictedToMinimumLevel: LogEventLevel.Error` on the target sink) filters the context again and defeats
the buffer. Leave the target unrestricted.

## Configuration

| Parameter | Default | Meaning |
| --- | --- | --- |
| `configureTarget` | none, required | Configures the target sink. The sink owns it exclusively. |
| `bufferMinimumLevel` | `Verbose` | Lowest level that is buffered. Lower levels are dropped. |
| `triggerLevel` | `Error` | Events at this level or above forward the context and then themselves. |
| `maximumBufferCount` | `1000` | Maximum number of buffered context events. The trigger does not count. |
| `maximumBufferAge` | 60 seconds | Maximum age of context that may be forwarded. |
| `flushOnShutdown` | `false` | Forward the remaining valid context when the logger is closed. |

All values apply for the lifetime of the sink instance. There is no dynamic reload and there are no level switches.
Change them by creating the logger again.

The values are validated when the sink is created:

- `maximumBufferCount` must be at least 1 and `maximumBufferAge` must be greater than zero
  (`ArgumentOutOfRangeException`).
- `bufferMinimumLevel` must not be above `triggerLevel` (`ArgumentException`). Equal levels are allowed and leave
  no context range: only the trigger events are forwarded.
- `configureTarget` must not be `null` (`ArgumentNullException`).

### JSON configuration

The sink can be configured with `Serilog.Settings.Configuration`. The nested target goes into the `configureTarget`
argument:

```json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.TriggerBuffer", "Serilog.Sinks.Http" ],
    "MinimumLevel": "Verbose",
    "WriteTo": [
      {
        "Name": "TriggerBuffer",
        "Args": {
          "bufferMinimumLevel": "Debug",
          "triggerLevel": "Error",
          "maximumBufferCount": 500,
          "maximumBufferAge": "00:01:00",
          "flushOnShutdown": false,
          "configureTarget": [
            {
              "Name": "DurableHttpUsingFileSizeRolledBuffers",
              "Args": {
                "requestUri": "https://logs.example.com/ingest",
                "bufferBaseFileName": "logs/buffer"
              }
            }
          ]
        }
      }
    ]
  }
}
```

The `Using` entry for the target's assembly is needed so that its extension method is found. All arguments except
`configureTarget` are optional.

### With DurableHttp

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.TriggerBuffer(
        t => t.DurableHttpUsingFileSizeRolledBuffers(
            requestUri: "https://logs.example.com/ingest",
            bufferBaseFileName: "logs/buffer"),
        triggerLevel: LogEventLevel.Error,
        maximumBufferCount: 500)
    .CreateLogger();
```

This combination (`Serilog.Sinks.Http` 9.2.1, `DurableHttpUsingFileSizeRolledBuffers`) is covered by automated tests
for construction, configuration and regular closing. Those tests use a stand-in for the HTTP transport, so they do not
prove delivery to a real receiver.

## How it behaves

### Levels

| Incoming event | Behavior |
| --- | --- |
| Below `bufferMinimumLevel` | Dropped. Neither buffered nor forwarded. |
| From `bufferMinimumLevel` up to below `triggerLevel` | Buffered. |
| At or above `triggerLevel` | The valid buffered context is forwarded in arrival order, then the event itself. |

There is no separate immediate-output range. With the defaults, `Warning` is buffered only.

### A trigger

After a trigger the buffer holds none of the forwarded events. New context starts a new buffer. There is no follow-up
phase after an error, no throttling and no merging: every trigger is processed, even when errors follow each other.
A trigger without context is forwarded on its own.

Example with the defaults:

1. Request A logs an `Information` event. It is buffered.
2. Request B logs a `Debug` and a `Warning` event. Both are buffered and the target still receives nothing.
3. Request B logs an `Error`.
4. The target receives: the `Information` event of A, the `Debug` and `Warning` events of B, the `Error` of B.
5. A `Fatal` event logged right after is forwarded on its own.

### One buffer for everything

Each sink instance has exactly one shared buffer. An error in one request also forwards the context of other requests
and background jobs, and uses it up. Per-request isolation is not provided.

### Limits

- The count limit and the age limit apply at the same time. Neither is an alternative to the other.
- When the count is exceeded, the oldest buffered events are displaced first.
- Events older than `maximumBufferAge` are not forwarded. Age is measured from the moment the sink accepted the event,
  with a monotonic clock, not from `LogEvent.Timestamp`. An event that is exactly as old as the limit is still valid.
- Displaced and expired events are not restored.
- Expired events are removed when an event arrives, before a handover and at shutdown. There is no timer. During
  complete inactivity, memory is not released on time.
- `maximumBufferCount` is **not** a memory limit. Events with large properties or exceptions can occupy a lot of
  memory even with a small count.

### Order and concurrency

- The guaranteed order is the order in which the sink **accepted** the events, not the order of `LogEvent.Timestamp`.
  When calls arrive at the same time, the sink decides their order.
- Accepting events and handing over a block share one lock. A block of context events followed by its trigger is never
  mixed with the events of another caller.
- The handover is synchronous. The logging call that carries the trigger returns after the handover attempts. Other
  threads that log at the same time wait. **A slow target therefore delays the logging calls of the whole application.**
- The guaranteed order ends at the handover to the target. What a downstream system does with the events is up to it.
- An event that the target logs back into the same sink is ignored and reported through `SelfLog`, because it would
  change the block that is being handed over. Do not route a target back into the logger that contains this sink.

### Target failures

- The sink guarantees an ordered handover. It does not guarantee delivery. Batching, retries and persistence belong
  to the target.
- When the target throws for an event, the exception is not passed on to your code. The failure is reported through
  `SelfLog` (level and exception, not the event payload), and the remaining events, including the trigger, are still
  handed over.
- There are no retries. Failed events are not put back into the buffer. A block can therefore arrive incomplete, and a
  throwing target does not tell whether it accepted the event before it threw.

Make failures visible by enabling `SelfLog`:

```csharp
Serilog.Debugging.SelfLog.Enable(message => Console.Error.WriteLine(message));
```

### Shutdown and ownership

- The sink owns the target exclusively. Do not share the same target instance with anything else.
- When the logger is closed or disposed (`Log.CloseAndFlush()`, `Dispose()`, `DisposeAsync()`), the target is closed
  exactly once, whether or not remaining context was forwarded. Closing repeatedly is safe. Events that arrive after
  that are ignored.
- By default the remaining buffer is **discarded**, so restarts do not undo the savings. With
  `flushOnShutdown: true` the context that is still valid is forwarded before the target is closed.
- Asynchronous disposal closes the target with `DisposeAsync` if it supports it. A target that implements only
  `IAsyncDisposable` is not closed by a synchronous `Dispose()`. Serilog's own aggregate sink behaves the same way.
  Dispose the logger asynchronously in that case.
- A process crash, a hard kill or a termination forced by the host guarantees neither a flush of the remaining buffer
  nor the closing of the target.

## Security and data protection

The sink does not change, redact or filter the content of events. The application decides what may be logged and has to
redact before or independently of this sink.

Buffering means that levels such as `Debug` and `Verbose` are now forwarded in the case of an error. Before you enable
it, check whether these levels contain sensitive information, and whether your log storage and, for DurableHttp, the
local buffer files are approved for it.

## Limits at a glance

- No crash safety: buffered context that has not been triggered does not survive a process crash.
- No hard memory limit, see "Limits".
- No guarantee of delivery to an HTTP endpoint. That is the job of the target.
- No measured performance. Keeping overhead low is a design goal, but there are no benchmarks and no latency or
  throughput promises. The synchronous handover can block logging calls, and how long depends on the number and size
  of the events and on the target.
- No per-request, trace or correlation buffers, no manual flush, no follow-up phase, no throttling.

## Supported versions

| Component | Version |
| --- | --- |
| Library target | `net8.0`. It runs in applications on .NET 8 and newer. .NET Framework is not supported. |
| Serilog | 4.4.0 or newer |
| Tested with | `Serilog.Settings.Configuration` 10.0.1, `Serilog.Sinks.Http` 9.2.1 (`DurableHttpUsingFileSizeRolledBuffers`) |

The tests run on .NET 10. The library itself targets `net8.0`, but it has not been run on the .NET 8 runtime by the
test suite.

## Development

```powershell
dotnet test
```

The tests run through the public Serilog configuration with a recording target sink. Decisions about dependencies are
documented as ADRs in [`docs/adr`](docs/adr). A new package needs an ADR first.

## License

MIT, see [LICENSE](LICENSE).
