# What driving the game through this mod costs: the runs

The logs and CSVs this mod's performance figures are read from. What they say is in
[Performance](../docs/performance.md).

Measured with [KSPProfiler](https://github.com/KSPModdingLibs/KSPProfiler), **by the procedure written in
[Performance](../docs/performance.md#how-the-frames-were-timed)**: by hand with KSPProfiler 1.0.0, and by
[this script](https://github.com/lhervier/KSP-PQSBench/blob/main/perfs/automation/run-perfs.py)
through this mod with [a fork of KSPProfiler](https://github.com/lhervier/KSP-ExtMod-KSPProfiler) that
this mod can drive.

## The runs

KSP 1.12.5. `GameData` holding Harmony 2.2.1, ModuleManager 4.2.3, KSP Community Fixes 1.41.1 and
KSPProfiler with the KsmUI library it ships with, in every run, and nothing else:

- in the `hand` runs, KSPProfiler 1.0.0, and not this mod;
- in the `server` runs, this mod, with `screen_messages = false` in its `PluginData/settings.cfg`, and the
  KSPProfiler fork (version 1.0.0 plus this server's tools for its window's buttons).

A command pod on rails in a circular equatorial orbit 5 km over the Mun, from
[the save PQS Bench provides](https://github.com/lhervier/KSP-PQSBench/blob/main/perfs/ref-mune-5km.sfs),
timed from 30 s to 1 min 40 s of mission time, at ×1. In every run, KSP's window was in front of the other
windows, and the mouse pointer outside it.

### The machine

| | |
|---|---|
| processor | Intel Core i7-4790K, 4 cores, 8 logical |
| memory | 32 GB of DDR3 |
| graphics | NVIDIA GeForce GTX 1060 6 GB |
| system | Windows 10, *High performance* power plan |
| KSP | 1280×720 window, vertical sync off (`SYNC_VBL = 0`), no frame limit, terrain detail High, terrain scatter on at full density |

Figures from another machine, or another session of runs, are not comparable to these: the runs here are
only read against each other.

### The logs

Six runs, each in a fresh KSP, in one session, in this order: `server-1`, `hand-1`, `server-2`, `hand-2`,
`server-3`, `hand-3`. The frames captured are as the script recorded them for the `server` runs, and as
read off the profiler's window for the `hand` runs.

| configuration | run | frames captured | CSV | `KSP.log` |
|---|---|---|---|---|
| by script, through this mod | 1 | 7 319 | [csv](runs/mun-05km-server-1.csv) | [log](runs/mun-05km-server-1.log) |
| by hand, without this mod | 1 | about 7 000, not noted exactly | [csv](runs/mun-05km-hand-1.csv) | [log](runs/mun-05km-hand-1.log) |
| by script, through this mod | 2 | 7 199 | [csv](runs/mun-05km-server-2.csv) | [log](runs/mun-05km-server-2.log) |
| by hand, without this mod | 2 | 7 335 | [csv](runs/mun-05km-hand-2.csv) | [log](runs/mun-05km-hand-2.log) |
| by script, through this mod | 3 | 7 061 | [csv](runs/mun-05km-server-3.csv) | [log](runs/mun-05km-server-3.log) |
| by hand, without this mod | 3 | 7 374 | [csv](runs/mun-05km-hand-3.csv) | [log](runs/mun-05km-hand-3.log) |

Every frame count is under the profiler's 10 000 ceiling, so each capture ended on *Stop*.

### Telling the two configurations apart

**The profiler's CSV does not name the mods.** The `KSP.log` of the same run does:

- its `Mod DLLs found` list names `KSPMCPServer v0.0.0.0` in the `server` logs only;
- this mod's own lines appear in the `server` logs only: at startup, one
  `[KSP-MCPServer] Tool profiler_... from KSPProfiler` line for each of the fork's five tools, then
  `[KSP-MCPServer] Listening on http://127.0.0.1:8770/mcp/ with 35 tools`; at the end of the run,
  `[KSP-MCPServer] Quitting KSP, as asked`;
- both builds of KSPProfiler call themselves `KSPProfiler v1.0.0.0`. The second list of assemblies in the
  log, with their SHA256, tells them apart: KSPProfiler and the KsmUI library it ships with have one
  checksum in the `hand` logs, another in the `server` logs.

## The figures

In milliseconds per frame, except the frame rate, as read from the CSVs. The CSV repeats row names:
**Update** is the first `Update` row, the whole phase; **Update → Update** is the `Update` row right after
it, the `Update()` calls of every `MonoBehaviour`; **Update → Coroutines** is the first `Coroutines` row
after the first `Update` row.

| | server 1 | hand 1 | server 2 | hand 2 | server 3 | hand 3 |
|---|---|---|---|---|---|---|
| frames per second, mean | 104.3 | 103.3 | 102.5 | 104.5 | 100.6 | 105.1 |
| frame time, mean | 9.58 | 9.68 | 9.75 | 9.57 | 9.94 | 9.52 |
| frame time, worst 1 % | 34.72 | 35.06 | 34.94 | 34.83 | 35.27 | 34.79 |
| Update, mean | 2.34 | 2.35 | 2.37 | 2.35 | 2.38 | 2.34 |
| Update → Update, mean | 0.71 | 0.72 | 0.73 | 0.72 | 0.72 | 0.72 |
| Update → Coroutines, mean | 1.93 | 1.93 | 1.96 | 1.91 | 1.98 | 1.90 |
| Update → Coroutines, median | 1.32 | 1.33 | 1.36 | 1.32 | 1.36 | 1.31 |
| Update → Coroutines, worst 1 % | 24.61 | 24.85 | 24.78 | 24.60 | 24.82 | 24.69 |
| Cameras render, mean | 3.51 | 3.49 | 3.48 | 3.42 | 3.60 | 3.43 |
| VSync, mean | 0.06 | 0.06 | 0.06 | 0.06 | 0.06 | 0.06 |
| profiler overhead, mean | 0.45 | 0.44 | 0.45 | 0.44 | 0.46 | 0.44 |
