# Performance

Part of [KSP-MCPServer](../README.md). The short version is on the main page, under [Performance](../README.md#performance); here are the protocol and the figures.

Other mods publish frame timings taken with [KSPProfiler](https://github.com/KSPModdingLibs/KSPProfiler)
in runs played by a script through this server:
[Terrain Precision Fix](https://github.com/lhervier/KSP-TerrainPrecisionFix/blob/master/docs/performance.md)
and [Rock Precision Fix](https://github.com/lhervier/KSP-RockPrecisionFix/blob/master/docs/performance.md).
The question here is whether driving KSP through this mod changes what a frame costs. The same runs were
played both ways: by hand, without this mod, and by script, through it.

The answer: on the coroutines the terrain is updated in, the runs by script cost **at most about 0.05 ms
more per frame**, and no larger effect stands out of the spread between runs. Not nothing, but small.

## What the server does in a frame

While nobody calls it, this mod does three things, all visible in [`Src/`](../Src):

- **Once a frame, on the main thread**, the `Update()` of its `MonoBehaviour`
  ([`KSPMCPServerMod.cs`](../Src/KSPMCPServerMod.cs)) calls `Subscribe()`, which returns on a boolean
  test once the game events are subscribed, then `MainThread.Pump()`
  ([`MainThread.cs`](../Src/MainThread.cs)), which takes a lock on the queue of pending tool calls and
  returns when the queue is empty.
- **When two game events fire**, `onFloatingOriginShift` and `onFlightReady`, a handler counts the shifts
  of the floating origin or resets that count: a few assignments.
- **On a thread of its own**, the HTTP listener ([`McpHttpServer.cs`](../Src/McpHttpServer.cs)) waits
  in `HttpListener.GetContext()` for a request. While it waits, it takes nothing from the main thread.

A tool runs only while a request is being served: the listener queues it, `Pump()` starts it in the next
frame's `Update()` as a coroutine of the mod's `MonoBehaviour`, and the listener waits for it to end.
Between the start and the stop of the captures below, no tool runs: the script sleeps.

## The campaign

KSP 1.12.5 with Harmony 2.2.1, ModuleManager 4.2.3, KSP Community Fixes 1.41.1 and KSPProfiler, with the
KsmUI library it ships with, in every run, and nothing else. The two configurations:

- **by hand**: KSPProfiler 1.0.0, without this mod, its buttons pressed by hand;
- **by script**: this mod, and [a fork of KSPProfiler](https://github.com/lhervier/KSP-ExtMod-KSPProfiler)
  which only adds this server's tools for its window's buttons — nothing in what it measures changed —
  pressed by [`run-perfs.py`](https://github.com/lhervier/KSP-PQSBench/blob/master/perfs/automation/run-perfs.py),
  a Python script with no dependency. This server's on-screen messages are off
  (`screen_messages = false`).

The two sides differ in those two mods, the fork with the build of KsmUI it ships with, and in who plays
the steps; nothing else.

A command pod on rails in a circular equatorial orbit 5 km over the Mun, from
[the save PQS Bench provides](https://github.com/lhervier/KSP-PQSBench/blob/master/perfs/ref-mune-5km.sfs).
Every run times the same 70 seconds of that orbit, from 30 s of mission time. Terrain detail High,
terrain scatter on at full density.

Three runs per configuration, the two configurations taken in turn. **All of them were taken on my
desktop, in one session of runs**, described with the logs in [`perfs/`](../perfs/README.md); figures
from another machine, or another session of runs, are not comparable to these.

### How the frames were timed

The procedure of
[PQS Bench's runs](https://github.com/lhervier/KSP-PQSBench/blob/master/docs/measuring-a-terrain-mod.md#the-runs),
with KSPProfiler as the instrument. Each run is a fresh KSP:

1. Load the save. Right after the load, turn the camera to look ahead along the orbit, with the Mun's
   ground on the left two thirds of the screen: the framing of step 2 there, pictured. By hand, the camera
   is turned to that framing; the script sets it at heading 204° in the orbit's frame, pitch 0, which
   gives it.
2. Open the profiler's window, where it opens (the middle of the screen), KSP's interface shown.
   Captured frames at 10 000 (the most the window accepts; the profiler stops by itself when it is
   reached), *AutoCapture* off.
3. *Start capture* at 30 s of mission time, *Stop capture* at 1 min 40 s, at ×1 all along. Nothing is
   asked of the game in between: in the runs by script, the script sleeps through the window.
4. *Export to CSV*.

In every run, KSP's window was in front of the other windows, and the mouse pointer outside it.

A run is worth keeping when fewer than 10 000 frames were captured, so that the capture ended on *Stop*
and not before. All six did, with about 7 000 to 7 374 frames each.

## What a frame pays

The CSV repeats row names. Below, **Update** is the whole `Update` phase; **Update → Update** is the
`Update()` calls of every `MonoBehaviour`, this mod's among them; **Update → Coroutines** is the
coroutines run inside the `Update` phase, among them the terrain's update. In milliseconds per frame
(every row of every run is [with the runs](../perfs/README.md#the-figures)):

| | by script 1 | by hand 1 | by script 2 | by hand 2 | by script 3 | by hand 3 |
|---|---|---|---|---|---|---|
| frame time, mean | 9.58 | 9.68 | 9.75 | 9.57 | 9.94 | 9.52 |
| Update, mean | 2.34 | 2.35 | 2.37 | 2.35 | 2.38 | 2.34 |
| Update → Update, mean | 0.71 | 0.72 | 0.73 | 0.72 | 0.72 | 0.72 |
| Update → Coroutines, mean | 1.93 | 1.93 | 1.96 | 1.91 | 1.98 | 1.90 |
| Cameras render, mean | 3.51 | 3.49 | 3.48 | 3.42 | 3.60 | 3.43 |
| VSync, mean | 0.06 | 0.06 | 0.06 | 0.06 | 0.06 | 0.06 |
| profiler overhead, mean | 0.45 | 0.44 | 0.45 | 0.44 | 0.46 | 0.44 |

**VSync** near zero says the frame waited on the processor, not on the screen or the graphics card.
Averaged over the three runs of each configuration:

| | by hand | by script |
|---|---|---|
| frame time, mean | 9.590 | 9.757 |
| Update, mean | 2.347 | 2.363 |
| Update → Update, mean | 0.720 | 0.720 |
| Update → Coroutines, mean | 1.913 | 1.957 |
| Cameras render, mean | 3.447 | 3.530 |

On **frame time, Update, Update → Update and Cameras render**, the two sides overlap. The mean frame time is 0.17 ms higher by script on
average, but the three runs by script spread by 0.36 ms, twice that. **Update → Update**, where this
mod's `Update()` is counted, reads the same on both sides.

On **Update → Coroutines**, the runs by script read 1.93, 1.96 and 1.98 ms, the runs by hand 1.93, 1.91
and 1.90 ms. The runs by script average 0.04 ms higher, about 2 %, and none of them reads lower than a
run by hand: the lowest by script equals the highest by hand. That difference is the size of the spread
within each side, 0.03 ms by hand and 0.05 ms by script.

The profiler's own overhead reads 0.45 to 0.46 ms by script against 0.44 ms by hand: 0.01 to 0.02 ms more.

## What this says

**At most about 0.05 ms per frame**, on the coroutines the terrain is updated in; nothing larger stands
out of the spread between runs. That is not "no effect": in this session, the runs by script stayed on
the costlier side of that row.

What the server does in a frame when nobody calls it — a boolean test, and a lock taken on an empty queue
(see [What the server does in a frame](#what-the-server-does-in-a-frame)) — cannot account for 0.04 ms by
itself, and the row it is counted in reads the same on both sides. The cause of the difference is not
identified.

For a page that publishes runs played by script through this server: compare configurations played the
same way, all by script. Whatever the server costs is then paid by every configuration alike.
