using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>Tools that read the game, load a save, move the camera, and take screenshots.</summary>
    internal static class GameTools
    {
        private static int _shifts;
        private static double _lastShift = double.NaN;

        public static IEnumerable<Tool> All()
        {
            yield return new Tool("get_state",
                "Reads the game: scene, pause, universal time, and for the active vessel its name, situation, " +
                "latitude, longitude, altitude, height above the terrain, surface speed, heading (degrees from " +
                "north, towards east), brakes and SAS.",
                Schema.Object(), GetState);
            yield return new Tool("get_floating_origin",
                "Reads KSP's floating origin, the point the world is kept centred on: the active vessel's " +
                "distance from it, where it lies from the vessel (metres north, east and up), the distance at " +
                "which KSP moves it back onto the vessel, and the shifts counted since the flight scene opened " +
                "with the length of the last one.",
                Schema.Object(), GetFloatingOrigin);
            yield return new Tool("load_save",
                "Loads a save into flight, from any scene, and waits until the active vessel is unpacked and " +
                "physics runs on it.",
                Schema.Object(
                    Schema.P("folder", "string", "the game's folder under saves/", true),
                    Schema.P("save", "string", "the save's name, without .sfs", true)),
                LoadSave);
            yield return new Tool("open_game",
                "Opens a game from any scene, the main menu included, as Resume Game does: in the scene it was " +
                "saved in, the space centre for its persistent save. Waits until that scene is up.",
                Schema.Object(
                    Schema.P("folder", "string", "the game's folder under saves/", true),
                    Schema.P("save", "string", "the save's name, without .sfs (default persistent)")),
                OpenGame);
            yield return new Tool("new_game",
                "Starts a new game from the main menu, as New Game does with the Normal difficulty and the " +
                "default flag: in saves/<folder>, opening at the space centre. Waits until the space centre is " +
                "up. Refuses a folder that already holds a game.",
                Schema.Object(
                    Schema.P("folder", "string", "the new game's folder under saves/, also its name", true),
                    Schema.P("mode", "string", "CAREER, SCIENCE_SANDBOX or SANDBOX (default CAREER)")),
                NewGame);
            yield return new Tool("play_mission",
                "Starts a mission of Making History from the main menu, from its start, as Play Missions does: " +
                "the mission's own game is set up, its launch sites placed, and the game opens in the scene the " +
                "mission starts in. Waits until that scene is up. Needs the expansion, and the main menu.",
                Schema.Object(
                    Schema.P("mission", "string",
                        "the mission's folder: absolute, or its name under the user's Missions folder or the " +
                        "expansion's stock missions", true)),
                PlayMission);
            yield return new Tool("save_game",
                "Saves the game as it is now, as a quicksave does, to saves/<folder>/<save>.sfs, overwriting it. " +
                "Saved from flight, the save opens in flight.",
                Schema.Object(
                    Schema.P("save", "string", "the save's name, without .sfs", true),
                    Schema.P("folder", "string", "the game's folder under saves/ (default: the game being played)")),
                SaveGame);
            yield return new Tool("launch_vessel",
                "Launches a vessel from its .craft file at a launch site, as the editor's Launch button does, with " +
                "the crew the editor would give it by default, and waits until physics runs on it. KSP saves the " +
                "game as 'persistent' first, as it does for any launch. Needs a game loaded.",
                Schema.Object(
                    Schema.P("craft", "string",
                        "the .craft file: absolute, or relative to the Ships folder of the game being played " +
                        "(SPH/<name>.craft or VAB/<name>.craft)", true),
                    Schema.P("site", "string", "the launch site's name (default Runway; LaunchPad for the pad)")),
                LaunchVessel);
            yield return new Tool("list_vessels",
                "Lists the vessels of the game: id, name, situation, body, whether each is loaded and packed, " +
                "whether it is the active vessel or its target, and its distance from the active vessel in metres.",
                Schema.Object(), ListVessels);
            yield return new Tool("switch_vessel",
                "Makes a loaded vessel the active one, as the switch vessel keys [ and ] do, and waits until " +
                "physics runs on it.",
                Schema.Object(
                    Schema.P("vessel", "string", "the vessel's id, or its name when no other vessel bears it", true)),
                SwitchVessel);
            yield return new Tool("set_target",
                "Sets the target of the active vessel to another vessel, as Set as Target does, or clears it.",
                Schema.Object(
                    Schema.P("vessel", "string",
                        "the vessel's id, or its name when no other vessel bears it; left out, clears the target")),
                SetTarget);
            yield return new Tool("screenshot",
                "Captures the screen as it is drawn, user interface included. Returns the image, and saves it " +
                "as a PNG when a path is given (relative to the KSP folder, or absolute).",
                Schema.Object(
                    Schema.P("path", "string", "where to save the PNG"),
                    Schema.P("return_image", "boolean", "whether to return the image (default true)")),
                Screenshot);
            yield return new Tool("set_camera",
                "Sets the flight camera: distance from the vessel in metres, heading (the direction it looks in, " +
                "from north towards east) and pitch in degrees, and its field of view, which Alt and the mouse " +
                "wheel narrow (20 to 160 degrees, 60 by default), and where it aims away from the vessel, as " +
                "dragging with the middle mouse button does (at most half the field of view either way). Values " +
                "left out are kept.",
                Schema.Object(
                    Schema.P("distance", "number", "metres"),
                    Schema.P("heading", "number", "degrees"),
                    Schema.P("pitch", "number", "degrees"),
                    Schema.P("fov", "number", "field of view, degrees"),
                    Schema.P("aim_heading", "number", "degrees to the right of the vessel, middle mouse button"),
                    Schema.P("aim_pitch", "number", "degrees below the vessel, middle mouse button")),
                SetCamera);
            yield return new Tool("set_time",
                "Sets the universal time of the game, in seconds, in any scene but the main menu: the time of " +
                "day at a spot, for one. A flight reverted to its launch goes back to the time of the launch.",
                Schema.Object(Schema.P("ut", "number", "seconds", true)),
                SetTime);
            yield return new Tool("set_ui",
                "Hides or shows the game's interface in flight, as F2 does: navball, staging, toolbars. Windows " +
                "of mods that do not follow it stay.",
                Schema.Object(Schema.P("visible", "boolean", "true to show it, false to hide it", true)),
                SetUi);
            yield return new Tool("set_pause",
                "Pauses or resumes the flight, as Escape does, without the menu.",
                Schema.Object(Schema.P("paused", "boolean", "true to pause", true)),
                SetPause);
            yield return new Tool("set_cheats",
                "Turns on or off the cheats of the Alt+F12 menu given; those left out are kept. Answers with " +
                "their state.",
                Schema.Object(
                    Schema.P("infinite_electricity", "boolean", "Infinite Electricity"),
                    Schema.P("infinite_fuel", "boolean", "Infinite Fuel")),
                SetCheats);
            yield return new Tool("set_position",
                "Moves the active vessel just above a point of a body, as Set Position of the Alt+F12 menu does, and " +
                "lets it settle on the ground: answers once it is landed (or splashed) and still, or after the " +
                "time limit.",
                Schema.Object(
                    Schema.P("body", "string", "the body's name (default: the vessel's)"),
                    Schema.P("latitude", "number", "degrees", true),
                    Schema.P("longitude", "number", "degrees", true),
                    Schema.P("altitude", "number", "metres of the vessel's root part above the terrain (default 1; more than the distance from the root part to the lowest point of the vessel)"),
                    Schema.P("pitch", "number",
                        "degrees, as the menu's pitch (default 0); 90 stands a craft built in the SPH, such as a " +
                        "capsule on a tank, on its base"),
                    Schema.P("heading", "number", "degrees from north, towards east (default 0)"),
                    Schema.P("timeout", "number", "seconds to wait for the vessel to settle (default 60)")),
                SetPosition);
            yield return new Tool("get_terrain",
                "Reads the terrain of a body at a point, as the game computes it: the body's radius, the height " +
                "of the terrain above it, whether the sea covers it, and its slope and roughness (the largest " +
                "difference of height) from the heights at four points around it.",
                Schema.Object(
                    Schema.P("body", "string", "the body's name (default: the active vessel's)"),
                    Schema.P("latitude", "number", "degrees", true),
                    Schema.P("longitude", "number", "degrees", true),
                    Schema.P("spacing", "number", "metres from the point to the four points of the slope (default 5)")),
                GetTerrain);
            yield return new Tool("set_orbit",
                "Puts the active vessel on an orbit, as Set Orbit of the Alt+F12 menu does, and waits until " +
                "physics runs on it again.",
                Schema.Object(
                    Schema.P("body", "string", "the body's name (default: the vessel's)"),
                    Schema.P("altitude", "number", "metres above the body's radius, for a circular orbit"),
                    Schema.P("sma", "number", "semi-major axis in metres, instead of altitude"),
                    Schema.P("eccentricity", "number", "default 0"),
                    Schema.P("inclination", "number", "degrees, default 0"),
                    Schema.P("lan", "number", "longitude of the ascending node, degrees, default 0"),
                    Schema.P("argument_of_periapsis", "number", "degrees, default 0"),
                    Schema.P("mean_anomaly", "number", "radians at the current time, default 0")),
                SetOrbit);
            yield return new Tool("revert_to_launch",
                "Reverts the flight to its launch, as Revert to Launch does, and waits until physics runs on " +
                "the vessel again.",
                Schema.Object(), RevertToLaunch);
            yield return new Tool("revert_to_editor",
                "Reverts the flight to the editor it was launched from, as Revert to Vehicle Assembly Building " +
                "or Revert to Space Plane Hangar does, and waits until the editor is up.",
                Schema.Object(
                    Schema.P("facility", "string", "VAB or SPH (default VAB)")),
                RevertToEditor);
            yield return new Tool("recover_vessel",
                "Recovers the active vessel, as the Recover button above the altimeter does: KSP saves the game, " +
                "goes to the space centre and recovers the vessel there. Waits until the recovery report is up, " +
                "and answers with the place and the share of the value it gives (location, factor).",
                Schema.Object(), RecoverVessel);
            yield return new Tool("go_to_scene",
                "Leaves the flight, or the scene the game is in, for the space centre, the tracking station or " +
                "the main menu, saving the game as persistent first as the game's own buttons do, and waits until " +
                "that scene is up.",
                Schema.Object(
                    Schema.P("scene", "string", "SPACECENTER, TRACKSTATION or MAINMENU", true)),
                GoToScene);
            yield return new Tool("fly_vessel",
                "Flies a vessel from the tracking station, as its Fly button does: KSP saves the game as " +
                "persistent and opens the flight on that vessel. Waits until physics runs on it. Only a vessel " +
                "the player owns, not an asteroid.",
                Schema.Object(
                    Schema.P("vessel", "string", "the vessel's id, or its name when no other vessel bears it", true)),
                FlyVessel);
            yield return new Tool("open_facility",
                "Clicks a building of the space centre, as the left mouse button does: the Vehicle Assembly " +
                "Building and the Space Plane Hangar open their editor, the Tracking Station its scene, the other " +
                "buildings their screen over the space centre, or the dialog KSP shows when it is closed in this " +
                "game. Waits until the scene it opens is up, or a second for a screen. Answers with the scene " +
                "the game is in.",
                Schema.Object(
                    Schema.P("facility", "string",
                        "the building's facility name, as KSP names it: VAB, SPH, TrackingStation, " +
                        "AstronautComplex, RnD, MissionControl, Administration, LaunchPad, Runway, FlagPole", true)),
                OpenFacility);
            yield return new Tool("close_screen",
                "Closes one thing open over the scene, as its own button does, the topmost first: a dialog " +
                "(its button pressed when it has a single one, such as the guide's of a new career; otherwise " +
                "closed as Escape does), then, at the space centre, the recovery report, then the screen of a " +
                "building. Answers with what it closed: closed is nothing when nothing was open.",
                Schema.Object(
                    Schema.P("dialogs_only", "boolean", "close a dialog only, never a report or a screen (default false)")),
                CloseScreen);
            yield return new Tool("quit_game",
                "Quits KSP, a second after answering, so that the answer gets back first.",
                Schema.Object(), QuitGame);
            yield return new Tool("wait",
                "Waits a number of seconds of real time, letting the game run.",
                Schema.Object(Schema.P("seconds", "number", "how long", true)),
                Wait);
        }

        /// <summary>Counts a shift of the floating origin. Called by the host, which holds the subscription.</summary>
        public static void OnOriginShift(Vector3d offset, Vector3d nonFrame)
        {
            _shifts++;
            _lastShift = (offset + nonFrame).magnitude;
        }

        /// <summary>Starts counting again for a new flight scene. Called by the host.</summary>
        public static void OnFlightReady()
        {
            _shifts = 0;
            _lastShift = double.NaN;
        }

        /// <summary>The state of the game, as a dictionary ready to be written as JSON.</summary>
        public static Dictionary<string, object> State()
        {
            Dictionary<string, object> state = new Dictionary<string, object>
            {
                { "scene", HighLogic.LoadedScene.ToString() },
                { "paused", FlightDriver.Pause },
                // Without a planetarium, the time is the game's, and there is no game while KSP loads or on
                // the main menu.
                { "ut", Planetarium.fetch != null || HighLogic.CurrentGame != null
                    ? Planetarium.GetUniversalTime() : double.NaN }
            };
            Vessel v = HighLogic.LoadedSceneIsFlight ? FlightGlobals.ActiveVessel : null;
            if (v != null)
            {
                state["vessel"] = new Dictionary<string, object>
                {
                    { "name", v.vesselName },
                    { "situation", v.situation.ToString() },
                    { "packed", v.packed },
                    { "body", v.mainBody.bodyName },
                    { "latitude", v.latitude },
                    { "longitude", v.longitude },
                    { "altitude", v.altitude },
                    { "heightFromTerrain", (double)v.heightFromTerrain },
                    { "surfaceSpeed", v.srfSpeed },
                    { "heading", Geo.Heading(v) },
                    { "brakes", v.ActionGroups[KSPActionGroup.Brakes] },
                    { "sas", v.ActionGroups[KSPActionGroup.SAS] }
                };
            }
            return state;
        }

        /// <summary>The shifts of the floating origin counted since the flight scene opened.</summary>
        public static int Shifts
        {
            get { return _shifts; }
        }

        /// <summary>The floating origin seen from the active vessel, as a dictionary ready to be written as JSON.</summary>
        public static Dictionary<string, object> FloatingOriginState()
        {
            Dictionary<string, object> state = new Dictionary<string, object>
            {
                { "threshold", FloatingOrigin.fetch != null ? (double)FloatingOrigin.fetch.threshold : double.NaN },
                { "shiftsThisScene", _shifts },
                { "lastShift", _lastShift }
            };
            Vessel v = HighLogic.LoadedSceneIsFlight ? FlightGlobals.ActiveVessel : null;
            if (v != null)
            {
                // The origin of the world is the zero of world space: seen from the vessel, it lies at minus
                // the vessel's position.
                Vector3d toOrigin = -(Vector3d)v.transform.position;
                Vector3d north, east;
                Geo.NorthEast(v, out north, out east);
                Vector3d up = (v.CoMD - v.mainBody.position).normalized;
                state["distance"] = toOrigin.magnitude;
                state["north"] = Vector3d.Dot(toOrigin, north);
                state["east"] = Vector3d.Dot(toOrigin, east);
                state["up"] = Vector3d.Dot(toOrigin, up);
            }
            return state;
        }

        private static IEnumerator GetFloatingOrigin(ToolCall call)
        {
            call.Text(FloatingOriginState());
            yield break;
        }

        private static IEnumerator GetState(ToolCall call)
        {
            call.Text(State());
            yield break;
        }

        private static IEnumerator LoadSave(ToolCall call)
        {
            string folder = call.String("folder");
            string save = call.String("save");
            Game game = GamePersistence.LoadGame(save, folder, true, false);
            if (game == null || game.flightState == null)
            {
                call.Fail("Could not load saves/" + folder + "/" + save + ".sfs");
                yield break;
            }
            Vessel previous = FlightGlobals.ActiveVessel;
            HighLogic.SaveFolder = folder;
            HighLogic.CurrentGame = game;
            FlightDriver.StartAndFocusVessel(game, game.flightState.activeVesselIdx);
            yield return WaitForNewActiveVessel(call, previous);
        }

        private static IEnumerator OpenGame(ToolCall call)
        {
            string folder = call.String("folder");
            string save = call.String("save", "persistent");
            // Read as GamePersistence.LoadGame reads a save.
            ConfigNode node = GamePersistence.LoadSFSFile(save, folder);
            Game game = node != null ? GamePersistence.LoadGameCfg(node, save, true, false) : null;
            if (game == null)
            {
                call.Fail("Could not open saves/" + folder + "/" + save + ".sfs");
                yield break;
            }

            // What the main menu's Resume Game does, except saving the game back as persistent.
            Vessel previous = FlightGlobals.ActiveVessel;
            SceneWatch watch = new SceneWatch(game.startScene);
            GameEvents.onLevelWasLoadedGUIReady.Add(watch.OnLoaded);
            GamePersistence.UpdateScenarioModules(game);
            HighLogic.CurrentGame = game;
            HighLogic.SaveFolder = folder;
            GameEvents.onGameStatePostLoad.Fire(node);
            game.Start();

            if (game.startScene == GameScenes.FLIGHT)
            {
                GameEvents.onLevelWasLoadedGUIReady.Remove(watch.OnLoaded);
                yield return WaitForNewActiveVessel(call, previous);
                yield break;
            }

            // Until the scene asked for has loaded: it may be the one being left, so the scene loaded now
            // does not tell.
            float start = Time.realtimeSinceStartup;
            while (!watch.Loaded)
            {
                if (Time.realtimeSinceStartup - start > 180f)
                {
                    GameEvents.onLevelWasLoadedGUIReady.Remove(watch.OnLoaded);
                    call.Fail("The scene " + game.startScene + " was not up after 3 minutes");
                    yield break;
                }
                yield return null;
            }
            GameEvents.onLevelWasLoadedGUIReady.Remove(watch.OnLoaded);
            call.Text(State());
        }

        private static IEnumerator NewGame(ToolCall call)
        {
            if (HighLogic.LoadedScene != GameScenes.MAINMENU)
            {
                call.Fail("New games start from the main menu, and the game is in " + HighLogic.LoadedScene);
                yield break;
            }
            string folder = call.String("folder");
            Game.Modes mode;
            switch ((call.String("mode") ?? "CAREER").ToUpperInvariant())
            {
                case "CAREER":
                    mode = Game.Modes.CAREER;
                    break;
                case "SCIENCE_SANDBOX":
                    mode = Game.Modes.SCIENCE_SANDBOX;
                    break;
                case "SANDBOX":
                    mode = Game.Modes.SANDBOX;
                    break;
                default:
                    call.Fail("mode: CAREER, SCIENCE_SANDBOX or SANDBOX");
                    yield break;
            }
            string persistent = Path.Combine(Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, "saves"), folder),
                "persistent.sfs");
            if (File.Exists(persistent))
            {
                call.Fail("saves/" + folder + " already holds a game");
                yield break;
            }

            // What the main menu does once New Game is confirmed: the flag it proposes, the parameters of the
            // difficulty it selects first.
            MainMenu menu = UnityEngine.Object.FindObjectOfType<MainMenu>();
            string flag = menu != null && !string.IsNullOrEmpty(menu.DefaultFlagURL) ? menu.DefaultFlagURL : "Squad/Flags/default";
            GameParameters parameters = GameParameters.GetDefaultParameters(mode, GameParameters.Preset.Normal);
            SceneWatch watch = new SceneWatch(GameScenes.SPACECENTER);
            GameEvents.onLevelWasLoadedGUIReady.Add(watch.OnLoaded);
            HighLogic.CurrentGame = GamePersistence.CreateNewGame(folder, mode, parameters, flag,
                GameScenes.SPACECENTER, EditorFacility.None);
            GameEvents.onGameNewStart.Fire();
            HighLogic.CurrentGame.Start();
            yield return WaitForScene(call, watch);
            if (!call.IsError)
            {
                call.Text(State());
            }
        }

        private static IEnumerator PlayMission(ToolCall call)
        {
            if (!Expansions.ExpansionsLoader.IsExpansionInstalled("MakingHistory"))
            {
                call.Fail("Making History is not installed");
                yield break;
            }
            if (HighLogic.LoadedScene != GameScenes.MAINMENU || Expansions.Missions.Runtime.MissionSystem.Instance == null)
            {
                call.Fail("Missions start from the main menu, and the game is in " + HighLogic.LoadedScene);
                yield break;
            }
            string file = MissionFile(call.String("mission"));
            if (file == null)
            {
                call.Fail("No mission " + call.String("mission") + " (persistent.mission not found)");
                yield break;
            }
            Expansions.Missions.MissionFileInfo info = Expansions.Missions.MissionFileInfo.CreateFromPath(file);

            // What Play Missions does once a mission is picked: clear what an earlier mission left, set the
            // mission's game up, then start that game and clear the objects the setup used.
            MethodInfo removeObjects = typeof(Expansions.Missions.Runtime.MissionSystem).GetMethod("RemoveMissionObjects",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (removeObjects == null)
            {
                call.Fail("MissionSystem.RemoveMissionObjects not found in this version of KSP");
                yield break;
            }
            removeObjects.Invoke(null, new object[] { true });
            MissionStart start = new MissionStart();
            Expansions.Missions.Runtime.MissionSystem.Instance.StartCoroutine(
                Expansions.Missions.Runtime.MissionSystem.Instance.SetupMissionGame(info, true, false,
                    () => start.Succeeded = true, () => start.Failed = true));
            float begin = Time.realtimeSinceStartup;
            while (!start.Succeeded && !start.Failed)
            {
                if (Time.realtimeSinceStartup - begin > 180f)
                {
                    call.Fail("The mission was not set up after 3 minutes");
                    yield break;
                }
                yield return null;
            }
            if (start.Failed || HighLogic.CurrentGame == null)
            {
                removeObjects.Invoke(null, new object[] { false });
                call.Fail("KSP could not set the mission up: see KSP.log");
                yield break;
            }

            Game game = HighLogic.CurrentGame;
            Vessel previous = FlightGlobals.ActiveVessel;
            SceneWatch watch = new SceneWatch(game.startScene);
            GameEvents.onLevelWasLoadedGUIReady.Add(watch.OnLoaded);
            game.Start();
            removeObjects.Invoke(null, new object[] { false });

            if (game.startScene == GameScenes.FLIGHT)
            {
                GameEvents.onLevelWasLoadedGUIReady.Remove(watch.OnLoaded);
                yield return WaitForNewActiveVessel(call, previous);
                yield break;
            }
            begin = Time.realtimeSinceStartup;
            while (!watch.Loaded)
            {
                if (Time.realtimeSinceStartup - begin > 180f)
                {
                    GameEvents.onLevelWasLoadedGUIReady.Remove(watch.OnLoaded);
                    call.Fail("The scene " + game.startScene + " was not up after 3 minutes");
                    yield break;
                }
                yield return null;
            }
            GameEvents.onLevelWasLoadedGUIReady.Remove(watch.OnLoaded);
            call.Text(State());
        }

        /// <summary>
        /// The <c>persistent.mission</c> file of a mission given by its folder: absolute, or a folder name
        /// looked up among the user's missions, then the expansion's stock missions. Null if there is none.
        /// </summary>
        private static string MissionFile(string mission)
        {
            List<string> folders = new List<string>();
            if (Path.IsPathRooted(mission))
            {
                folders.Add(mission);
            }
            else
            {
                folders.Add(Path.Combine(Expansions.Missions.MissionsUtils.UsersMissionsPath, mission));
                folders.Add(Path.Combine(Expansions.Missions.MissionsUtils.StockMissionsPath, mission));
            }
            foreach (string folder in folders)
            {
                string file = Path.GetFullPath(Path.Combine(folder, "persistent.mission"));
                if (File.Exists(file))
                {
                    return file;
                }
            }
            return null;
        }

        /// <summary>How the setup of a mission ended, filled by its callbacks.</summary>
        private sealed class MissionStart
        {
            public bool Succeeded;
            public bool Failed;
        }

        /// <summary>Tells when a given scene has loaded. An instance, since an event refuses a static handler.</summary>
        private sealed class SceneWatch
        {
            private readonly GameScenes _scene;

            /// <summary>Whether the scene has loaded since the watch was made.</summary>
            public bool Loaded;

            public SceneWatch(GameScenes scene)
            {
                _scene = scene;
            }

            /// <summary>The scene watched for.</summary>
            public GameScenes Scene
            {
                get { return _scene; }
            }

            public void OnLoaded(GameScenes scene)
            {
                if (scene == _scene)
                {
                    Loaded = true;
                }
            }
        }

        private static IEnumerator SaveGame(ToolCall call)
        {
            if (HighLogic.CurrentGame == null)
            {
                call.Fail("No game loaded");
                yield break;
            }
            string save = call.String("save");
            string folder = call.String("folder", HighLogic.SaveFolder);

            // What a quicksave does: the game brought up to date, set to open in the scene it was saved from.
            Game game = HighLogic.CurrentGame.Updated();
            if (HighLogic.LoadedSceneIsFlight)
            {
                game.startScene = GameScenes.FLIGHT;
            }
            GamePersistence.SaveGame(game, save, folder, SaveMode.OVERWRITE);
            string path = Path.Combine(Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, "saves"), folder), save + ".sfs");
            if (!File.Exists(path))
            {
                call.Fail("Could not save " + path);
                yield break;
            }
            call.Text("Saved " + Path.GetFullPath(path));
        }

        private static IEnumerator LaunchVessel(ToolCall call)
        {
            if (HighLogic.CurrentGame == null)
            {
                call.Fail("No game loaded");
                yield break;
            }
            string path = call.String("craft");
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Path.Combine(Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, "saves"),
                    HighLogic.SaveFolder), "Ships"), path);
            }
            path = Path.GetFullPath(path);
            ConfigNode craft = File.Exists(path) ? ConfigNode.Load(path) : null;
            if (craft == null)
            {
                call.Fail("Could not read the craft " + path);
                yield break;
            }

            // The crew the editor proposes for that craft, without hiring anyone.
            VesselCrewManifest crew = HighLogic.CurrentGame.CrewRoster.DefaultCrewForVessel(craft, null, false);
            Vessel previous = FlightGlobals.ActiveVessel;
            FlightDriver.StartWithNewLaunch(path, HighLogic.CurrentGame.flagURL, call.String("site", "Runway"), crew);
            yield return WaitForNewActiveVessel(call, previous);
        }

        /// <summary>
        /// Waits until a flight scene is up with an active vessel other than <paramref name="previous"/>, and
        /// physics runs on it; then answers with the state of the game, or fails after three minutes.
        /// </summary>
        private static IEnumerator WaitForNewActiveVessel(ToolCall call, Vessel previous)
        {
            // The scene changes a few frames later: the vessel of the scene being left must not count. A
            // vessel destroyed with its scene compares equal to null, and so differs from the new one.
            float start = Time.realtimeSinceStartup;
            while (!(HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && FlightGlobals.ActiveVessel != null
                     && FlightGlobals.ActiveVessel != previous && !FlightGlobals.ActiveVessel.packed))
            {
                if (Time.realtimeSinceStartup - start > 180f)
                {
                    call.Fail("The flight scene was not ready after 3 minutes");
                    yield break;
                }
                yield return null;
            }
            call.Text(State());
        }

        private static IEnumerator ListVessels(ToolCall call)
        {
            if (!HighLogic.LoadedSceneIsFlight || FlightGlobals.fetch == null)
            {
                call.Fail("Not in flight");
                yield break;
            }
            Vessel active = FlightGlobals.ActiveVessel;
            ITargetable target = FlightGlobals.fetch.VesselTarget;
            Vessel targetVessel = target != null ? target.GetVessel() : null;
            List<object> vessels = new List<object>();
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (v == null)
                {
                    continue;
                }
                vessels.Add(new Dictionary<string, object>
                {
                    { "id", v.id.ToString() },
                    { "name", v.vesselName },
                    { "situation", v.situation.ToString() },
                    { "body", v.mainBody != null ? v.mainBody.bodyName : null },
                    { "loaded", v.loaded },
                    { "packed", v.packed },
                    { "active", v == active },
                    { "target", v == targetVessel },
                    { "distance", active != null ? Vector3d.Distance(v.GetWorldPos3D(), active.GetWorldPos3D()) : double.NaN }
                });
            }
            call.Text(vessels);
        }

        private static IEnumerator SwitchVessel(ToolCall call)
        {
            string error;
            Vessel vessel = FindVessel(call.String("vessel"), out error);
            if (vessel == null)
            {
                call.Fail(error);
                yield break;
            }
            if (!vessel.loaded)
            {
                call.Fail(vessel.vesselName + " is not loaded: only a vessel within loading range can be switched to");
                yield break;
            }
            if (vessel != FlightGlobals.ActiveVessel)
            {
                FlightGlobals.SetActiveVessel(vessel);
            }
            float start = Time.realtimeSinceStartup;
            while (FlightGlobals.ActiveVessel != vessel || vessel.packed)
            {
                if (Time.realtimeSinceStartup - start > 60f)
                {
                    call.Fail("Physics was not running on " + vessel.vesselName + " after a minute");
                    yield break;
                }
                yield return null;
            }
            call.Text(State());
        }

        private static IEnumerator SetTarget(ToolCall call)
        {
            if (!HighLogic.LoadedSceneIsFlight || FlightGlobals.fetch == null)
            {
                call.Fail("Not in flight");
                yield break;
            }
            if (!call.Has("vessel"))
            {
                FlightGlobals.fetch.SetVesselTarget(null);
                yield return null;
                call.Text("target cleared");
                yield break;
            }
            string error;
            Vessel vessel = FindVessel(call.String("vessel"), out error);
            if (vessel == null)
            {
                call.Fail(error);
                yield break;
            }
            if (vessel == FlightGlobals.ActiveVessel)
            {
                call.Fail("The active vessel cannot be its own target");
                yield break;
            }
            FlightGlobals.fetch.SetVesselTarget(vessel);
            yield return null;
            call.Text("target: " + vessel.vesselName + " (" + vessel.id + ")");
        }

        /// <summary>
        /// The vessel of the game whose id is <paramref name="key"/>, or else the only one named so; null with
        /// <paramref name="error"/> set when there is none, or when the name is shared.
        /// </summary>
        private static Vessel FindVessel(string key, out string error, bool trackingStation = false)
        {
            error = null;
            bool scene = HighLogic.LoadedSceneIsFlight
                || (trackingStation && HighLogic.LoadedScene == GameScenes.TRACKSTATION);
            if (!scene || FlightGlobals.fetch == null)
            {
                error = trackingStation ? "Neither in flight nor at the tracking station" : "Not in flight";
                return null;
            }
            Vessel found = null;
            int named = 0;
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (v == null)
                {
                    continue;
                }
                if (string.Equals(v.id.ToString(), key, StringComparison.OrdinalIgnoreCase))
                {
                    return v;
                }
                if (v.vesselName == key)
                {
                    found = v;
                    named++;
                }
            }
            if (named > 1)
            {
                error = named + " vessels are named " + key + ": give its id instead (list_vessels)";
                return null;
            }
            if (found == null)
            {
                error = "No vessel has the id or the name " + key;
            }
            return found;
        }

        private static IEnumerator Screenshot(ToolCall call)
        {
            yield return new WaitForEndOfFrame();
            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
            // The screen's alpha is whatever the shaders wrote, and the terrain's writes zero: kept, the ground
            // shows as transparent, white or black depending on the viewer. Encode the colours alone.
            Texture2D opaque = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            opaque.SetPixels32(texture.GetPixels32());
            opaque.Apply();
            byte[] png = opaque.EncodeToPNG();
            UnityEngine.Object.Destroy(texture);
            UnityEngine.Object.Destroy(opaque);

            string path = call.String("path");
            if (!string.IsNullOrEmpty(path))
            {
                if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(KSPUtil.ApplicationRootPath, path);
                }
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllBytes(path, png);
                call.Text("Saved " + Path.GetFullPath(path) + " (" + png.Length + " bytes)");
            }
            if (call.Bool("return_image", true))
            {
                call.Png(png);
            }
        }

        private static IEnumerator SetCamera(ToolCall call)
        {
            FlightCamera camera = FlightCamera.fetch;
            if (camera == null)
            {
                call.Fail("No flight camera: not in flight");
                yield break;
            }
            if (call.Has("distance"))
            {
                camera.SetDistanceImmediate((float)call.Number("distance"));
            }
            if (call.Has("heading"))
            {
                camera.camHdg = (float)(call.Number("heading") * Math.PI / 180.0);
            }
            if (call.Has("pitch"))
            {
                camera.camPitch = (float)(call.Number("pitch") * Math.PI / 180.0);
            }
            if (call.Has("fov"))
            {
                // What Alt and the mouse wheel do: the field kept by the camera, then applied to its cameras.
                camera.FieldOfView = Mathf.Clamp((float)call.Number("fov"), camera.fovMin, camera.fovMax);
                camera.SetFoV(camera.FieldOfView);
            }
            // What dragging with the middle mouse button changes: the aim, not the place of the camera. The game
            // keeps these fields protected, and clamps them every frame to half the field of view.
            if (call.Has("aim_heading"))
            {
                AimField("offsetHdg").SetValue(camera, (float)(call.Number("aim_heading") * Math.PI / 180.0));
            }
            if (call.Has("aim_pitch"))
            {
                AimField("offsetPitch").SetValue(camera, (float)(call.Number("aim_pitch") * Math.PI / 180.0));
            }
            yield return null;
            call.Text(new Dictionary<string, object>
            {
                { "distance", (double)camera.Distance },
                { "heading", camera.camHdg * 180.0 / Math.PI },
                { "pitch", camera.camPitch * 180.0 / Math.PI },
                { "fov", (double)camera.FieldOfView },
                { "aimHeading", (float)AimField("offsetHdg").GetValue(camera) * 180.0 / Math.PI },
                { "aimPitch", (float)AimField("offsetPitch").GetValue(camera) * 180.0 / Math.PI }
            });
        }

        /// <summary>A protected field of <see cref="FlightCamera"/> that holds where it aims.</summary>
        private static System.Reflection.FieldInfo AimField(string name)
        {
            return typeof(FlightCamera).GetField(name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        }

        private static IEnumerator SetTime(ToolCall call)
        {
            if (Planetarium.fetch == null)
            {
                call.Fail("No game loaded");
                yield break;
            }
            Planetarium.SetUniversalTime(call.Number("ut"));
            yield return null;
            call.Text(State());
        }

        private static IEnumerator SetUi(ToolCall call)
        {
            // What F2 does: the game and the mods that follow it listen to these two events.
            if (call.Bool("visible"))
            {
                GameEvents.onShowUI.Fire();
            }
            else
            {
                GameEvents.onHideUI.Fire();
            }
            yield return null;
            call.Text(new Dictionary<string, object> { { "visible", call.Bool("visible") } });
        }

        private static IEnumerator SetPause(ToolCall call)
        {
            FlightDriver.SetPause(call.Bool("paused"), false);
            yield return null;
            call.Text(new Dictionary<string, object> { { "paused", FlightDriver.Pause } });
        }

        private static IEnumerator SetCheats(ToolCall call)
        {
            if (call.Has("infinite_electricity"))
            {
                CheatOptions.InfiniteElectricity = call.Bool("infinite_electricity");
            }
            if (call.Has("infinite_fuel"))
            {
                CheatOptions.InfinitePropellant = call.Bool("infinite_fuel");
            }
            yield return null;
            call.Text(new Dictionary<string, object>
            {
                { "infinite_electricity", CheatOptions.InfiniteElectricity },
                { "infinite_fuel", CheatOptions.InfinitePropellant }
            });
        }

        /// <summary>
        /// The index in <see cref="FlightGlobals.Bodies"/> of the body named in the call, or of the active
        /// vessel's body when none is; -1 with <paramref name="error"/> set when there is no such body.
        /// </summary>
        private static int BodyIndex(ToolCall call, out string error)
        {
            error = null;
            // The active vessel is only read when no body is named: outside flight there is none.
            string name = call.Has("body") ? call.String("body") : FlightGlobals.ActiveVessel.mainBody.bodyName;
            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                if (string.Equals(FlightGlobals.Bodies[i].bodyName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            error = "No body named " + name;
            return -1;
        }

        private static bool InFlight(ToolCall call)
        {
            if (HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && FlightGlobals.ActiveVessel != null)
            {
                return true;
            }
            call.Fail("Not in flight");
            return false;
        }

        private static IEnumerator SetPosition(ToolCall call)
        {
            if (!InFlight(call))
            {
                yield break;
            }
            string error;
            int body = BodyIndex(call, out error);
            if (body < 0)
            {
                call.Fail(error);
                yield break;
            }
            // A vessel set where another one lies lands on it, and both break.
            CelestialBody target = FlightGlobals.Bodies[body];
            double lat = call.Number("latitude");
            double lon = call.Number("longitude");
            Vector3d spot = target.GetWorldSurfacePosition(lat, lon, TerrainHeight(target, lat, lon));
            foreach (Vessel other in FlightGlobals.Vessels)
            {
                if (other == null || other == FlightGlobals.ActiveVessel || other.mainBody != target)
                {
                    continue;
                }
                double distance = Vector3d.Distance(other.GetWorldPos3D(), spot);
                if (distance < 50.0)
                {
                    call.Fail(other.vesselName + " (" + other.id + ") lies " + distance.ToString("F1") + " m from that point");
                    yield break;
                }
            }

            // As the menu does it, never under the sea and the altitude counted from the terrain, but without
            // its ease to the ground: the game loses the lighter gravity of that ease when the vessel goes off
            // rails while keeping it flagged, so a vessel set high falls at full weight. Set it a little above the
            // ground instead.
            FlightGlobals.fetch.SetVesselPosition(body, lat, lon, call.Number("altitude", 1.0), call.Number("pitch", 0.0),
                call.Number("heading", 0.0), true, false);
            FloatingOrigin.ResetTerrainShaderOffset();

            // Settled: on the ground, and still for two seconds in a row.
            Vessel placed = FlightGlobals.ActiveVessel;
            float start = Time.realtimeSinceStartup;
            float stillSince = -1f;
            float limit = (float)call.Number("timeout", 60.0);
            while (Time.realtimeSinceStartup - start < limit)
            {
                yield return null;
                Vessel vessel = FlightGlobals.ActiveVessel;
                // A vessel that broke on landing is no longer the active one, or is dead.
                if (vessel != placed || vessel.state == Vessel.State.DEAD)
                {
                    call.Fail("The vessel was destroyed while it was put down");
                    yield break;
                }
                bool down = vessel != null && !vessel.packed
                    && (vessel.situation == Vessel.Situations.LANDED || vessel.situation == Vessel.Situations.SPLASHED
                        || vessel.situation == Vessel.Situations.PRELAUNCH);
                if (down && vessel.srfSpeed < 0.01)
                {
                    if (stillSince < 0f)
                    {
                        stillSince = Time.realtimeSinceStartup;
                    }
                    else if (Time.realtimeSinceStartup - stillSince >= 2f)
                    {
                        call.Text(State());
                        yield break;
                    }
                }
                else
                {
                    stillSince = -1f;
                }
            }
            call.Fail("The vessel had not settled after " + limit + " s: " + Json.Write(State()));
        }

        private static IEnumerator GetTerrain(ToolCall call)
        {
            if (FlightGlobals.Bodies == null || (!call.Has("body") && FlightGlobals.ActiveVessel == null))
            {
                call.Fail("Name a body, or be in flight");
                yield break;
            }
            string error;
            int index = BodyIndex(call, out error);
            if (index < 0)
            {
                call.Fail(error);
                yield break;
            }
            CelestialBody body = FlightGlobals.Bodies[index];
            if (body.pqsController == null)
            {
                call.Fail(body.bodyName + " has no terrain");
                yield break;
            }
            double lat = call.Number("latitude");
            double lon = call.Number("longitude");
            double spacing = call.Number("spacing", 5.0);
            double height = TerrainHeight(body, lat, lon);

            // The four points north, south, east and west of it, by the angle the spacing makes at the centre.
            double radius = body.Radius + height;
            double dLat = spacing / radius * 180.0 / Math.PI;
            double dLon = dLat / Math.Max(Math.Cos(lat * Math.PI / 180.0), 1e-6);
            double north = TerrainHeight(body, lat + dLat, lon);
            double south = TerrainHeight(body, lat - dLat, lon);
            double east = TerrainHeight(body, lat, lon + dLon);
            double west = TerrainHeight(body, lat, lon - dLon);
            double gradient = Math.Sqrt(Math.Pow((north - south) / (2 * spacing), 2) + Math.Pow((east - west) / (2 * spacing), 2));
            call.Text(new Dictionary<string, object>
            {
                { "body", body.bodyName },
                { "radius", body.Radius },
                { "height", height },
                { "underSea", body.ocean && height < 0.0 },
                { "slope", Math.Atan(gradient) * 180.0 / Math.PI },
                { "roughness", Math.Max(Math.Max(north, south), Math.Max(east, west))
                    - Math.Min(Math.Min(north, south), Math.Min(east, west)) }
            });
        }

        /// <summary>The height of the terrain of <paramref name="body"/> above its radius at a point.</summary>
        private static double TerrainHeight(CelestialBody body, double latitude, double longitude)
        {
            return body.pqsController.GetSurfaceHeight(body.GetRelSurfaceNVector(latitude, longitude)) - body.Radius;
        }

        private static IEnumerator SetOrbit(ToolCall call)
        {
            if (!InFlight(call))
            {
                yield break;
            }
            string error;
            int body = BodyIndex(call, out error);
            if (body < 0)
            {
                call.Fail(error);
                yield break;
            }
            double sma = call.Has("sma") ? call.Number("sma")
                : call.Has("altitude") ? FlightGlobals.Bodies[body].Radius + call.Number("altitude") : double.NaN;
            if (double.IsNaN(sma))
            {
                call.Fail("Give either altitude or sma");
                yield break;
            }
            FlightGlobals.fetch.SetShipOrbit(body, call.Number("eccentricity", 0.0), sma, call.Number("inclination", 0.0),
                call.Number("lan", 0.0), call.Number("mean_anomaly", 0.0), call.Number("argument_of_periapsis", 0.0), 0.0);
            FloatingOrigin.ResetTerrainShaderOffset();

            // The vessel is packed for the move, and unpacked a few frames later.
            yield return null;
            float start = Time.realtimeSinceStartup;
            while (FlightGlobals.ActiveVessel == null || FlightGlobals.ActiveVessel.packed)
            {
                if (Time.realtimeSinceStartup - start > 60f)
                {
                    call.Fail("Physics was not running on the vessel a minute after the orbit was set");
                    yield break;
                }
                yield return null;
            }
            call.Text(State());
        }

        private static IEnumerator RevertToLaunch(ToolCall call)
        {
            if (!InFlight(call))
            {
                yield break;
            }
            if (!FlightDriver.CanRevertToPostInit || FlightDriver.PostInitState == null)
            {
                call.Fail("This flight cannot be reverted to its launch");
                yield break;
            }
            Vessel previous = FlightGlobals.ActiveVessel;
            FlightDriver.RevertToLaunch();
            yield return WaitForNewActiveVessel(call, previous);
        }

        private static IEnumerator RevertToEditor(ToolCall call)
        {
            if (!InFlight(call))
            {
                yield break;
            }
            EditorFacility facility;
            switch ((call.String("facility") ?? "VAB").ToUpperInvariant())
            {
                case "VAB":
                    facility = EditorFacility.VAB;
                    break;
                case "SPH":
                    facility = EditorFacility.SPH;
                    break;
                default:
                    call.Fail("facility: VAB or SPH");
                    yield break;
            }
            if (!FlightDriver.CanRevertToPrelaunch || FlightDriver.PreLaunchState == null)
            {
                call.Fail("This flight cannot be reverted to an editor");
                yield break;
            }
            SceneWatch watch = new SceneWatch(GameScenes.EDITOR);
            GameEvents.onLevelWasLoadedGUIReady.Add(watch.OnLoaded);
            FlightDriver.RevertToPrelaunch(facility);
            yield return WaitForScene(call, watch);
            if (!call.IsError)
            {
                call.Text(State());
            }
        }

        private static IEnumerator RecoverVessel(ToolCall call)
        {
            if (!InFlight(call))
            {
                yield break;
            }
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || !v.IsRecoverable)
            {
                call.Fail("The active vessel cannot be recovered");
                yield break;
            }
            // The button refuses for the same reasons: moving, on a ladder, about to crash...
            ClearToSaveStatus status = FlightGlobals.ClearToSave();
            if (status != ClearToSaveStatus.CLEAR)
            {
                call.Fail("KSP refuses to recover now: " + status);
                yield break;
            }
            SceneWatch watch = new SceneWatch(GameScenes.SPACECENTER);
            GameEvents.onLevelWasLoadedGUIReady.Add(watch.OnLoaded);
            GameEvents.OnVesselRecoveryRequested.Fire(v);
            yield return WaitForScene(call, watch);
            if (call.IsError)
            {
                yield break;
            }

            // The vessel is recovered once the space centre is up, and the report follows.
            KSP.UI.Screens.MissionRecoveryDialog report = null;
            float start = Time.realtimeSinceStartup;
            while (report == null)
            {
                if (Time.realtimeSinceStartup - start > 30f)
                {
                    call.Fail("The space centre is up, but no recovery report after 30 seconds");
                    yield break;
                }
                yield return null;
                report = UnityEngine.Object.FindObjectOfType<KSP.UI.Screens.MissionRecoveryDialog>();
            }
            Dictionary<string, object> state = State();
            state["recovery"] = new Dictionary<string, object>
            {
                { "location", report.recoveryLocation },
                { "factor", report.recoveryFactor }
            };
            call.Text(state);
        }

        private static IEnumerator GoToScene(ToolCall call)
        {
            GameScenes scene;
            switch ((call.String("scene") ?? "").ToUpperInvariant())
            {
                case "SPACECENTER":
                    scene = GameScenes.SPACECENTER;
                    break;
                case "TRACKSTATION":
                    scene = GameScenes.TRACKSTATION;
                    break;
                case "MAINMENU":
                    scene = GameScenes.MAINMENU;
                    break;
                default:
                    call.Fail("scene: SPACECENTER, TRACKSTATION or MAINMENU");
                    yield break;
            }
            if (HighLogic.CurrentGame == null)
            {
                call.Fail("No game loaded");
                yield break;
            }
            // What the buttons above the altimeter do in flight, and what leaving any other scene does. Quit
            // to Main Menu, in flight, also forgets the vessels' persistent ids before saving (internal).
            if (scene == GameScenes.MAINMENU && HighLogic.LoadedSceneIsFlight)
            {
                MethodInfo clearIds = typeof(FlightGlobals).GetMethod("ClearpersistentIdDictionaries",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (clearIds == null)
                {
                    call.Fail("FlightGlobals.ClearpersistentIdDictionaries not found in this version of KSP");
                    yield break;
                }
                clearIds.Invoke(null, null);
            }
            SceneWatch watch = new SceneWatch(scene);
            GameEvents.onLevelWasLoadedGUIReady.Add(watch.OnLoaded);
            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
            HighLogic.LoadScene(scene);
            yield return WaitForScene(call, watch);
            if (!call.IsError)
            {
                call.Text(State());
            }
        }

        private static IEnumerator FlyVessel(ToolCall call)
        {
            if (HighLogic.LoadedScene != GameScenes.TRACKSTATION)
            {
                call.Fail("Vessels are flown from the tracking station, and the game is in " + HighLogic.LoadedScene);
                yield break;
            }
            string error;
            Vessel vessel = FindVessel(call.String("vessel"), out error, true);
            if (vessel == null)
            {
                call.Fail(error);
                yield break;
            }
            if (vessel.DiscoveryInfo.Level != DiscoveryLevels.Owned)
            {
                call.Fail(vessel.vesselName + " is not the player's: the tracking station does not fly it");
                yield break;
            }
            // What the Fly button does for a vessel of the player's.
            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
            FlightDriver.StartAndFocusVessel("persistent", FlightGlobals.Vessels.IndexOf(vessel));
            yield return WaitForNewActiveVessel(call, null);
        }

        private static IEnumerator OpenFacility(ToolCall call)
        {
            if (HighLogic.LoadedScene != GameScenes.SPACECENTER)
            {
                call.Fail("Buildings open from the space centre, and the game is in " + HighLogic.LoadedScene);
                yield break;
            }
            string name = call.String("facility") ?? "";
            SpaceCenterBuilding building = null;
            List<string> names = new List<string>();
            foreach (SpaceCenterBuilding b in UnityEngine.Object.FindObjectsOfType<SpaceCenterBuilding>())
            {
                names.Add(b.facilityName);
                if (string.Equals(b.facilityName, name, StringComparison.OrdinalIgnoreCase))
                {
                    building = b;
                }
            }
            if (building == null)
            {
                names.Sort(StringComparer.Ordinal);
                call.Fail("No building " + name + " at the space centre; there are: " + string.Join(", ", names.ToArray()));
                yield break;
            }

            // A building either opens a scene, or a screen over the space centre: a second tells them apart.
            // Both handlers before the click: the scene may be up a few frames after it is asked for.
            SceneRequest request = new SceneRequest();
            GameEvents.onGameSceneLoadRequested.Add(request.OnRequested);
            GameEvents.onLevelWasLoadedGUIReady.Add(request.OnLoaded);
            building.OnLeftClick();
            float start = Time.realtimeSinceStartup;
            while (!request.Loaded)
            {
                float elapsed = Time.realtimeSinceStartup - start;
                if (!request.Requested && elapsed >= 1f)
                {
                    break;
                }
                if (elapsed > 180f)
                {
                    call.Fail("The scene " + request.Scene + " was not up after 3 minutes");
                    break;
                }
                yield return null;
            }
            GameEvents.onGameSceneLoadRequested.Remove(request.OnRequested);
            GameEvents.onLevelWasLoadedGUIReady.Remove(request.OnLoaded);
            if (!call.IsError)
            {
                call.Text(State());
            }
        }

        private static IEnumerator CloseScreen(ToolCall call)
        {
            Dictionary<string, object> answer = new Dictionary<string, object> { { "closed", "nothing" } };
            PopupDialog[] dialogs = UnityEngine.Object.FindObjectsOfType<PopupDialog>();
            KSP.UI.Screens.MissionRecoveryDialog report = HighLogic.LoadedScene == GameScenes.SPACECENTER
                ? UnityEngine.Object.FindObjectOfType<KSP.UI.Screens.MissionRecoveryDialog>() : null;
            KSP.UI.Screens.UISpaceCenter ui = HighLogic.LoadedScene == GameScenes.SPACECENTER
                ? KSP.UI.Screens.UISpaceCenter.Instance : null;
            bool dialogsOnly = call.Bool("dialogs_only");
            if (dialogs.Length > 0)
            {
                PopupDialog dialog = dialogs[dialogs.Length - 1];
                List<DialogGUIButton> buttons = new List<DialogGUIButton>();
                if (dialog.dialogToDisplay != null && dialog.dialogToDisplay.Options != null)
                {
                    foreach (DialogGUIBase option in dialog.dialogToDisplay.Options)
                    {
                        CollectButtons(option, buttons);
                    }
                }
                answer["closed"] = "dialog";
                answer["dialog"] = dialog.dialogToDisplay != null ? dialog.dialogToDisplay.name : "";
                if (buttons.Count == 1)
                {
                    // What a click on its only button does: its callback, then the dialog closed if the button
                    // closes it and the callback did not already.
                    answer["button"] = buttons[0].OptionText;
                    buttons[0].OptionSelected();
                    yield return null;
                    if (dialog != null && buttons[0].DismissOnSelect)
                    {
                        dialog.Dismiss();
                    }
                }
                else
                {
                    dialog.Dismiss();
                }
            }
            else if (report != null && !dialogsOnly)
            {
                // What its button does.
                UnityEngine.Object.Destroy(report.gameObject);
                answer["closed"] = "recovery report";
            }
            else if (ui != null && !dialogsOnly)
            {
                // What the screen's Exit button does, the event Escape fires too.
                if (ui.SpawnedAC)
                {
                    GameEvents.onGUIAstronautComplexDespawn.Fire();
                    answer["closed"] = "AstronautComplex";
                }
                else if (ui.SpawnedRD)
                {
                    GameEvents.onGUIRnDComplexDespawn.Fire();
                    answer["closed"] = "RnD";
                }
                else if (ui.SpawnedMC)
                {
                    GameEvents.onGUIMissionControlDespawn.Fire();
                    answer["closed"] = "MissionControl";
                }
                else if (ui.SpawnedADM)
                {
                    GameEvents.onGUIAdministrationFacilityDespawn.Fire();
                    answer["closed"] = "Administration";
                }
            }
            yield return new WaitForSecondsRealtime(1f);
            call.Text(answer);
        }

        /// <summary>Adds to <paramref name="buttons"/> the buttons of a dialog element and of all it holds.</summary>
        private static void CollectButtons(DialogGUIBase element, List<DialogGUIButton> buttons)
        {
            if (element == null)
            {
                return;
            }
            DialogGUIButton button = element as DialogGUIButton;
            if (button != null)
            {
                buttons.Add(button);
            }
            foreach (DialogGUIBase child in element.children)
            {
                CollectButtons(child, buttons);
            }
        }

        /// <summary>
        /// Waits until the scene of <paramref name="watch"/>, already subscribed, has loaded, then drops the
        /// subscription; fails the call after three minutes.
        /// </summary>
        private static IEnumerator WaitForScene(ToolCall call, SceneWatch watch)
        {
            float start = Time.realtimeSinceStartup;
            while (!watch.Loaded)
            {
                if (Time.realtimeSinceStartup - start > 180f)
                {
                    GameEvents.onLevelWasLoadedGUIReady.Remove(watch.OnLoaded);
                    call.Fail("The scene " + watch.Scene + " was not up after 3 minutes");
                    yield break;
                }
                yield return null;
            }
            GameEvents.onLevelWasLoadedGUIReady.Remove(watch.OnLoaded);
        }

        /// <summary>
        /// Records the scene KSP is asked to load, and whether it has loaded since. An instance, since an event
        /// refuses a static handler.
        /// </summary>
        private sealed class SceneRequest
        {
            public bool Requested;
            public GameScenes Scene;
            public bool Loaded;

            public void OnRequested(GameScenes scene)
            {
                Requested = true;
                Scene = scene;
            }

            public void OnLoaded(GameScenes scene)
            {
                if (Requested && scene == Scene)
                {
                    Loaded = true;
                }
            }
        }

        private static IEnumerator QuitGame(ToolCall call)
        {
            // The answer is only sent once this tool ends: quitting happens later, on its own.
            MainThread.Host.StartCoroutine(QuitSoon());
            call.Text("quitting");
            yield break;
        }

        private static IEnumerator QuitSoon()
        {
            yield return new WaitForSecondsRealtime(1f);
            Log.Info("Quitting KSP, as asked");
            Application.Quit();
        }

        private static IEnumerator Wait(ToolCall call)
        {
            yield return new WaitForSecondsRealtime((float)call.Number("seconds", 1.0));
            call.Text("waited");
        }
    }
}
