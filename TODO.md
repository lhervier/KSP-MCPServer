# TODO

What is left to do on KSP-MCPServer, most urgent first.

- **`set_position` over a long jump**: moved from the launchpad of the KSC to the other side of Kerbin
  (latitude 3.165°, longitude −141.128°), a two-part craft crashed through the terrain about a second
  after being unpacked, three times out of three, with and without Terrain Precision Fix. The ground of
  the arrival likely has no collider yet when physics resumes: hold the vessel on rails until a ray cast
  down from it hits the terrain.
- **`play_mission` resumes the mission's saved game** (`saves/missions/<mission>/persistent.sfs`) when
  there is one, instead of starting it again as its description says: see what *Restart* does in
  `MissionPlayDialog`.
- **`launch_vessel` waits three minutes** when KSP gives up a launch and goes back to the space centre
  (a launch site it cannot find): fail as soon as the scene goes back.

- **Check the stall recovery of `drive_to`** on the lip of the runway deck: approaching a spot on the
  deck from the grass, the rover used to creep at its lowest speed against the lip until the 300 s limit.
  It now raises that speed after three seconds without progress; not yet seen on that lip.
- **Publish**: create the GitHub repository (`lhervier/KSP-MCPServer`, the address the README and KSP Diag -
  Terrain Height link to), and a first release with the DLL.
