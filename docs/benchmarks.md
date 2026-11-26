# Benchmarks

These are **measurements, not a promise**. They show what the sink cost on one machine under the conditions listed
with each result. The README makes no performance guarantee, and this document does not change that.

> **Status (2026-11-23):** the benchmark project is complete and verified, and a first full measurement
> exists (see "Results"). Both ideas of issue #3 reach their thresholds, but neither is decided yet. The
> measurement ran without Docker containers, which deviates from the agreed conditions.

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

A and B also contain a **control**, `RetainOnly`: a sink that only keeps the last events in a ring (the size of the
buffer) and does nothing else, with no lock, timestamp, expiry check or queue. Any buffer has at least this cost.
Compared with the no-op sink it shows the price of keeping events alive, which is mostly garbage collection. Compared
with the trigger buffer it shows what the sink's own logic (lock, timestamp, expiry, queue) adds. The control was
added after the first measurement below, so that measurement does not contain it yet.

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

The measurements are meant to show what a developer sees on an ordinary machine, not the best case. Developers
usually start the application locally, often in their own Docker containers, with an IDE, a browser and a chat client
open. The agreed conditions are therefore: Release build, mains power, the default power plan ("Balanced") and a
typical developer workload running (IDE, browser, chat client, and the containers that are usually running).

The load cannot be reproduced exactly. So every result states what was running (applications, containers) and roughly
how busy the CPU was before the run. Do not close applications to get cleaner numbers, and do not change the power
plan. Use the default BenchmarkDotNet job, not `--job short`. For D, run three times and show the runs side by side.
Scenario D needs about six free cores, so check the CPU load before starting it.

A quiet machine can be used as an additional reference, but it is labeled as such and is not compared with the other
results.

## Thresholds and noise

Fixed before measuring, so that the numbers cannot be read to fit a wish afterwards.

- **Idea 2 (cheaper hot path)** is worth doing if a buffered event costs more than about 20 % over the baseline in the
  `Small` and `Medium` profiles, or if it allocates in a way that can be avoided.
- **Idea 1 (handover outside the lock)** is worth doing if throughput with several threads drops strongly because of
  the lock, or if in scenario D p99 and the maximum of the logging threads are clearly above the handover duration of
  the reference run.
- **Noise:** a difference counts only if it is larger than twice the reported error and at least 5 %. If the error on
  the hot path is larger than 5 %, the limit is set again after the first measurement and the change is written here.
Under a realistic workload the error will often be larger, so expect to repeat runs or raise the iteration count
before a difference passes this rule. Do not relax the rule just to get a result.

For a before and after comparison of a later change, run both states on the same machine back to back, with the same
workload.

## Results

First full measurement, 2026-11-23, 13:55 to 14:54. The measurement and the project are described above. The
tables are copied from the BenchmarkDotNet reports and from the output of `--slow-target`.

### Conditions

- Commit `904e4c2`, branch `feature/benchmarks`, Release build
- CPU Intel Core i7-10850H, 6 physical and 12 logical cores
- Windows 11 Pro 10.0.26300, .NET 10.0.14 (the library targets `net8.0`), BenchmarkDotNet 0.15.8, default job
- Power plan "Balanced", mains power (battery at 100 %)
- Before the start: CPU load 14 %, 13.2 GB of 31.6 GB memory free
- Running: Vivaldi (about 4.6 GB), VS Code, Discord, Slack, Signal, Obsidian, Cisco Collab, a security agent and
  other background applications
- **Docker was not running**: the daemon was not started, so there were no containers. The agreed conditions name
  containers as part of the usual workload, so this deviates from them.
- Use of the machine during the run was not controlled.

### Scenario 0: baseline

| Method | Mean | Error | StdDev | Allocated |
| --- | --- | --- | --- | --- |
| NoSink | 252.6 ns | 4.67 ns | 8.18 ns | 488 B |
| NullSinkOnly | 256.1 ns | 5.06 ns | 9.51 ns | 488 B |

### Scenario A: hot path, one thread

