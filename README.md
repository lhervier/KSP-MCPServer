# KSP-MCPServer

**⚠️ Work in progress.** The tools, their names and their arguments can still change.

A mod for KSP 1.12 that lets a program drive the game: read where the active vessel is, load a save,
drive a rover to a point, take a screenshot, press a button of another mod. It answers requests sent over
HTTP, on the computer KSP runs on only, in the format of the
[Model Context Protocol](https://modelcontextprotocol.io) (MCP), so that an AI assistant can use it as a
set of tools — but a ten-line Python script, or `curl`, can use it just as well, and no AI is needed.

It was written to play measuring protocols that are too tedious, or too imprecise, to play by hand: one of
them asks to park a rover on the same two spots again and again, before and after the game moves its
world, and this mod parks it there to within a centimetre
([KSP Diag - Terrain Height](https://github.com/lhervier/KSP-Diag-TerrainHeight/blob/master/docs/the-protocol-driving-runway.md)).

**How this was made.** Written with Claude, Anthropic's AI assistant, and reviewed by a human — me. I am
saying so up front, because contributions made with an AI deserve a closer look than others, and because
some people would rather stop reading here. What there is to check here is small: the source is eleven
short files, it opens no port beyond the loopback address, and it changes nothing in the game until a
request asks it to.

## What it offers

Every tool runs on the game's main thread, one at a time, and answers once it is done: `drive_to` answers
when the rover is parked, `load_save` when the vessel is in flight and physics runs on it.

| tool | what it does |
|---|---|
| `get_state` | the scene, the pause, the time, and the active vessel: situation, latitude, longitude, altitude, height above the terrain, speed, heading, brakes, SAS |
| `get_floating_origin` | the floating origin KSP keeps the world centred on: the vessel's distance from it, where it lies from the vessel (north, east, up), the distance at which KSP moves it, the moves since the scene opened |
| `open_game` | opens a game from any scene, the main menu included, as *Resume Game* does: in the scene it was saved in |
| `load_save` | loads a save into flight, from any scene |
| `save_game` | saves the game as it is now under a name, as a quicksave does |
| `launch_vessel` | launches a vessel from its `.craft` file at a launch site, with the crew the editor would give it, as the editor's Launch button does |
| `list_vessels` | the vessels of the game: id, name, situation, loaded or not, packed or not, active or target, distance from the active vessel |
| `switch_vessel` | makes a loaded vessel the active one, as the switch vessel keys `[` and `]` do |
| `set_target` | sets the active vessel's target to another vessel, or clears it |
| `screenshot` | captures the screen, interface included; returns the image and saves it as a PNG |
| `set_camera` | the flight camera's distance, heading and pitch |
| `set_pause` | pauses or resumes the flight |
| `wait` | lets the game run for a number of seconds |
| `set_cheats` | turns cheats of the `Alt+F12` menu on or off: *Infinite Electricity* and *Infinite Fuel* |
| `set_position` | moves the active vessel just above a point of a body, as *Set Position* of the `Alt+F12` menu does, pitch included, and answers once it has settled on the ground; refuses a point where another vessel lies |
| `get_terrain` | the terrain of a body at a point: its height, whether the sea covers it, its slope and roughness |
| `set_orbit` | puts the active vessel on an orbit, as *Set Orbit* of the `Alt+F12` menu does |
| `revert_to_launch` | reverts the flight to its launch, as *Revert to Launch* does |
| `go_to_scene` | leaves for the space centre or the tracking station, saving the game first as the game's own buttons do |
| `quit_game` | quits KSP, a second after answering |
| `set_controls` | holds the wheel throttle and steering, and sets the brakes |
| `drive` | drives a rover along a heading, at a speed, for a distance or until the floating origin moves; slows down before the end and stops with the brakes on |
| `drive_to` | drives a rover to a latitude and longitude, forward or in reverse, and stops there with the brakes on, still |
| `get_member`, `set_member` | reads or writes a field or property of a loaded object of any mod — a window's position, say |
| `call_method` | calls a method without arguments of a loaded object of any mod |

Other mods can add their own tools, without depending on this one: see
[Adding tools from another mod](#adding-tools-from-another-mod).

## Install

Copy `GameData/KSPMCPServer/` into the `GameData` folder of KSP. When KSP starts, its log reads
`[KSP-MCPServer] Listening on http://127.0.0.1:8770/mcp/`.

The port is in `GameData/KSPMCPServer/PluginData/settings.cfg`, read when KSP starts.

It listens on `127.0.0.1` only: nothing outside the computer KSP runs on can reach it.

## Using it

**With an AI assistant.** Register it as an MCP server over HTTP, KSP running. With Claude Code:

```
claude mcp add --transport http ksp http://127.0.0.1:8770/mcp/
```

**With anything else.** Each tool is a JSON-RPC call, posted to the same address:

```
curl -X POST http://127.0.0.1:8770/mcp/ -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"get_state","arguments":{}}}'
```

`{"method":"tools/list"}` lists the tools with the schema of their arguments. A complete example, a
Python script with no dependency that plays a whole protocol, is
[`run-driving-runway.py`](https://github.com/lhervier/KSP-Diag-TerrainHeight/blob/master/diag/automation/run-driving-runway.py)
in KSP Diag - Terrain Height.

## Adding tools from another mod

A mod references `KSPMCPServer.dll` (without copying it: `<Private>false</Private>`) and puts
`com.github.lhervier.ksp.mcpserver.McpToolAttribute` on the methods it offers. This server looks for that
attribute when KSP starts, in every loaded assembly, and publishes each method as a tool. Nothing reads
the attribute when this server is not installed, so the mod runs the same without it: the reference is
only needed to compile.

```csharp
using com.github.lhervier.ksp.mcpserver;

internal static class McpTools
{
    [McpTool("my_mod_record", "Freezes the reading in progress into the table, as the Record button does.")]
    internal static object Record() { return Object.FindObjectOfType<MyMod>().Record(); }
}
```

The attribute is recognised by its name, `McpToolAttribute`: a mod that would rather not reference this
one at all can declare its own attribute class of that name, with `Name` and `Description` properties.

- The method may be static or not, public or not. An instance method is called on the loaded object of
  its type, which has to be a Unity object — typically the mod's `MonoBehaviour`.
- Its parameters become the tool's arguments, by name: numbers, strings, booleans.
- What it returns is written as JSON: numbers, strings, booleans, lists, dictionaries, and the public
  fields of any other object. A `void` method answers `done`.

KSP Diag - Terrain Height and Diag FloatingOrigin offer their Record and Clear buttons this way, the reading of
their table, and the moving of their window.

## Build

Clone this repository, set `KSPDIR` to your KSP install folder and run `build.bat`. It needs the .NET
SDK, reads the KSP assemblies from your install, and puts the DLL in `GameData/KSPMCPServer/` inside the
repository. It does not install anything.

## License

MIT — see [LICENSE](LICENSE).
