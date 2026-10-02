# TODO

What is left to do on KSP-MCPServer, most urgent first.

- **Check the stall recovery of `drive_to`** on the lip of the runway deck: approaching a spot on the
  deck from the grass, the rover used to creep at its lowest speed against the lip until the 300 s limit.
  It now raises that speed after three seconds without progress; not yet seen on that lip.
- **Publish**: create the GitHub repository (`lhervier/KSP-MCPServer`, the address the README and KSP Diag -
  Terrain Height link to), and a first release with the DLL.