| Method | BufferSize | Profile | Mean | Error | Ratio | Gen1 | Gen2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| NullSinkOnly | 100 | None | 266.4 ns | 5.27 ns | 1.00 | - | - | 488 B |
| Buffered | 100 | None | 309.8 ns | 6.19 ns | 1.16 | 0.0110 | - | 488 B |
| NullSinkOnly | 100 | Small | 1,116.8 ns | 22.21 ns | 1.00 | 0.0019 | - | 2136 B |
| Buffered | 100 | Small | 1,233.7 ns | 24.66 ns | 1.11 | 0.1678 | - | 2136 B |
| NullSinkOnly | 100 | Medium | 3,485.9 ns | 113.48 ns | 1.01 | 0.0229 | - | 7920 B |
| Buffered | 100 | Medium | 4,794.9 ns | 89.49 ns | 1.39 | 1.2512 | - | 7920 B |
| NullSinkOnly | 100 | Large | 12,659.1 ns | 157.72 ns | 1.00 | 0.2441 | - | 27521 B |
| Buffered | 100 | Large | 23,956.3 ns | 357.42 ns | 1.89 | 4.3335 | - | 27521 B |
| NullSinkOnly | 1000 | None | 267.9 ns | 5.15 ns | 1.00 | - | - | 488 B |
| Buffered | 1000 | None | 347.0 ns | 6.86 ns | 1.30 | 0.0772 | - | 488 B |
| NullSinkOnly | 1000 | Small | 1,241.4 ns | 24.85 ns | 1.00 | 0.0019 | - | 2136 B |
| Buffered | 1000 | Small | 2,040.4 ns | 40.54 ns | 1.65 | 0.3357 | - | 2136 B |
| NullSinkOnly | 1000 | Medium | 3,589.7 ns | 70.73 ns | 1.00 | 0.0229 | - | 7920 B |
| Buffered | 1000 | Medium | 13,742.6 ns | 235.02 ns | 3.84 | 1.2512 | 1.1597 | 7921 B |
| NullSinkOnly | 1000 | Large | 12,404.9 ns | 247.44 ns | 1.00 | 0.2441 | - | 27521 B |
| Buffered | 1000 | Large | 66,391.0 ns | 1,326.05 ns | 5.35 | 2.9297 | 0.8545 | 27521 B |

### Scenario C: one trigger

The reference is the same logger without the trigger buffer, so the difference is buffering plus handover.

| Method | BufferedEvents | Profile | Mean | Error | Ratio | Gen1 | Gen2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| PassThrough | 10 | None | 3.001 µs | 0.0554 µs | 1.00 | - | - | 5.24 KB |
| BufferThenTrigger | 10 | None | 3.542 µs | 0.0677 µs | 1.18 | 0.0038 | - | 5.24 KB |
| PassThrough | 10 | Small | 15.468 µs | 0.6613 µs | 1.01 | 0.0153 | - | 22.95 KB |
| BufferThenTrigger | 10 | Small | 15.321 µs | 0.3052 µs | 1.00 | 0.1068 | - | 22.95 KB |
| PassThrough | 10 | Medium | 40.522 µs | 0.9642 µs | 1.00 | 0.2441 | - | 85.08 KB |
| BufferThenTrigger | 10 | Medium | 36.853 µs | 0.6744 µs | 0.91 | 1.5259 | - | 85.08 KB |
| PassThrough | 10 | Large | 136.689 µs | 3.8419 µs | 1.01 | 2.8076 | - | 295.63 KB |
| BufferThenTrigger | 10 | Large | 154.385 µs | 3.1259 µs | 1.14 | 12.9395 | - | 295.63 KB |
| PassThrough | 100 | None | 26.855 µs | 0.4807 µs | 1.00 | - | - | 48.13 KB |
| BufferThenTrigger | 100 | None | 32.168 µs | 0.4565 µs | 1.20 | 0.5493 | - | 48.13 KB |
| PassThrough | 100 | Small | 121.251 µs | 2.2948 µs | 1.00 | - | - | 210.69 KB |
| BufferThenTrigger | 100 | Small | 130.546 µs | 2.5295 µs | 1.08 | 3.4180 | - | 210.69 KB |
| PassThrough | 100 | Medium | 367.532 µs | 7.3194 µs | 1.00 | 2.4414 | - | 781.19 KB |
| BufferThenTrigger | 100 | Medium | 407.730 µs | 6.5392 µs | 1.11 | 73.7305 | - | 781.19 KB |
| PassThrough | 100 | Large | 1,241.948 µs | 24.1752 µs | 1.00 | 25.3906 | - | 2714.45 KB |
| BufferThenTrigger | 100 | Large | 1,754.253 µs | 32.2570 µs | 1.41 | 367.1875 | - | 2714.45 KB |
| PassThrough | 1000 | None | 274.638 µs | 4.5662 µs | 1.00 | - | - | 477.04 KB |
| BufferThenTrigger | 1000 | None | 333.992 µs | 4.4829 µs | 1.22 | 17.0898 | - | 477.04 KB |
| PassThrough | 1000 | Small | 1,126.291 µs | 18.9705 µs | 1.00 | 1.9531 | - | 2088.08 KB |
| BufferThenTrigger | 1000 | Small | 1,521.002 µs | 23.4883 µs | 1.35 | 279.2969 | - | 2088.08 KB |
| PassThrough | 1000 | Medium | 3,473.929 µs | 69.1254 µs | 1.00 | 23.4375 | - | 7742.32 KB |
| BufferThenTrigger | 1000 | Medium | 8,185.996 µs | 149.8178 µs | 2.36 | 1046.8750 | 187.5000 | 7742.38 KB |
| PassThrough | 1000 | Large | 12,313.470 µs | 204.8409 µs | 1.00 | 250.0000 | - | 26902.6 KB |
| BufferThenTrigger | 1000 | Large | 49,833.819 µs | 963.7948 µs | 4.05 | 2818.1818 | 1181.8182 | 26903.64 KB |

