using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
                "Sets the flight camera: distance from the vessel in metres, heading and pitch in degrees. " +
                "Values left out are kept.",
                Schema.Object(
                    Schema.P("distance", "number", "metres"),
                    Schema.P("heading", "number", "degrees"),
                    Schema.P("pitch", "number", "degrees")),
                SetCamera);
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
                "Moves the active vessel above a point of a body, as Set Position of the Alt+F12 menu does, and " +
                "lets it settle on the ground: answers once it is landed (or splashed) and still, or after the " +
                "time limit.",
                Schema.Object(
                    Schema.P("body", "string", "the body's name (default: the vessel's)"),
                    Schema.P("latitude", "number", "degrees", true),
                    Schema.P("longitude", "number", "degrees", true),
                    Schema.P("altitude", "number", "metres above the terrain to drop from (default 2)"),
                    Schema.P("heading", "number", "degrees from north, towards east (default 0)"),
                    Schema.P("timeout", "number", "seconds to wait for the vessel to settle (default 60)")),
                SetPosition);
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
            yield return new Tool("go_to_scene",
                "Leaves the flight, or the scene the game is in, for the space centre or the tracking station, " +
                "saving the game as persistent first as the game's own buttons do, and waits until that scene is up.",
                Schema.Object(
                    Schema.P("scene", "string", "SPACECENTER or TRACKSTATION", true)),
                GoToScene);
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
        private static Vessel FindVessel(string key, out string error)
        {
            error = null;
            if (!HighLogic.LoadedSceneIsFlight || FlightGlobals.fetch == null)
            {
                error = "Not in flight";
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
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.Destroy(texture);

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
            yield return null;
            call.Text(new Dictionary<string, object>
            {
                { "distance", (double)camera.Distance },
                { "heading", camera.camHdg * 180.0 / Math.PI },
                { "pitch", camera.camPitch * 180.0 / Math.PI }
            });
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
            string name = call.String("body", FlightGlobals.ActiveVessel.mainBody.bodyName);
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
            // As the menu does it, with the vessel upright and eased down onto the ground: never under the
            // sea, and the altitude counted from the terrain.
            FlightGlobals.fetch.SetVesselPosition(body, call.Number("latitude"), call.Number("longitude"),
                call.Number("altitude", 2.0), 0.0, call.Number("heading", 0.0), true, true);
            FloatingOrigin.ResetTerrainShaderOffset();

            // Settled: on the ground, out of the ease-in, and still for two seconds in a row.
            Vessel vessel = FlightGlobals.ActiveVessel;
            float start = Time.realtimeSinceStartup;
            float stillSince = -1f;
            float limit = (float)call.Number("timeout", 60.0);
            while (Time.realtimeSinceStartup - start < limit)
            {
                yield return null;
                vessel = FlightGlobals.ActiveVessel;
                bool down = vessel != null && !vessel.packed && !vessel.easingInToSurface
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
                default:
                    call.Fail("scene: SPACECENTER or TRACKSTATION");
                    yield break;
            }
            if (HighLogic.CurrentGame == null)
            {
                call.Fail("No game loaded");
                yield break;
            }
            SceneWatch watch = new SceneWatch(scene);
            GameEvents.onLevelWasLoadedGUIReady.Add(watch.OnLoaded);
            // What the buttons above the altimeter do in flight, and what leaving any other scene does.
            GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
            HighLogic.LoadScene(scene);
            float start = Time.realtimeSinceStartup;
            while (!watch.Loaded)
            {
                if (Time.realtimeSinceStartup - start > 180f)
                {
                    GameEvents.onLevelWasLoadedGUIReady.Remove(watch.OnLoaded);
                    call.Fail("The scene " + scene + " was not up after 3 minutes");
                    yield break;
                }
                yield return null;
            }
            GameEvents.onLevelWasLoadedGUIReady.Remove(watch.OnLoaded);
            call.Text(State());
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
