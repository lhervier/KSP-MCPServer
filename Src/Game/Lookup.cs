using System;
using System.Collections.Generic;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>Finds what the arguments of a tool name: a vessel, a body, a building; and reads the terrain.</summary>
    internal static class Lookup
    {
        /// <summary>
        /// The vessel of the game whose id is <paramref name="key"/>, or else the only one named so; null with
        /// <paramref name="error"/> set when there is none, or when the name is shared. In flight only, or
        /// also at the tracking station when <paramref name="trackingStation"/> is true.
        /// </summary>
        public static Vessel FindVessel(string key, out string error, bool trackingStation = false)
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

        /// <summary>
        /// The index in <see cref="FlightGlobals.Bodies"/> of the body named by the <c>body</c> argument, or of
        /// the active vessel's body when it is left out; -1 after failing the call when there is no such body.
        /// </summary>
        public static int BodyIndex(ToolCall call)
        {
            // The active vessel is only read when no body is named: outside flight there is none.
            string name = call.Has("body") ? call.String("body") : FlightGlobals.ActiveVessel.mainBody.bodyName;
            for (int i = 0; i < FlightGlobals.Bodies.Count; i++)
            {
                if (string.Equals(FlightGlobals.Bodies[i].bodyName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            call.Fail("No body named " + name);
            return -1;
        }

        /// <summary>
        /// The building of the space centre named by the <c>facility</c> argument, or null after failing the
        /// call with the names of those there are.
        /// </summary>
        public static SpaceCenterBuilding FindBuilding(ToolCall call)
        {
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
            }
            return building;
        }

        /// <summary>The height of the terrain of <paramref name="body"/> above its radius at a point.</summary>
        public static double TerrainHeight(CelestialBody body, double latitude, double longitude)
        {
            return body.pqsController.GetSurfaceHeight(body.GetRelSurfaceNVector(latitude, longitude)) - body.Radius;
        }
    }
}