### Scenario B: several threads

One invocation is `Threads` times 20 000 events.

| Method | Threads | Profile | Mean | Error | Ratio | Gen1 | Gen2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| NullSinkOnly | 1 | None | 5.723 ms | 0.1127 ms | 1.00 | - | - | 9.31 MB |
| Buffered | 1 | None | 7.600 ms | 0.1487 ms | 1.33 | 1546.8750 | - | 9.31 MB |
| NullSinkOnly | 1 | Small | 22.938 ms | 0.3581 ms | 1.00 | 31.2500 | - | 40.74 MB |
| Buffered | 1 | Small | 37.563 ms | 0.7106 ms | 1.64 | 6714.2857 | - | 40.74 MB |
| NullSinkOnly | 1 | Medium | 71.327 ms | 1.3070 ms | 1.00 | 428.5714 | - | 151.07 MB |
| Buffered | 1 | Medium | 291.578 ms | 5.8072 ms | 4.09 | 25000.0000 | 19000.0000 | 151.07 MB |
| NullSinkOnly | 1 | Large | 256.502 ms | 5.0733 ms | 1.00 | 5000.0000 | - | 524.92 MB |
| Buffered | 1 | Large | 1,373.774 ms | 26.7524 ms | 5.36 | 63000.0000 | 20000.0000 | 524.93 MB |
| NullSinkOnly | 8 | None | 13.446 ms | 0.2592 ms | 1.00 | 109.3750 | - | 74.47 MB |
| Buffered | 8 | None | 49.847 ms | 0.9830 ms | 3.71 | 12363.6364 | - | 74.47 MB |
| NullSinkOnly | 8 | Small | 58.845 ms | 1.2509 ms | 1.00 | 2000.0000 | - | 325.94 MB |
| Buffered | 8 | Small | 187.131 ms | 6.8592 ms | 3.19 | 54000.0000 | - | 325.95 MB |
| NullSinkOnly | 8 | Medium | 189.866 ms | 7.1039 ms | 1.01 | 29333.3333 | - | 1208.55 MB |
| Buffered | 8 | Medium | 1,531.907 ms | 29.3905 ms | 8.16 | 140000.0000 | 134000.0000 | 1208.64 MB |
| NullSinkOnly | 8 | Large | 675.770 ms | 13.3753 ms | 1.00 | 241000.0000 | - | 4199.4 MB |
| Buffered | 8 | Large | 10,831.584 ms | 352.4064 ms | 16.06 | 444000.0000 | 80000.0000 | 4199.76 MB |

Events per second, calculated from the means above, and how much 8 threads gain over 1 thread:

| Profile | Reference, 1 thread | Reference, 8 threads | Gain | Buffered, 1 thread | Buffered, 8 threads | Gain |
| --- | --- | --- | --- | --- | --- | --- |
| None | 3,494,671 | 11,899,450 | 3.4 x | 2,631,579 | 3,209,822 | 1.2 x |
| Small | 871,916 | 2,719,008 | 3.1 x | 532,439 | 855,016 | 1.6 x |
| Medium | 280,399 | 842,700 | 3.0 x | 68,592 | 104,445 | 1.5 x |
| Large | 77,972 | 236,767 | 3.0 x | 14,558 | 14,772 | 1.0 x |

### Scenario D: slow target

Four logging threads, one trigger per second, 10 triggers per run, 500 buffered events per handover. Times in
microseconds. The reference has a target without delay.

