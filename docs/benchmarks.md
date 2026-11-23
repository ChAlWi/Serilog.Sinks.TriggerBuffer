# Benchmarks

These are **measurements, not a promise**. They show what the sink cost on one machine under the conditions listed
with each result. The README makes no performance guarantee, and this document does not change that.

> **Status (2026-11-23):** the benchmark project is complete and verified, but there is **no measurement under the
> agreed conditions yet**. Only a short trial run on a machine that did not meet them (balanced power plan, other
> applications running) has been made. Its numbers are not published here and do not decide anything. The sections
> "Results" and "Decisions" stay empty until a proper run exists.

## Why

Two design questions need data (issue #3):

1. **Idea 2, a cheaper hot path.** Is the per-event work on the buffering path (timestamp, expiry check, enqueue,
   displacement) noticeable compared with what Serilog itself costs?
2. **Idea 1, handover outside the lock.** The handover to the target holds the sink's lock. How badly does a slow
   target delay the logging calls of other threads?

## How to run

Use a Release build. The benchmark project is not part of the solution build, CI or `dotnet test`, so it is run by hand.

```powershell
# Check that the scenarios measure what they claim to measure (no measurement, takes about a second)
dotnet run --project benchmark/Serilog.Sinks.TriggerBuffer.Benchmarks -c Release -- --verify

# Scenarios 0, A, B and C (BenchmarkDotNet, long: plan for an hour or more)
dotnet run --project benchmark/Serilog.Sinks.TriggerBuffer.Benchmarks -c Release -- --filter "*Baseline*" "*HotPath*" "*Contention*" "*TriggerBenchmarks*"

# Scenario D, slow target (10 seconds per run, 3 runs per delay by default)
dotnet run --project benchmark/Serilog.Sinks.TriggerBuffer.Benchmarks -c Release -- --slow-target
```

## Scenarios

Everything goes through the public API (`LoggerConfiguration` and `WriteTo.TriggerBuffer`). Every scenario is compared
with the same logger and a sink that does nothing, so the numbers show what the trigger buffer adds.

| # | Scenario | What it shows |
| --- | --- | --- |
| 0 | Baseline: one event with no sink, and with a no-op sink | What Serilog itself costs |
| A | Buffered hot path, one thread, buffer size 100 and 1000, events below the trigger level | Cost of buffering one event (idea 2) |
| B | The same with 1 and 8 threads, buffer size 1000 | Lock contention (ideas 1 and 2) |
| C | One trigger after 10, 100 or 1000 buffered events, target does nothing | Cost of buffering plus handover, without target latency |
| D | Four threads log below the trigger level while one thread triggers every second, against a target that needs 0, 50 µs or 1 ms per event | What a slow target costs the other threads (idea 1) |

A to C run for the context profiles `None`, `Small`, `Medium` and `Large`. D runs without context.

### Event

One message template with three properties (`Loaded {OrderId} for {Customer} in {Elapsed} ms`), no exception, fixed
values, the system time provider.

### Context profiles

The profiles mix integers, strings, byte arrays and dictionaries with fixed values. They are attached with
`Enrich.WithProperty`, so Serilog converts them for every event. That work is part of the baseline and is not charged
to the sink.

| Profile | Properties | Content |
| --- | --- | --- |
| `None` | 0 | |
| `Small` | 6 | 2 integers, 2 strings (about 20 characters), 1 byte array (16 bytes), 1 dictionary (3 entries) |
| `Medium` | 20 | 8 integers, 8 strings (20 to 80 characters), 2 byte arrays (16 and 256 bytes), 2 dictionaries (10 entries) |
| `Large` | 50 | 20 integers, 20 strings, 5 byte arrays (16 to 256 bytes), 5 dictionaries (20 entries) |

Serilog does not hand a byte array to the sink as an array: it arrives as a **hex string** of twice the length. The
profiles are checked against that form.

### Scenario D in detail

- The target waits by spinning on a stopwatch, because `Thread.Sleep` is too coarse on Windows for delays of 50 µs to
  1 ms. The waiting thread keeps a core busy. The run prints the core count and warns below six cores.
- The four logging threads work at a fixed rate (5000 events per second each) and the buffer holds 500 events, so
  one handover is 500 events plus the trigger.
- The latency of a call is measured **from the moment it was due**, not from the moment it started. A thread that was
  blocked for half a second records every call it missed with its real delay. Measured from the start of the call, a
  blocked thread would show only one slow call and p99 would look harmless.
- Percentiles come from a histogram and are rounded up to the bucket edge (at most 12.5 % too high). The maximum is
  exact.
- A short warm-up run is made first and discarded. The handover duration is the time of the logging call that carries
  the trigger.

## Measurement conditions

A result is only comparable under the same conditions. Every published result lists:

- date and commit
- CPU, number of physical and logical cores
- operating system, .NET version (the measurements run on .NET 10, the library targets `net8.0`) and
  BenchmarkDotNet version
- power plan and power source
- what else was running

The agreed conditions are: Release build, power plan "High performance", mains power, no other load (close the IDE,
browsers and chat clients, no virus scan running). For D, run three times and show the runs side by side.

## Thresholds and noise

Fixed before measuring, so that the numbers cannot be read to fit a wish afterwards.

- **Idea 2 (cheaper hot path)** is worth doing if a buffered event costs more than about 20 % over the baseline in the
  `Small` and `Medium` profiles, or if it allocates in a way that can be avoided.
- **Idea 1 (handover outside the lock)** is worth doing if throughput with several threads drops strongly because of
  the lock, or if in scenario D p99 and the maximum of the logging threads are clearly above the handover duration of
  the reference run.
- **Noise:** a difference counts only if it is larger than twice the reported error and at least 5 %. If the error on
  the hot path is larger than 5 %, the limit is set again after the first measurement and the change is written here.

For a before and after comparison of a later change, run both states on the same machine back to back.

## Results

No measurement under the agreed conditions yet.

## Decisions

| Idea | Decision | Based on |
| --- | --- | --- |
| 1, handover outside the lock | open | |
| 2, cheaper hot path | open | |

## Limits

- The numbers hold for the machine and conditions listed with them.
- Scenario D uses a simulated target that spins. A real file or HTTP target behaves differently.
- Memory held by the buffer is not measured. `maximumBufferCount` is not a memory limit (see the README).
- The sink allocates nothing per event: the allocated bytes per operation were identical with and without it in the
  trial run. Events that it keeps in the buffer live longer, though, so the garbage collector has more to promote
  (the trial showed many more generation 1 and 2 collections). Part of a measured difference can therefore be
  collection work and not the sink's own code. Keep this in mind when reading ratios for the larger profiles.
- Scenario B at `Large` with 8 threads allocates several gigabytes per run. Garbage collection can then dominate the
  result and hide the cost of the lock.
