using System;
using System.Collections;
using System.Collections.Generic;
using KSP.UI.Screens;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>Tools that fly the active vessel as its pilot does: throttle, SAS, staging.</summary>
    internal static class FlightTools
    {
        public static IEnumerable<Tool> All()
        {
            yield return new Tool("set_flight",
                "Sets the main throttle of the active vessel (0 to 1, as Shift, Ctrl, Z and X do), turns SAS on or " +
                "off, and chooses its mode; values left out are kept. Answers with the state of the game, the " +
                "throttle and the SAS mode included.",
                Schema.Object(
                    Schema.P("throttle", "number", "0 (cut) to 1 (full)"),
                    Schema.P("sas", "boolean", "SAS on or off"),
                    Schema.P("sas_mode", "string",
                        "StabilityAssist, Prograde, Retrograde, Normal, Antinormal, RadialIn, RadialOut, Target, " +
                        "AntiTarget, Maneuver")),
                SetFlight);
            yield return new Tool("stage",
                "Activates the next stage of the active vessel, as the space bar does. Answers with the stage now " +
                "current.",
                Schema.Object(), Stage);
        }

        private static IEnumerator SetFlight(ToolCall call)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null)
            {
                call.Fail("No active vessel");
                yield break;
            }
            if (call.Has("throttle"))
            {
                // What the throttle keys write: the state the game copies into the vessel's controls every frame.
                FlightInputHandler.state.mainThrottle = UnityEngine.Mathf.Clamp01((float)call.Number("throttle"));
            }
            if (call.Has("sas"))
            {
                v.ActionGroups.SetGroup(KSPActionGroup.SAS, call.Bool("sas"));
            }
            // The SAS takes its mode only once it is on: wait a frame after turning it on.
            yield return null;
            if (call.Has("sas_mode"))
            {
                VesselAutopilot.AutopilotMode mode;
                try
                {
                    mode = (VesselAutopilot.AutopilotMode)Enum.Parse(typeof(VesselAutopilot.AutopilotMode),
                        call.String("sas_mode"), true);
                }
                catch (ArgumentException)
                {
                    call.Fail("Unknown SAS mode " + call.String("sas_mode"));
                    yield break;
                }
                if (!v.Autopilot.CanSetMode(mode))
                {
                    call.Fail("The SAS of this vessel cannot hold " + mode + " now");
                    yield break;
                }
                v.Autopilot.SetMode(mode);
                yield return null;
            }
            Dictionary<string, object> state = GameTools.State();
            state["throttle"] = (double)FlightInputHandler.state.mainThrottle;
            state["sasMode"] = v.Autopilot.Mode.ToString();
            call.Text(state);
        }

        private static IEnumerator Stage(ToolCall call)
        {
            if (FlightGlobals.ActiveVessel == null)
            {
                call.Fail("No active vessel");
                yield break;
            }
            StageManager.ActivateNextStage();
            yield return null;
            call.Text(new Dictionary<string, object> { { "currentStage", StageManager.CurrentStage } });
        }
    }
}
