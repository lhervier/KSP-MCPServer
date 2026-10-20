# TODO

What is left to do on KSP-MCPServer, most urgent first.

- **Check `drive_to` on a tall rover over rough ground**, on the Earth of Real Solar System near Kourou,
  where a rover of 18 parts rolled over three times out of four drives (6 m/s once, 3 m/s twice). The control
  point of that rover faces up: the drives read its speed and heading along an axis that stood upright,
  so they never cut the throttle (seen on Kerbin: 28 m/s for 4 asked). They now drive along the axis the
  wheels drive along, choose forward or reverse again when the target falls behind, and stop when the
  rover tips over or runs away; on Kerbin, that rover now reaches its target at 4, 6 and 10 m/s. Not yet
  driven near Kourou. Since then, the steering learns how sharply the rover turns, slows down for a sharp
  turn and never asks for more than a quarter of the local gravity sideways: on Kerbin, the small
  Diag2-Rover, which rolled over turning 90° at 15 m/s, now takes that turn on its wheels. To try with the
  rover of 18 parts, whose centre of mass stands higher.
- **`set_position` over a long jump, in Real Solar System and with a large rover**: the vessel crashed
  through the terrain after a long jump, on Kerbin and near Kourou in Real Solar System. Two causes, both
  fixed: the ground of the arrival had no collider yet when physics resumed, and the vessel was let go
  with the speed of the turning ground (54 m/s at 72° of latitude on Kerbin, 462 m/s at the equator of the
  Earth), where the scene turns with the body and the ground stands still in it. `set_position` now holds
  the vessel still until the quad under it is of the finest level and what lies under it reads the same for
  a second, then lowers it to a few centimetres and lets it go; a capsule on a tank jumped from the KSC to
  72° N unharmed. Still to try: near Kourou in Real Solar System, and the rover of 18 parts that crashed
  twice at the Desert Airfield.
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

- **Check the stall recovery of `drive_to`** on the lip of the runway deck: approaching a spot on the
  deck from the grass, the rover used to creep at its lowest speed against the lip until the 300 s limit.
  It now raises that speed after three seconds without progress; not yet seen on that lip.
- **Publish**: create the GitHub repository (`lhervier/KSP-MCPServer`, the address the README and the mods that
  offer it tools link to), and a first release with the DLL.

- **Let the agent drive a rover itself over rough ground** (Eve, the Mun with Parallax's rocks), choosing its
  own path to a given spot, instead of `drive_to`'s straight line. Today `drive` and `drive_to` hold the call
  until the rover stops, and see nothing ahead. Wanted:
  - **Start and return at once**: a drive that starts and answers straight away, with an id; a call that changes
    its heading and speed while it runs; `stop_drive` as today.
  - **Follow the drive by polling** (simpler than server-sent events, whose notifications an MCP client does
    not necessarily hand to the model): each poll answers the rover's state as a navball would tell it
    (heading, pitch, roll, speed, vertical speed, wheels touching the ground, slip), the distance and bearing to
    the target, the drive's state (driving, arrived, stalled, tipped over, stopped), the shape of the ground
    ahead (a grid of heights and slopes in the rover's frame, a few tens of metres deep, from the collider
    rather than the computed height, which differ between vertices), and the colliders ahead (scatter rocks,
    statics: position, size, height above the ground), the way Diag Colliders finds them.
  - **The server keeps the reflexes**: between two polls the agent is blind for several seconds, so the server
    holds the heading and speed asked, and keeps today's guards (tip-over, runaway, stall, turns no harder
    than keeps the rover on its wheels). A dead man's switch brakes when no poll or command comes for some
    seconds. A turn-by-turn mode is worth trying: the game paused while the agent decides, then run for a
    given time.
  - **Poll only when something may happen**: the agent sets when it polls next from what the last poll told it,
    so it does not read again and again a rover that just rolls on. Over flat ground with no collider in the
    next 500 m, it drives on and polls again only once the rover is 500 m further. For that, a poll should say
    how far the way ahead is clear (no slope above a limit, no collider), looking as far as the agent asks
    rather than a few tens of metres; and the agent should be able to wait by distance rather than by time
    ("answer when the rover has gone 500 m, or earlier if a guard fires or the drive ends"). The dead man's
    switch then follows the delay the agent announces, not a fixed few seconds.
