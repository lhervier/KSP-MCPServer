using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// Tools that drive a rover: wheel throttle and steering given through the vessel's fly-by-wire, the way
    /// a player's keys are, and the brakes action group.
    /// </summary>
    internal static class DriveTools
    {
        // The commands given to the wheels; NaN leaves the player's own input alone.
        private static float _throttle = float.NaN;
        private static float _steer = float.NaN;
        private static Vessel _wired;

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
            yield return new Tool("drive",
                "Drives the active rover along a heading at a given speed, steering to hold it, then stops with " +
                "the brakes on and waits for it to be still. Stops after a distance, or when the floating origin " +
                "shifts if until_shift is true, whichever comes first.",
                Schema.Object(
                    Schema.P("heading", "number", "degrees from north towards east", true),
                    Schema.P("speed", "number", "metres per second (default 3)"),
                    Schema.P("distance", "number", "metres to drive (default 1000)"),
                    Schema.P("until_shift", "boolean", "stop as soon as the floating origin shifts")),
                Drive);
            yield return new Tool("drive_to",
                "Drives the active rover to a latitude and longitude and stops there with the brakes on, still. " +
                "Drives forward or in reverse, whichever faces the target. For a precise stop, start from a few " +
                "metres away, roughly lined up with the target.",
                Schema.Object(
                    Schema.P("latitude", "number", "degrees", true),
                    Schema.P("longitude", "number", "degrees", true),
                    Schema.P("speed", "number", "top speed in metres per second (default 2)"),
                    Schema.P("tolerance", "number", "metres from the target to stop at (default 0.2)")),
                DriveTo);
        }

        private static void Wire(Vessel v)
        {
            if (ReferenceEquals(_wired, v))
            {
                return;
            }
            if (_wired != null)
            {
                _wired.OnFlyByWire -= FlyByWire;
            }
            _wired = v;
            if (v != null)
            {
                v.OnFlyByWire += FlyByWire;
            }
        }

        private static void FlyByWire(FlightCtrlState s)
        {
            if (!float.IsNaN(_throttle))
            {
                s.wheelThrottle = _throttle;
            }
            if (!float.IsNaN(_steer))
            {
                s.wheelSteer = _steer;
            }
        }

        private static void Command(float throttle, float steer)
        {
            Wire(FlightGlobals.ActiveVessel);
            _throttle = throttle;
            _steer = steer;
        }

        private static void Release()
        {
            _throttle = float.NaN;
            _steer = float.NaN;
        }

        private static void Brakes(Vessel v, bool on)
        {
            v.ActionGroups.SetGroup(KSPActionGroup.Brakes, on);
        }

        private static IEnumerator SetControls(ToolCall call)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null)
            {
                call.Fail("No active vessel");
                yield break;
            }
            Command(call.Has("wheel_throttle") ? (float)call.Number("wheel_throttle") : float.NaN,
                call.Has("wheel_steer") ? (float)call.Number("wheel_steer") : float.NaN);
            if (call.Has("brakes"))
            {
                Brakes(v, call.Bool("brakes"));
            }
            yield return null;
            call.Text(GameTools.State());
        }

        // Steering towards a heading error, in degrees: proportional, saturated at 25 degrees. Positive
        // wheelSteer turns left, so a target to the right (positive error) needs a negative command; in
        // reverse the wheels turn the other way.
        private static float SteerFor(double errorDegrees, bool reverse)
        {
            float steer = Mathf.Clamp((float)(-errorDegrees / 25.0), -1f, 1f);
            return reverse ? -steer : steer;
        }

        // Throttle to bring the speed along the direction of travel to the wanted one.
        private static float ThrottleFor(double wanted, double actual, bool reverse)
        {
            float throttle = Mathf.Clamp((float)((wanted - actual) * 0.5 + wanted * 0.15), 0f, 1f);
            return reverse ? -throttle : throttle;
        }

        // Speed along the vessel's facing, positive forward.
        private static double ForwardSpeed(Vessel v)
        {
            return Vector3d.Dot(v.srf_velocity, v.ReferenceTransform.up);
        }

        private static IEnumerator StopAndSettle(Vessel v)
        {
            Command(0f, 0f);
            Brakes(v, true);
            float stillSince = -1f;
            float start = Time.time;
            while (Time.time - start < 30f)
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
            Release();
        }

        private static IEnumerator Drive(ToolCall call)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null)
            {
                call.Fail("No active vessel");
                yield break;
            }
            double heading = Geo.Normalize(call.Number("heading"));
            double speed = call.Number("speed", 3.0);
            double distance = call.Number("distance", 1000.0);
            bool untilShift = call.Bool("until_shift");

            double startLat = v.latitude, startLon = v.longitude;
            int startShifts = GameTools.Shifts;
            Brakes(v, false);
            string reason = "distance";
            HeadingPid pid = new HeadingPid();
            while (true)
            {
                double n, e;
                Geo.Offset(v, startLat, startLon, out n, out e);
                double remaining = distance - Math.Sqrt(n * n + e * e);
                if (remaining <= 0.0)
                {
                    break;
                }
                if (untilShift && GameTools.Shifts > startShifts)
                {
                    reason = "shift";
                    break;
                }

                // Slow down as the end comes, so that the brakes stop the rover there rather than tens of
                // metres on; brake when faster than that.
                double along = ForwardSpeed(v);
                double wanted = Math.Max(1.0, Math.Min(speed, remaining * 0.35));
                bool tooFast = along > wanted * 1.2 + 0.3;
                Brakes(v, tooFast);
                float steer = pid.Steer(Geo.Delta(Geo.Heading(v), heading), along, Time.fixedDeltaTime);
                Command(tooFast ? 0f : ThrottleFor(wanted, along, false), steer);
                yield return new WaitForFixedUpdate();
            }
            yield return StopAndSettle(v);
            Dictionary<string, object> state = GameTools.State();
            state["stoppedBecause"] = reason;
            state["floatingOrigin"] = GameTools.FloatingOriginState();
            call.Text(state);
        }

        /// <summary>
        /// Holds a heading with the wheel steering: proportional to the heading error, with a gain that falls
        /// with speed (the same steering turns a fast rover much more), derivative to damp the swing from
        /// side to side, and a small bounded integral for a steady pull to one side; nothing at all inside a
        /// small dead band.
        /// </summary>
        private sealed class HeadingPid
        {
            private double _integral;
            private double _lastError = double.NaN;

            // Errors under this are left alone: the heading read every physics step quivers on bumpy ground,
            // and chasing that quiver is what makes a fast rover weave.
            private const double DeadBandDegrees = 2.0;

            public float Steer(double errorDegrees, double speed, double dt)
            {
                // Beyond the dead band, only the part above it, so that the command does not jump at its edge.
                errorDegrees = Math.Abs(errorDegrees) <= DeadBandDegrees
                    ? 0.0
                    : errorDegrees - Math.Sign(errorDegrees) * DeadBandDegrees;
                double kp = 0.04 / (1.0 + Math.Abs(speed) / 4.0);
                const double ki = 0.004;
                const double kd = 0.015;
                _integral = Math.Max(-25.0, Math.Min(25.0, _integral + errorDegrees * dt));
                double derivative = double.IsNaN(_lastError) || dt <= 0.0 ? 0.0 : Geo.Delta(_lastError, errorDegrees) / dt;
                _lastError = errorDegrees;
                double u = kp * errorDegrees + ki * _integral + kd * derivative;
                // Positive wheelSteer turns left: a target to the right (positive error) needs a negative
                // command.
                return Mathf.Clamp((float)-u, -1f, 1f);
            }
        }

        private static IEnumerator DriveTo(ToolCall call)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null)
            {
                call.Fail("No active vessel");
                yield break;
            }
            double lat = call.Number("latitude");
            double lon = call.Number("longitude");
            double topSpeed = call.Number("speed", 2.0);
            double tolerance = call.Number("tolerance", 0.2);

            Brakes(v, false);
            float start = Time.time;
            bool reverse = false;
            bool decided = false;
            double remaining = double.NaN;
            double maxSpeed = 0.0;
            double bestRemaining = double.MaxValue;
            float lastProgress = Time.time;
            double stallBoost = 0.0;
            while (Time.time - start < 300f)
            {
                double n, e;
                Geo.Offset(v, lat, lon, out n, out e);
                double distance = Math.Sqrt(n * n + e * e);
                double bearing = Geo.Normalize(Math.Atan2(e, n) * 180.0 / Math.PI);
                double heading = Geo.Heading(v);

                // Forward or reverse, chosen once at the start from where the target lies.
                if (!decided)
                {
                    reverse = Math.Abs(Geo.Delta(heading, bearing)) > 90.0;
                    decided = true;
                }
                double travel = reverse ? Geo.Normalize(heading + 180.0) : heading;
                double error = Geo.Delta(travel, bearing);

                // Distance left along the direction of travel: negative once the target is behind.
                remaining = distance * Math.Cos(error * Math.PI / 180.0);
                if (remaining <= tolerance)
                {
                    break;
                }

                // Close in, steering only makes the rover wander around the point: hold straight.
                float steer = distance > 3.0 ? SteerFor(error, reverse) : 0f;
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
                double along = reverse ? -ForwardSpeed(v) : ForwardSpeed(v);
                maxSpeed = Math.Max(maxSpeed, along);

                // Too fast for what is left: brake rather than only cut the throttle, the wheels alone
                // roll on for metres.
                bool tooFast = along > wanted * 1.3 + 0.1;
                Brakes(v, tooFast);
                Command(tooFast ? 0f : ThrottleFor(wanted, along, reverse), steer);
                yield return new WaitForFixedUpdate();
            }
            double speedAtStop = reverse ? -ForwardSpeed(v) : ForwardSpeed(v);
            yield return StopAndSettle(v);

            double fn, fe;
            Geo.Offset(v, lat, lon, out fn, out fe);
            Dictionary<string, object> state = GameTools.State();
            state["missedBy"] = new Dictionary<string, object>
            {
                { "metres", Math.Sqrt(fn * fn + fe * fe) },
                { "north", -fn },
                { "east", -fe }
            };
            state["reverse"] = reverse;
            state["maxSpeed"] = maxSpeed;
            state["speedAtStop"] = speedAtStop;
            call.Text(state);
        }
    }
}
