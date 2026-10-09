using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// Tools that choose the active vessel and its target, and move it as the Alt+F12 menu does: set it on
    /// the ground or on an orbit, with its cheats.
    /// </summary>
    internal static class VesselTools
    {
        public static IEnumerable<Tool> All()
        {
            yield return new Tool("switch_vessel",
                "Makes a vessel the active one, and waits until physics runs on it: a loaded vessel as the switch " +
                "vessel keys [ and ] do; one that is not loaded, of the player's, as Switch To of the map view " +
                "does: KSP saves the game as persistent and opens the flight again on it. Fails when KSP refuses " +
                "the switch (the active vessel under acceleration, moving over the surface, in the atmosphere...).",
                Schema.Object(
                    Schema.P("vessel", "string", "the vessel's id, or its name when no other vessel bears it", true)),
                SwitchVessel);
            yield return new Tool("set_target",
                "Sets the target of the active vessel to another vessel, as Set as Target does, or clears it.",
                Schema.Object(
                    Schema.P("vessel", "string",
                        "the vessel's id, or its name when no other vessel bears it; left out, clears the target")),
                SetTarget);
            yield return new Tool("set_cheats",
                "Turns on or off the cheats of the Alt+F12 menu given; those left out are kept. Answers with " +
                "their state.",
                Schema.Object(
                    Schema.P("infinite_electricity", "boolean", "Infinite Electricity"),
                    Schema.P("infinite_fuel", "boolean", "Infinite Fuel")),
                SetCheats) { Concurrent = true };
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
        }

        private static IEnumerator SwitchVessel(ToolCall call)
        {
            string error;
            Vessel vessel = Lookup.FindVessel(call.String("vessel"), out error);
            if (vessel == null)
            {
                call.Fail(error);
                yield break;
            }
            if (vessel == FlightGlobals.ActiveVessel)
            {
                call.Text(GameState.State());
                yield break;
            }
            if (!vessel.loaded)
            {
                // What Switch To of the map view does (MapContextMenuOptions.FocusObject): only for a vessel of
                // the player's, and only if the game lets the player switch to a vessel far away.
                if (vessel.DiscoveryInfo.Level != DiscoveryLevels.Owned)
                {
                    call.Fail(vessel.vesselName + " is not the player's: the map view does not switch to it");
                    yield break;
                }
                if (!HighLogic.CurrentGame.Parameters.Flight.CanSwitchVesselsFar)
                {
                    call.Fail("This game does not let the player switch to a vessel far away");
                    yield break;
                }
            }
            // Read before the switch: KSP refuses it for these reasons only, with a message on the screen.
            ClearToSaveStatus clear = FlightGlobals.ClearToSave();
            if (!FlightGlobals.SetActiveVessel(vessel))
            {
                call.Fail("KSP refused to switch to " + vessel.vesselName + ": " + clear);
                yield break;
            }
            if (!vessel.loaded)
            {
                // KSP saves the game as persistent and opens the flight again on that vessel: a new scene, with
                // a new Vessel object, found by its id.
                yield return Waits.ForNewActiveVessel(call, null, vessel.id);
                yield break;
            }
            yield return Waits.Until(call, () => FlightGlobals.ActiveVessel == vessel && !vessel.packed, 60f,
                "Physics was not running on " + vessel.vesselName + " after a minute");
            if (!call.IsError)
            {
                call.Text(GameState.State());
            }
        }

        private static IEnumerator SetTarget(ToolCall call)
        {
            if (!Require.FlightScene(call))
            {
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
            Vessel vessel = Lookup.FindVessel(call.String("vessel"), out error);
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

        private static IEnumerator SetPosition(ToolCall call)
        {
            if (!Require.Flight(call))
            {
                yield break;
            }
            int body = Lookup.BodyIndex(call);
            if (body < 0)
            {
                yield break;
            }
            // A vessel set where another one lies lands on it, and both break.
            CelestialBody target = FlightGlobals.Bodies[body];
            double lat = call.Number("latitude");
            double lon = call.Number("longitude");
            Vector3d spot = target.GetWorldSurfacePosition(lat, lon, Lookup.TerrainHeight(target, lat, lon));
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
                bool down = !vessel.packed
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
                        call.Text(GameState.State());
                        yield break;
                    }
                }
                else
                {
                    stillSince = -1f;
                }
            }
            call.Fail("The vessel had not settled after " + limit + " s: " + Json.Write(GameState.State()));
        }

        private static IEnumerator SetOrbit(ToolCall call)
        {
            if (!Require.Flight(call))
            {
                yield break;
            }
            int body = Lookup.BodyIndex(call);
            if (body < 0)
            {
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
            yield return Waits.Until(call, () => FlightGlobals.ActiveVessel != null && !FlightGlobals.ActiveVessel.packed,
                60f, "Physics was not running on the vessel a minute after the orbit was set");
            if (!call.IsError)
            {
                call.Text(GameState.State());
            }
        }
    }
}
