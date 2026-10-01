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
                { "ut", Planetarium.GetUniversalTime() }
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
            HighLogic.SaveFolder = folder;
            HighLogic.CurrentGame = game;
            FlightDriver.StartAndFocusVessel(game, game.flightState.activeVesselIdx);

            // Until the flight scene is up and physics has the active vessel.
            float start = Time.realtimeSinceStartup;
            while (!(HighLogic.LoadedSceneIsFlight && FlightGlobals.ready && FlightGlobals.ActiveVessel != null
                     && !FlightGlobals.ActiveVessel.packed))
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
