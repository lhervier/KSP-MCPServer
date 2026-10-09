# TODO

What is left to do on KSP-MCPServer, most urgent first.

- **Check `drive_to` on a tall rover over rough ground**, on the Earth of Real Solar System near Kourou,
  where `Diag3-Rover` rolled over three times out of four drives (6 m/s once, 3 m/s twice). The control
  point of that rover faces up: the drives read its speed and heading along an axis that stood upright,
  so they never cut the throttle (seen on Kerbin: 28 m/s for 4 asked). They now drive along the axis the
  wheels drive along, choose forward or reverse again when the target falls behind, steer less above
  3 m/s, and stop when the rover tips over or runs away; on Kerbin, `Diag3-Rover` now reaches its target
  at 4, 6 and 10 m/s. Not yet driven near Kourou.
- **`set_position` over a long jump**: moved from the launchpad of the KSC to the other side of Kerbin
  (latitude 3.165°, longitude −141.128°), a two-part craft crashed through the terrain about a second
  after being unpacked, three times out of three, with and without Terrain Precision Fix. The ground of
  the arrival likely has no collider yet when physics resumes. Tried, and taken back: setting the vessel
  1,000 m above the point, waiting until a ray cast down meets terrain that stays put for a second, then
  setting it down (with or without the ease of the menu). It still went through the ground near Kourou in
  Real Solar System, and once set down it read a speed over the ground of 462 m/s, the speed of Earth's
  turning at the equator, while it stood still: the velocity the jump gives it looks wrong, not the
  ground. `FlightGlobals.SetVesselPosition` gives the vessel the velocity of the turning ground
  (`angularVelocity` × radius) after turning the rotating frame off (`PrepForOrbitSet`), and
  `PostOrbitSet` may turn it on again: measure the speed over the ground right after the jump, short and
  long.
  Seen again on Kerbin: `Diag3-Rover` (18 parts) jumped 121 km to the Desert Airfield, then to the flat
  ground 200 m north of it, crashed through the terrain two seconds after arriving, both times; a kerbal
  on EVA made the same jump unharmed. To bring a craft to a launch site, `launch_vessel` with `site` and
  `crew` does it.
- **EVA and EVA construction tools**, so that a protocol that places parts no longer needs a player's
  hands (anchors on a static, for Terrain Precision Fix): `eva` (a kerbal out of a crewed part, by
  `FlightEVA.spawnEVA`: public, simple); placing a part from a kerbal's inventory on the ground (the
  editor of EVA construction drops it through private methods driven by the mouse,
  `EVAConstructionModeEditor.DropAttachablePart` among them: the state the mouse sets up has to be set up
  by hand); taking a part off a vessel and attaching it to another (`PickupPart`, `CheckAttach`, which casts
  a ray under the cursor, `AttachPart`: the hardest, not sure to work cleanly).
- **`launch_vessel` with a site mover**: with KSCSwitcher sending the KSC to Kourou
  in Real Solar System, a craft launched by `launch_vessel` from the space centre appeared at Kourou with
  no building of the KSC around it; launched from the VAB by hand, the KSC stood there. KSCSwitcher moves
  the KSC only when a game is created at the space centre. Before its launch, the editor also writes the
  launch site it chose as the game's default (`EditorDriver.saveselectedLaunchSite`), which the launch
  dialog of the space centre does not. Not seen since: to play again with KSCSwitcher.
- **`launch_vessel` from flight with the Mun loaded**: once, KSP gave the launch up
  (`Cannot find a transform named 'Facility/LaunchPad_spawn'`) and went back to the space centre. The
  tool now fails as soon as KSP leaves for another scene, in a second; launched again from the orbit of
  the Mun, the craft reached the pad: what made KSP give up is not known.

- **Check the stall recovery of `drive_to`** on the lip of the runway deck: approaching a spot on the
  deck from the grass, the rover used to creep at its lowest speed against the lip until the 300 s limit.
  It now raises that speed after three seconds without progress; not yet seen on that lip.
- **Publish**: create the GitHub repository (`lhervier/KSP-MCPServer`, the address the README and KSP Diag -
  Terrain Height link to), and a first release with the DLL.
