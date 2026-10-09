using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>Tools that show the game: the screenshot, the flight camera, the interface.</summary>
    internal static class ViewTools
    {
        // Protected in FlightCamera: where it aims, away from the vessel, as the middle mouse button sets it.
        private static readonly FieldInfo AimHeading = typeof(FlightCamera).GetField("offsetHdg",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AimPitch = typeof(FlightCamera).GetField("offsetPitch",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public static IEnumerable<Tool> All()
        {
            yield return new Tool("screenshot",
                "Captures the screen as it is drawn, user interface included. Returns the image, and saves it " +
                "as a PNG when a path is given (relative to the KSP folder, or absolute).",
                Schema.Object(
                    Schema.P("path", "string", "where to save the PNG"),
                    Schema.P("return_image", "boolean", "whether to return the image (default true)")),
                Screenshot) { Concurrent = true };
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
                SetCamera) { Concurrent = true };
            yield return new Tool("set_ui",
                "Hides or shows the game's interface in flight, as F2 does: navball, staging, toolbars. Windows " +
                "of mods that do not follow it stay.",
                Schema.Object(Schema.P("visible", "boolean", "true to show it, false to hide it", true)),
                SetUi) { Concurrent = true };
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
                camera.camHdg = Radians(call.Number("heading"));
            }
            if (call.Has("pitch"))
            {
                camera.camPitch = Radians(call.Number("pitch"));
            }
            if (call.Has("fov"))
            {
                // What Alt and the mouse wheel do: the field kept by the camera, then applied to its cameras.
                camera.FieldOfView = Mathf.Clamp((float)call.Number("fov"), camera.fovMin, camera.fovMax);
                camera.SetFoV(camera.FieldOfView);
            }
            // What dragging with the middle mouse button changes: the aim, not the place of the camera. The game
            // clamps it every frame to half the field of view.
            if (call.Has("aim_heading"))
            {
                AimHeading.SetValue(camera, Radians(call.Number("aim_heading")));
            }
            if (call.Has("aim_pitch"))
            {
                AimPitch.SetValue(camera, Radians(call.Number("aim_pitch")));
            }
            yield return null;
            call.Text(new Dictionary<string, object>
            {
                { "distance", (double)camera.Distance },
                { "heading", camera.camHdg * 180.0 / Math.PI },
                { "pitch", camera.camPitch * 180.0 / Math.PI },
                { "fov", (double)camera.FieldOfView },
                { "aimHeading", (float)AimHeading.GetValue(camera) * 180.0 / Math.PI },
                { "aimPitch", (float)AimPitch.GetValue(camera) * 180.0 / Math.PI }
            });
        }

        private static float Radians(double degrees)
        {
            return (float)(degrees * Math.PI / 180.0);
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
    }
}