| Target delay | Run | Events | Triggers | Handover avg | Handover max | Log p50 | Log p99 | Log p99.9 | Log max |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 0 (reference) | 1 | 200000 | 10 | 19.0 | 30.7 | 1.2 | 327.7 | 15728.6 | 45578.8 |
| 0 (reference) | 2 | 200000 | 10 | 18.5 | 22.9 | 1.2 | 24.6 | 1179.6 | 11754.6 |
| 0 (reference) | 3 | 200000 | 10 | 69.5 | 529.9 | 1.2 | 30.7 | 983.0 | 9631.6 |
| 50 µs | 1 | 200000 | 10 | 25432.6 | 25796.7 | 1.3 | 16777.2 | 25165.8 | 33489.9 |
| 50 µs | 2 | 200000 | 10 | 25294.0 | 25804.3 | 1.2 | 15728.6 | 25165.8 | 25657.5 |
| 50 µs | 3 | 200000 | 10 | 25251.5 | 25467.5 | 1.2 | 15728.6 | 25165.8 | 25239.1 |
| 1000 µs | 1 | 200000 | 10 | 502001.3 | 504221.1 | 4718.6 | 503316.5 | 503316.5 | 504111.4 |
| 1000 µs | 2 | 200000 | 10 | 504515.4 | 512003.4 | 8388.6 | 503316.5 | 511858.5 | 511858.5 |
| 1000 µs | 3 | 200000 | 10 | 502009.8 | 504434.7 | 5242.9 | 503316.5 | 503316.5 | 504295.8 |

### Against the thresholds

**Noise.** The largest reported error in scenarios 0 to C is 4.3 % of the mean, so the 5 % limit stays as it is.

**Idea 2, a cheaper hot path.** Overhead of a buffered event over the reference, one thread (scenario A):

| Profile | Buffer 100 | Buffer 1000 |
| --- | --- | --- |
| `None` | +16 % (43 ns) | +30 % (79 ns) |
| `Small` | +11 % | +65 % |
| `Medium` | +39 % | +284 % |
| `Large` | +89 % | +435 % |

The 20 % threshold is exceeded for `Small` and `Medium` in three of four cells (not for `Small` with buffer 100). The
allocated bytes per event are identical with and without the sink, so there is no avoidable allocation. The `Gen1`
column shows what changes: with the buffer, almost every object that survives a generation 0 collection is promoted
to generation 1, because the buffer holds it. Without the buffer almost none is. The overhead grows with the buffer
size and with the size of the event, which fits the cost of the garbage collector for retained objects better than a
fixed cost in the sink's own code. This is **not proven**. It needs a control that only retains the last N events
without any other logic. Until then the threshold is met, but a cheaper hot path may not lower the numbers.

**Idea 1, handover outside the lock.**

- Scenario D: with a target delay of 50 µs, p99 of the logging calls is 15.7 to 16.8 ms. With 1 ms it is 503 ms. The
  reference has 25 to 328 µs. The handover takes 25 ms and 502 ms (500 events times the delay), the reference about
  19 µs. The other threads wait about as long as the handover lasts, so the criterion is met. In the reference runs the
  maximum is 9.6 to 45.6 ms, which is scheduling noise on a loaded machine. The maximum at 50 µs (25 to 33 ms) cannot
  be told from that. p99 can.
- Scenario B: with 8 threads the buffered logger reaches 1.0 to 1.6 times the throughput of one thread. The
  reference reaches 3.0 to 3.4 times. It does not scale with the number of threads. Part of this is garbage
  collection and cannot be separated from the lock with this measurement.
- Handing over outside the lock would address scenario D. It would not address the contention on intake that
  scenario B points to.

## Decisions

| Idea | Decision | Based on |
| --- | --- | --- |
| 1, handover outside the lock | open, threshold met | Scenario D: the logging threads wait about as long as the handover lasts (p99 16 ms at 50 µs, 503 ms at 1 ms). Scenario B: no scaling with threads, partly garbage collection |
| 2, cheaper hot path | open, threshold met by ratio | Scenario A: +39 % to +284 % for `Medium` and +65 % for `Small` with buffer 1000. The cause is probably retention, not the sink's code. A control run that only retains events is needed before changing code |

## Limits

- The numbers hold for the machine and conditions listed with them.
- Scenario D uses a simulated target that spins. A real file or HTTP target behaves differently.
- Memory held by the buffer is not measured. `maximumBufferCount` is not a memory limit (see the README).
- The sink allocates nothing per event: the allocated bytes per operation were identical with and without it in the
  measurement. Events that it keeps in the buffer live longer, though, so the garbage collector has more to promote
  (the measurement showed many more generation 1 and 2 collections). Part of a measured difference can therefore be
  collection work and not the sink's own code. Keep this in mind when reading ratios for the larger profiles.
- Scenario B at `Large` with 8 threads allocates several gigabytes per run. Garbage collection can then dominate the
  result and hide the cost of the lock.
