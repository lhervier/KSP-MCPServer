using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>Tools that set the game's time: the universal time, the time warp, the pause.</summary>
    internal static class TimeTools
    {
        // Private in TimeWarp: what Alt with the warp keys calls, and whether a Warp To is running.
        private static readonly MethodInfo WarpSetMode = typeof(TimeWarp).GetMethod("setMode",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo WarpAutoEngaged = typeof(TimeWarp).GetField("autoWarpEngaged",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public static IEnumerable<Tool> All()
        {
            yield return new Tool("set_time",
                "Sets the universal time of the game, in seconds, in any scene but the main menu: the time of " +
                "day at a spot, for one. A flight reverted to its launch goes back to the time of the launch.",
                Schema.Object(Schema.P("ut", "number", "seconds", true)),
                SetTime);
            yield return new Tool("set_warp",
                "Sets the time warp in flight, as the . and , keys do, or Alt with them for physics warp: the rate " +
                "is an index into KSP's rates, 0 for normal time. Waits until KSP has reached it. Fails when KSP " +
                "allows a lower rate only (altitude, atmosphere, an engine firing, a vessel moving over the " +
                "ground): it then stays at that rate, which the answer gives.",
                Schema.Object(
                    Schema.P("rate_index", "number",
                        "0 for normal time; on rails, up to 7 (100000x with KSP's rates), in physics warp up to 3 (4x)", true),
                    Schema.P("physics", "boolean", "physics warp, as Alt with the keys (default false: on rails)")),
                SetWarp);
            yield return new Tool("warp_to",
                "Warps to a universal time, as Warp To of a manoeuvre node or of the map view does: KSP picks the " +
                "rates on rails, slows down and stops at that time. Waits until it is back to normal time.",
                Schema.Object(
                    Schema.P("ut", "number", "the universal time to warp to, in seconds"),
                    Schema.P("seconds", "number", "instead of ut: how long to warp for, in seconds of game time")),
                WarpTo);
            yield return new Tool("set_pause",
                "Pauses or resumes the flight, as Escape does, without the menu.",
                Schema.Object(Schema.P("paused", "boolean", "true to pause", true)),
                SetPause) { Concurrent = true };
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
            call.Text(GameState.State());
        }

        private static IEnumerator SetWarp(ToolCall call)
        {
            TimeWarp warp = TimeWarp.fetch;
            if (!HighLogic.LoadedSceneIsFlight || warp == null || !FlightGlobals.ready)
            {
                call.Fail("Not in flight");
                yield break;
            }
            bool physics = call.Bool("physics");
            float[] rates = physics ? warp.physicsWarpRates : warp.warpRates;
            double asked = call.Number("rate_index");
            int index = (int)asked;
            if (index != asked || index < 0 || index >= rates.Length)
            {
                call.Fail("rate_index: 0 to " + (rates.Length - 1));
                yield break;
            }

            // The keys switch between rails and physics warp at the lowest rates only.
            TimeWarp.Modes mode = physics ? TimeWarp.Modes.LOW : TimeWarp.Modes.HIGH;
            if (warp.Mode != mode)
            {
                if (TimeWarp.CurrentRateIndex != 0)
                {
                    TimeWarp.SetRate(0, true);
                    yield return null;
                }
                if (!(bool)WarpSetMode.Invoke(warp, new object[] { mode }))
                {
                    call.Fail("KSP did not switch to " + (physics ? "physics" : "rails") + " warp");
                    yield break;
                }
            }

            // KSP lowers the rate asked for to the one it allows, then reaches it gradually.
            TimeWarp.SetRate(index, false);
            float start = Time.realtimeSinceStartup;
            do
            {
                yield return null;
            }
            while (TimeWarp.CurrentRate != warp.tgt_rate && Time.realtimeSinceStartup - start < 10f);

            Dictionary<string, object> answer = new Dictionary<string, object>
            {
                { "physics", warp.Mode == TimeWarp.Modes.LOW },
                { "rateIndex", TimeWarp.CurrentRateIndex },
                { "rate", (double)TimeWarp.CurrentRate },
                { "ut", Planetarium.GetUniversalTime() }
            };
            if (TimeWarp.CurrentRateIndex != index)
            {
                call.Fail("KSP allows a lower rate only here: " + Json.Write(answer));
                yield break;
            }
            call.Text(answer);
        }

        private static IEnumerator WarpTo(ToolCall call)
        {
            TimeWarp warp = TimeWarp.fetch;
            if (warp == null || Planetarium.fetch == null)
            {
                call.Fail("No time warp in this scene");
                yield break;
            }
            double now = Planetarium.GetUniversalTime();
            double ut = call.Has("ut") ? call.Number("ut") : now + call.Number("seconds");
            if (double.IsNaN(ut) || ut <= now)
            {
                call.Fail("ut or seconds: a time ahead of the game's, which is " + now);
                yield break;
            }
            warp.WarpTo(ut);

            // KSP starts the warp at its next frame, and ends it once back to normal time; it gives up on its
            // own when it cannot warp at all. A warp cut short, by the time limit or by a cancellation, is
            // stopped.
            bool over = false;
            try
            {
                float start = Time.realtimeSinceStartup;
                do
                {
                    yield return null;
                    if (Time.realtimeSinceStartup - start > 600f)
                    {
                        call.Fail("Still warping after 10 minutes: stopped");
                        yield break;
                    }
                }
                while (warp.setAutoWarp || (bool)WarpAutoEngaged.GetValue(warp));
                over = true;
            }
            finally
            {
                if (!over && warp != null)
                {
                    warp.CancelAutoWarp();
                }
            }
            call.Text(GameState.State());
        }

        private static IEnumerator SetPause(ToolCall call)
        {
            FlightDriver.SetPause(call.Bool("paused"), false);
            yield return null;
            call.Text(new Dictionary<string, object> { { "paused", FlightDriver.Pause } });
        }
    }
}
