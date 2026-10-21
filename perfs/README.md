# What this mod costs a frame: the runs

The save, the script, the logs and CSVs this mod's performance figures are read from. What they say is in
[Performance](../docs/performance.md); every run follows [its run](../docs/performance.md#the-run), timed
with [KSPProfiler](https://github.com/KSPModdingLibs/KSPProfiler).

- [`ref-mune-5km.sfs`](ref-mune-5km.sfs): the save every run is flown from, a Mk1 command pod in a
  circular equatorial orbit 5 km over the Mun.
- [`automation/run-perfs.py`](automation/run-perfs.py): the script of the runs by script, Python 3 and
  nothing else.

## The machine

| | |
|---|---|
| processor | Intel Core i7-4790K, 4 cores, 8 logical |
| memory | 32 GB of DDR3 |
| graphics | NVIDIA GeForce GTX 1060 6 GB |
| system | Windows 10, *High performance* power plan |
| KSP | 1280×720 window, vertical sync off (`SYNC_VBL = 0`), no frame limit, terrain detail High, terrain scatter on at full density |

Figures from another machine, or another session of runs, are not comparable to these: the runs of a
campaign are only read against each other.

## Installed, and nobody calls it

KSP 1.12.5. `GameData` holding Harmony 2.2.1, ModuleManager 4.2.3, KSP Community Fixes 1.41.1 and
KSPProfiler 1.0.0 with the KsmUI library it ships with, in every run, and nothing else:

- in the `without` runs, not this mod;
- in the `idle` runs, this mod, its `PluginData/settings.cfg` as shipped. No program sent it a request.

Every run played by hand.

### The logs, installed and idle

Six runs, each in a fresh KSP, in one session, in this order: `without-1`, `idle-1`, `without-2`,
`idle-2`, `without-3`, `idle-3`. The frames captured are as read off the profiler's window.

| configuration | run | frames captured | CSV | `KSP.log` |
|---|---|---|---|---|
| without this mod | 1 | 7 087 | [csv](runs/mun-05km-without-1.csv) | [log](runs/mun-05km-without-1.log) |
| this mod, idle | 1 | 7 326 | [csv](runs/mun-05km-idle-1.csv) | [log](runs/mun-05km-idle-1.log) |
| without this mod | 2 | 7 107 | [csv](runs/mun-05km-without-2.csv) | [log](runs/mun-05km-without-2.log) |
| this mod, idle | 2 | 6 984 | [csv](runs/mun-05km-idle-2.csv) | [log](runs/mun-05km-idle-2.log) |
| without this mod | 3 | 7 245 | [csv](runs/mun-05km-without-3.csv) | [log](runs/mun-05km-without-3.log) |
| this mod, idle | 3 | 7 096 | [csv](runs/mun-05km-idle-3.csv) | [log](runs/mun-05km-idle-3.log) |

Every frame count is under the profiler's 10 000 ceiling, so each capture ended on *Stop*.

### Telling the two configurations apart, installed and idle

*The log lines quoted on this page come from runs of an earlier version of this mod, when its log prefix was `[KSP-MCPServer]`; it is now `[KSPUtilMCPServer]`.*

- The `Mod DLLs found` list of the log names `KSPUtilMCPServer v0.0.0.0` in the `idle` logs only.
- This mod writes a single line in the `idle` logs, at startup:
  `[KSP-MCPServer] Listening on http://127.0.0.1:8770/mcp/ with 43 tools`, and nothing after it: no tool
  was called. The `without` logs hold no `[KSP-MCPServer]` line.
- KSPProfiler and the KsmUI library it ships with have the same checksum in all six logs, in the second
  list of assemblies: the same build on both sides.

### The figures, installed and idle

In milliseconds per frame, except the frame rate, as read from the CSVs (rows as said in
[Reading the CSV](../docs/performance.md#the-run)).

| | without 1 | idle 1 | without 2 | idle 2 | without 3 | idle 3 |
|---|---|---|---|---|---|---|
| frames per second, mean | 101.4 | 104.3 | 101.3 | 99.6 | 103.3 | 101.4 |
| frame time, mean | 9.87 | 9.59 | 9.87 | 10.04 | 9.69 | 9.87 |
| frame time, worst 1 % | 35.26 | 35.18 | 35.65 | 35.70 | 35.37 | 35.29 |
| Update, mean | 2.50 | 2.36 | 2.49 | 2.52 | 2.43 | 2.46 |
| Update → Update, mean | 0.85 | 0.72 | 0.82 | 0.82 | 0.79 | 0.81 |
| Update → Coroutines, mean | 1.96 | 1.92 | 1.96 | 1.99 | 1.94 | 1.95 |
| Update → Coroutines, median | 1.30 | 1.31 | 1.30 | 1.33 | 1.30 | 1.29 |
| Update → Coroutines, worst 1 % | 24.85 | 25.03 | 25.28 | 25.05 | 25.00 | 25.00 |
| Cameras render, mean | 3.48 | 3.43 | 3.50 | 3.52 | 3.45 | 3.52 |
| VSync, mean | 0.06 | 0.06 | 0.06 | 0.06 | 0.06 | 0.06 |
| profiler overhead, mean | 0.43 | 0.45 | 0.44 | 0.44 | 0.44 | 0.45 |

## Driven by script

KSP 1.12.5. `GameData` holding Harmony 2.2.1, ModuleManager 4.2.3, KSP Community Fixes 1.41.1 and
KSPProfiler with the KsmUI library it ships with, in every run, and nothing else:

- in the `hand` runs, KSPProfiler 1.0.0, and not this mod, played by hand;
- in the `server` runs, this mod, with `screen_messages = false` in its `PluginData/settings.cfg`, and
  [a fork of KSPProfiler](https://github.com/lhervier/KSP-ExtMod-KSPProfiler) (version 1.0.0 plus this
  server's tools for its window's buttons), played by [`run-perfs.py`](automation/run-perfs.py).

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

- its `Mod DLLs found` list names `KSPUtilMCPServer v0.0.0.0` in the `server` logs only;
- this mod's own lines appear in the `server` logs only: at startup, one
  `[KSP-MCPServer] Tool profiler_... from KSPProfiler` line for each of the fork's five tools, then
  `[KSP-MCPServer] Listening on http://127.0.0.1:8770/mcp/ with 35 tools`; at the end of the run,
  `[KSP-MCPServer] Quitting KSP, as asked`;
- both builds of KSPProfiler call themselves `KSPProfiler v1.0.0.0`. The second list of assemblies in the
  log, with their SHA256, tells them apart: KSPProfiler and the KsmUI library it ships with have one
  checksum in the `hand` logs, another in the `server` logs.

### The figures, driven by script

In milliseconds per frame, except the frame rate, as read from the CSVs (rows as said in
[Reading the CSV](../docs/performance.md#the-run)).

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
