using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// Tools that drive a rover with its wheel throttle, steering and brakes (see <see cref="Wheels"/>), and
    /// stop it. A drive ends early, the rover braked, when the active vessel changes, the flight scene
    /// closes, the rover tips over, or its call is cancelled.
    /// </summary>
    internal static class DriveTools
    {
        private const string DriveTool = "drive";
        private const string DriveToTool = "drive_to";

        // Within this distance of its target, drive_to steers no more: steering then only makes the rover
        // wander around the point. A target behind the rover counts as reached within it.
        private const double HoldStraightWithin = 3.0;

        // Times drive_to stops to choose its direction again before giving up.
        private const int MaxTurnRounds = 4;

        public static IEnumerable<Tool> All()
        {
            yield return new Tool("set_controls",
                "Holds the wheel throttle and steering of the active vessel at the given values (−1 to 1) until " +
                "changed; null or left out gives them back to the player. Steering: positive turns left. Also " +
                "sets the brakes when given.",
                Schema.Object(
                    Schema.P("wheel_throttle", "number", "−1 (reverse) to 1 (forward)"),
                    Schema.P("wheel_steer", "number", "−1 to 1, positive to the left"),
                    Schema.P("brakes", "boolean", "brakes on or off")),
                SetControls);
            yield return new Tool(DriveTool,
                "Drives the active rover along a heading at a given speed, steering to hold it, then stops with " +
                "the brakes on and waits for it to be still. Stops after a distance, or when the floating origin " +
                "shifts if until_shift is true, whichever comes first. The rover's front is where its wheels " +
                "drive it with a positive throttle. Fails, the rover braked, when it tips over, runs away at " +
                "twice the speed asked, or when the active vessel changes; stop_drive stops it.",
                Schema.Object(
                    Schema.P("heading", "number", "degrees from north towards east", true),
                    Schema.P("speed", "number", "metres per second (default 3)"),
                    Schema.P("distance", "number", "metres to drive (default 1000)"),
                    Schema.P("until_shift", "boolean", "stop as soon as the floating origin shifts")),
                Drive);
            yield return new Tool(DriveToTool,
                "Drives the active rover to a latitude and longitude and stops there with the brakes on, still. " +
                "Drives forward or in reverse, whichever faces the target; when the target falls behind the " +
                "rover further than 3 m away, stops and chooses again. Steers less the faster it goes. Stops " +
                "after 300 s. For a precise stop, start from a few metres away, roughly lined up with the target. " +
                "Fails, the rover braked, when it tips over, runs away at twice the speed asked, or when the " +
                "active vessel changes; stop_drive stops it.",
                Schema.Object(
                    Schema.P("latitude", "number", "degrees", true),
                    Schema.P("longitude", "number", "degrees", true),
                    Schema.P("speed", "number", "top speed in metres per second (default 2)"),
                    Schema.P("tolerance", "number", "metres from the target to stop at (default 0.2)")),
                DriveTo);
            yield return new Tool("stop_drive",
                "Stops the rover: ends a drive or drive_to still running, whose call then fails, or else lets go " +
                "of the wheels held by set_controls; puts the brakes on and waits for the rover to be still, " +
                "unless the game is paused. Can be called while another tool runs.",
                Schema.Object(), StopDrive) { Concurrent = true };
        }

        private static IEnumerator SetControls(ToolCall call)
        {
            if (!Require.ActiveVessel(call))
            {
                yield break;
            }
            Wheels.Hold(call.Has("wheel_throttle") ? (float)call.Number("wheel_throttle") : float.NaN,
                call.Has("wheel_steer") ? (float)call.Number("wheel_steer") : float.NaN);
            if (call.Has("brakes"))
            {
                Wheels.Brakes(FlightGlobals.ActiveVessel, call.Bool("brakes"));
            }
            yield return null;
            call.Text(GameState.State());
        }

        private static IEnumerator StopDrive(ToolCall call)
        {
            List<MainThread.Job> drives = MainThread.Cancel(j => j.Name == DriveTool || j.Name == DriveToTool,
                "stopped by stop_drive");
            // The drives let go of the wheels as they end; at once, in case they do not end now.
            Wheels.Release();
            Vessel v = FlightGlobals.ActiveVessel;
            if (v != null)
            {
                Wheels.Brakes(v, true);
            }
            List<object> stopped = drives.ConvertAll(j => (object)j.Name);

            // Paused, the drives end and the rover stops as the game resumes.
            if (!FlightDriver.Pause)
            {
                yield return Waits.Until(call, () => drives.TrueForAll(j => j.IsDone), 10f,
                    "The drive had not stopped after 10 seconds");
                if (call.IsError)
                {
                    yield break;
                }
                if (v != null && v == FlightGlobals.ActiveVessel)
                {
                    yield return StopAndSettle(v);
                }
            }
            Dictionary<string, object> state = GameState.State();
            state["stopped"] = stopped;
            call.Text(state);
        }

        // Steering towards a heading error, in degrees: proportional, saturated at 25 degrees, and at a
        // smaller command above 3 m/s, where the same command turns the rover harder. Positive wheelSteer turns
        // left, so a target to the right (positive error) needs a negative command; in reverse the wheels turn
        // the other way.
        private static float SteerFor(double errorDegrees, bool reverse, double speed)
        {
            float limit = speed <= 3.0 ? 1f : (float)(3.0 / speed);
            float steer = Mathf.Clamp((float)(-errorDegrees / 25.0), -limit, limit);
            return reverse ? -steer : steer;
        }

        // Throttle to bring the speed along the direction of travel to the wanted one.
        private static float ThrottleFor(double wanted, double actual, bool reverse)
        {
            float throttle = Mathf.Clamp((float)((wanted - actual) * 0.5 + wanted * 0.15), 0f, 1f);
            return reverse ? -throttle : throttle;
        }

        // The direction a positive wheel throttle drives the rover in, in world space. The wheel motors drive
        // along the up axis of the control point, or along its forward axis when the up axis stands upright,
        // as on a part made to face up in the VAB (ModuleWheelMotor.GetMotorOrientationSign): of the two, the
        // one closer to the ground.
        private static Vector3d Forward(Vessel v)
        {
            Transform reference = v.ReferenceTransform;
            Vector3d up = (v.CoMD - v.mainBody.position).normalized;
            return Math.Abs(Vector3d.Dot(reference.up, up)) <= Math.Abs(Vector3d.Dot(reference.forward, up))
                ? (Vector3d)reference.up
                : (Vector3d)reference.forward;
        }

        // Speed along the rover's forward direction, positive forward.
        private static double ForwardSpeed(Vessel v)
        {
            return Vector3d.Dot(v.srf_velocity, Forward(v));
        }

        // The direction the rover faces, in degrees from north towards east.
        private static double Heading(Vessel v)
        {
            return Geo.Heading(v, Forward(v));
        }

        // Brakes until the rover is still for two seconds, 30 seconds at most, then gives the wheels back. Ends
        // early when the rover is no longer the active vessel.
        private static IEnumerator StopAndSettle(Vessel v)
        {
            Wheels.Hold(0f, 0f);
            Wheels.Brakes(v, true);
            float stillSince = -1f;
            float start = Time.time;
            while (Time.time - start < 30f && v != null && v == FlightGlobals.ActiveVessel)
            {
                if (v.srfSpeed < 0.02)
                {
                    if (stillSince < 0f)
                    {
                        stillSince = Time.time;
                    }
                    else if (Time.time - stillSince > 2f)
                    {
                        break;
                    }
                }
                else
                {
                    stillSince = -1f;
                }
                yield return null;
            }
            Wheels.Release();
        }

        // Brakes until the rover is nearly still, 10 seconds at most, then takes the brakes off again.
        private static IEnumerator Halt(Vessel v)
        {
            Wheels.Hold(0f, 0f);
            Wheels.Brakes(v, true);
            float start = Time.time;
            while (Time.time - start < 10f && v.srfSpeed > 0.3)
            {
                yield return new WaitForFixedUpdate();
            }
            Wheels.Brakes(v, false);
        }

        // Ends a drive that did not reach its end: the wheels given back and the brakes on, whatever stopped it.
        private static void Abandon(Vessel v)
        {
            Wheels.Release();
            if (v != null)
            {
                Wheels.Brakes(v, true);
            }
        }

        private static IEnumerator Drive(ToolCall call)
        {
            if (!Require.ActiveVessel(call))
            {
                yield break;
            }
            Vessel v = FlightGlobals.ActiveVessel;
            double heading = Geo.Normalize(call.Number("heading"));
            double speed = call.Number("speed", 3.0);
            double distance = call.Number("distance", 1000.0);
            bool untilShift = call.Bool("until_shift");

            double startLat = v.latitude, startLon = v.longitude;
            int startShifts = GameState.Shifts;
            DriveWatch watch = new DriveWatch(v, speed);
            HeadingPid pid = new HeadingPid();
            string reason = "distance";
            string trouble = null;
            bool ended = false;
            try
            {
                Wheels.Brakes(v, false);
                while (true)
                {
                    trouble = watch.Trouble();
                    if (trouble != null)
                    {
                        break;
                    }
                    double n, e;
                    Geo.Offset(v, startLat, startLon, out n, out e);
                    double remaining = distance - Math.Sqrt(n * n + e * e);
                    if (remaining <= 0.0)
                    {
                        break;
                    }
                    if (untilShift && GameState.Shifts > startShifts)
                    {
                        reason = "shift";
                        break;
                    }

                    // Slow down as the end comes, so that the brakes stop the rover there rather than tens of
                    // metres on; brake when faster than that.
                    double along = ForwardSpeed(v);
                    double wanted = Math.Max(1.0, Math.Min(speed, remaining * 0.35));
                    bool tooFast = along > wanted * 1.2 + 0.3;
                    Wheels.Brakes(v, tooFast);
                    float steer = pid.Steer(Geo.Delta(Heading(v), heading), along, Time.fixedDeltaTime);
                    Wheels.Hold(tooFast ? 0f : ThrottleFor(wanted, along, false), steer);
                    yield return new WaitForFixedUpdate();
                }
                if (trouble == null || watch.StillDriving())
                {
                    yield return StopAndSettle(v);
                }
                ended = true;
            }
            finally
            {
                if (!ended)
                {
                    Abandon(v);
                }
            }
            if (trouble != null)
            {
                Abandon(v);
                call.Fail("Stopped: " + trouble + ". " + Json.Write(GameState.State()));
                yield break;
            }
            Dictionary<string, object> state = GameState.State();
            state["stoppedBecause"] = reason;
            state["floatingOrigin"] = GameState.FloatingOriginState();
            call.Text(state);
        }

        private static IEnumerator DriveTo(ToolCall call)
        {
            if (!Require.ActiveVessel(call))
            {
                yield break;
            }
            Vessel v = FlightGlobals.ActiveVessel;
            double lat = call.Number("latitude");
            double lon = call.Number("longitude");
            double topSpeed = call.Number("speed", 2.0);
            double tolerance = call.Number("tolerance", 0.2);
            double reachedWithin = Math.Max(HoldStraightWithin, tolerance);

            DriveWatch watch = new DriveWatch(v, topSpeed);
            float start = Time.time;
            bool reverse = false;
            bool decided = false;
            int turnRounds = 0;
            double maxSpeed = 0.0;
            double bestRemaining = double.MaxValue;
            float lastProgress = Time.time;
            double stallBoost = 0.0;
            string reason = "time limit";
            string trouble = null;
            double speedAtStop = 0.0;
            bool ended = false;
            try
            {
                Wheels.Brakes(v, false);
                while (Time.time - start < 300f)
                {
                    trouble = watch.Trouble();
                    if (trouble != null)
                    {
                        break;
                    }
                    double n, e;
                    Geo.Offset(v, lat, lon, out n, out e);
                    double distance = Math.Sqrt(n * n + e * e);
                    double bearing = Geo.Normalize(Math.Atan2(e, n) * 180.0 / Math.PI);
                    double heading = Heading(v);

                    // Forward or reverse, chosen from where the target lies: at the start, and again whenever
                    // the target falls behind.
                    if (!decided)
                    {
                        reverse = Math.Abs(Geo.Delta(heading, bearing)) > 90.0;
                        decided = true;
                    }
                    double travel = reverse ? Geo.Normalize(heading + 180.0) : heading;
                    double error = Geo.Delta(travel, bearing);

                    // Distance left along the direction of travel: negative once the target is behind.
                    double remaining = distance * Math.Cos(error * Math.PI / 180.0);
                    if (remaining <= tolerance && distance <= reachedWithin)
                    {
                        reason = "arrived";
                        break;
                    }
                    if (remaining < 0.0)
                    {
                        // Behind further away: the rover turned round, or went past it. Stop, then choose again.
                        if (++turnRounds > MaxTurnRounds)
                        {
                            reason = "target behind " + MaxTurnRounds + " times";
                            break;
                        }
                        yield return Halt(v);
                        decided = false;
                        bestRemaining = double.MaxValue;
                        lastProgress = Time.time;
                        stallBoost = 0.0;
                        continue;
                    }

                    double along = reverse ? -ForwardSpeed(v) : ForwardSpeed(v);
                    maxSpeed = Math.Max(maxSpeed, along);
                    float steer = distance > HoldStraightWithin ? SteerFor(error, reverse, Math.Abs(along)) : 0f;
                    // Stalled against a small step, the lip of a runway say: no progress for three seconds
                    // raises the lowest speed asked, until the rover gets over it.
                    if (remaining < bestRemaining - 0.05)
                    {
                        bestRemaining = remaining;
                        lastProgress = Time.time;
                    }
                    else if (Time.time - lastProgress > 3f)
                    {
                        stallBoost = Math.Min(1.5, stallBoost + 0.3);
                        lastProgress = Time.time;
                    }
                    double wanted = Math.Max(0.15 + stallBoost, Math.Min(topSpeed, remaining * 0.3));

                    // Too fast for what is left: brake rather than only cut the throttle, the wheels alone
                    // roll on for metres.
                    bool tooFast = along > wanted * 1.3 + 0.1;
                    Wheels.Brakes(v, tooFast);
                    Wheels.Hold(tooFast ? 0f : ThrottleFor(wanted, along, reverse), steer);
                    yield return new WaitForFixedUpdate();
                }
                if (trouble == null || watch.StillDriving())
                {
                    speedAtStop = reverse ? -ForwardSpeed(v) : ForwardSpeed(v);
                    yield return StopAndSettle(v);
                }
                ended = true;
            }
            finally
            {
                if (!ended)
                {
                    Abandon(v);
                }
            }
            if (trouble != null)
            {
                Abandon(v);
                call.Fail("Stopped: " + trouble + ". " + Json.Write(GameState.State()));
                yield break;
            }

            double fn, fe;
            Geo.Offset(v, lat, lon, out fn, out fe);
            Dictionary<string, object> state = GameState.State();
            state["missedBy"] = new Dictionary<string, object>
            {
                { "metres", Math.Sqrt(fn * fn + fe * fe) },
                { "north", -fn },
                { "east", -fe }
            };
            state["stoppedBecause"] = reason;
            state["reverse"] = reverse;
            state["turnRounds"] = Math.Min(turnRounds, MaxTurnRounds);
            state["maxSpeed"] = maxSpeed;
            state["speedAtStop"] = speedAtStop;
            call.Text(state);
        }

        /// <summary>
        /// Tells why a drive must end before its end: the flight scene closed, the active vessel changed, the
        /// rover was destroyed, tipped over, or runs away at more than twice the speed asked for. Tipped over is
        /// counted from how the rover stood when the drive started.
        /// </summary>
        private sealed class DriveWatch
        {
            // Beyond this angle between the rover's up, as it stood at the start, and the local vertical, it no
            // longer stands on its wheels.
            private const float TippedDegrees = 60f;

            private readonly Vessel _vessel;
            private readonly Vector3 _upInVessel;
            private readonly double _speedLimit;

            /// <summary>A watch over the drive of <paramref name="v"/>, asked to go at <paramref name="speed"/> m/s at most.</summary>
            public DriveWatch(Vessel v, double speed)
            {
                _vessel = v;
                _upInVessel = v.ReferenceTransform.InverseTransformDirection(Up(v));
                _speedLimit = Math.Max(2.0 * speed, speed + 3.0);
            }

            /// <summary>Whether the rover is still the active vessel of the flight, not destroyed.</summary>
            public bool StillDriving()
            {
                return HighLogic.LoadedSceneIsFlight && _vessel != null && _vessel.state != Vessel.State.DEAD
                    && FlightGlobals.ActiveVessel == _vessel;
            }

            /// <summary>Why the drive must end, or null when it may go on.</summary>
            public string Trouble()
            {
                if (!HighLogic.LoadedSceneIsFlight)
                {
                    return "the flight scene closed";
                }
                if (_vessel == null || _vessel.state == Vessel.State.DEAD)
                {
                    return "the rover was destroyed";
                }
                if (FlightGlobals.ActiveVessel != _vessel)
                {
                    return "the active vessel changed";
                }
                float tilt = Vector3.Angle(_vessel.ReferenceTransform.TransformDirection(_upInVessel), Up(_vessel));
                if (tilt > TippedDegrees)
                {
                    return "the rover tipped over (" + tilt.ToString("F0") + "° from how it stood)";
                }
                if (_vessel.srfSpeed > _speedLimit)
                {
                    return "the rover ran away (" + _vessel.srfSpeed.ToString("F1") + " m/s over the ground, " +
                        ForwardSpeed(_vessel).ToString("F1") + " m/s of it forward)";
                }
                return null;
            }

            // The local vertical at the vessel, in world space.
            private static Vector3 Up(Vessel v)
            {
                return (v.CoMD - v.mainBody.position).normalized;
            }
        }
    }
}
