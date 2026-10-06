# TODO

What is left to do on KSP-MCPServer, most urgent first.

- **No way to stop `drive_to`**: once called, it steers the wheels every frame until it arrives or 300 s
  have passed, even after its caller gave up waiting; pausing the game only holds it. Add a `stop_drive`
  tool, and end any drive when the active vessel changes or the flight scene closes. A `stop_drive` needs
  the item below first.
- **One request at a time**: `McpHttpServer` serves its requests on a single thread, each to the end of
  its tool (`MainThread.RunAndWait`) before taking the next. While a `drive_to` runs, even `get_state`
  waits for it to finish (seen: about 210 s). Serve each request on its own thread, the tools still
  running on Unity's main thread.
- **`drive_to` on a tall rover over rough ground**: on the Earth of Real Solar System, near Kourou,
  `Diag3-Rover` rolled over three times out of four drives (6 m/s once, 3 m/s twice), turning hard or
  reversing over bumps. The direction (forward or reverse) is chosen once, from the heading at the start:
  a rover that turns round while reversing makes the drive stop, the target being then behind. Choose the
  direction again while driving, limit the steering with speed, and stop when the rover is no longer
  upright.
- **`set_position` over a long jump**: moved from the launchpad of the KSC to the other side of Kerbin
  (latitude 3.165°, longitude −141.128°), a two-part craft crashed through the terrain about a second
  after being unpacked, three times out of three, with and without Terrain Precision Fix. The ground of
  the arrival likely has no collider yet when physics resumes. Tried, and taken back: setting the vessel
  1,000 m above the point, waiting until a ray cast down meets terrain that stays put for a second, then
  setting it down (with or without the ease of the menu). It still went through the ground near Kourou in
  Real Solar System, and once set down it read a speed over the ground of 462 m/s, the speed of Earth's
  turning at the equator, while it stood still: the velocity the jump gives it looks wrong, not the
  ground.
- **`play_mission` resumes the mission's saved game** (`saves/missions/<mission>/persistent.sfs`) when
  there is one, instead of starting it again as its description says: see what *Restart* does in
  `MissionPlayDialog`.
- **`launch_vessel` with a site mover**: with KSCSwitcher sending the KSC to Kourou
  in Real Solar System, a craft launched by `launch_vessel` from the space centre appeared at Kourou with
  no building of the KSC around it; launched from the VAB by hand, the KSC stood there. Find what the
  editor's launch does that `FlightDriver.StartWithNewLaunch` alone does not.
- **`launch_vessel` waits three minutes** when KSP gives up a launch and goes back to the space centre
  (a launch site it cannot find): fail as soon as the scene goes back.

- **Check the stall recovery of `drive_to`** on the lip of the runway deck: approaching a spot on the
  deck from the grass, the rover used to creep at its lowest speed against the lip until the 300 s limit.
  It now raises that speed after three seconds without progress; not yet seen on that lip.
- **Publish**: create the GitHub repository (`lhervier/KSP-MCPServer`, the address the README and KSP Diag -
  Terrain Height link to), and a first release with the DLL.
