using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// Steers a rover towards a heading: asks for a rate of turn that brings the heading round smoothly, never
    /// more than the rover can turn at its speed without tipping over, and gives the wheels the steering that
    /// yields that rate. How sharply the rover turns for a given steering, at each speed, it learns as it drives,
    /// and remembers for that vessel until the game is closed, from one drive to the next.
    /// </summary>
    internal sealed class RoverSteering
    {
        // Seconds the heading error is asked to close in, the rate of turn asked being the error over this time:
        // this much at rest, and this much more per metre per second, the wheels answering the steering more
        // slowly the faster the rover goes.
        private const double CloseIn = 1.2;
        private const double CloseInPerSpeed = 0.05;

        // How much of the gap between the rate of turn asked and the rate measured is added to the command.
        private const double RateGain = 0.6;

        // The sideways acceleration a turn may ask for, as a fraction of the local gravity: well under what tips
        // a rover over, its centre of mass high on soft suspension.
        private const double MaxLateralGees = 0.25;

        // Seconds the measured rate of turn is smoothed over: the rover jolts on bumpy ground.
        private const double RateSmoothing = 0.1;

        // Seconds the rate of turn lags the steering command: the wheels turn at a finite rate, and the rover
        // takes time to swing round.
        private const double Lag = 0.25;

        private readonly Vessel _vessel;
        private readonly bool _reverse;
        private readonly TurnModel _model;
        private double _rate = double.NaN;
        private double _lagged;

        /// <summary>
        /// Steering for <paramref name="v"/>, driving forward or, when <paramref name="reverse"/>, backing away.
        /// </summary>
        public RoverSteering(Vessel v, bool reverse)
        {
            _vessel = v;
            _reverse = reverse;
            _model = TurnModel.For(v);
        }

        /// <summary>The rate of turn measured at the last call, degrees per second, positive to the right.</summary>
        public double Rate => double.IsNaN(_rate) ? 0.0 : _rate;

        /// <summary>The rate of turn asked for at the last call, degrees per second, positive to the right.</summary>
        public double RateWanted { get; private set; }

        /// <summary>How sharply the rover turned for a full steering, at the speed of the last call: the
        /// curvature of its path, per metre, as learned so far.</summary>
        public double Curvature { get; private set; }

        /// <summary>
        /// The wheel steering, −1 to 1 and positive to the left, as <see cref="FlightCtrlState.wheelSteer"/>
        /// takes it, to bring the direction of travel round by <paramref name="errorDegrees"/> (positive to the
        /// right), at <paramref name="speed"/> metres per second along it, <paramref name="dt"/> seconds after
        /// the last call.
        /// </summary>
        public float Steer(double errorDegrees, double speed, double dt)
        {
            double v = Math.Max(Math.Abs(speed), 0.5);
            double rate = MeasureRate(dt);

            // What the last commands did teaches how sharply the rover turns at this speed.
            _model.Learn(_reverse, v, _lagged, rate * Mathf.Deg2Rad);
            double curvature = _model.Curvature(_reverse, v);
            Curvature = curvature;

            // The rate of turn asked: the error closed in a fixed time, within what the rover can turn at this
            // speed and what keeps it on its wheels.
            double gravity = FlightGlobals.getGeeForceAtPosition(_vessel.CoMD).magnitude;
            double maxRate = Math.Min(MaxLateralGees * gravity / v, 0.9 * curvature * v) * Mathf.Rad2Deg;
            double wanted = Math.Max(-maxRate, Math.Min(maxRate, errorDegrees / (CloseIn + CloseInPerSpeed * v)));
            RateWanted = wanted;

            // The command that gives that rate, corrected by how far the rate measured is from it.
            double perCommand = curvature * v * Mathf.Rad2Deg;
            double command = (wanted + RateGain * (wanted - rate)) / perCommand;
            command = Math.Max(-1.0, Math.Min(1.0, command));
            _lagged += (command - _lagged) * Math.Min(1.0, dt / Lag);

            // Positive wheelSteer turns left: a turn to the right needs a negative command going forward. In
            // reverse the same command swings the direction of travel the other way.
            return (float)(_reverse ? command : -command);
        }

        // The rate at which the rover's heading turns, in degrees per second, positive to the right, smoothed.
        private double MeasureRate(double dt)
        {
            Rigidbody rb = _vessel.rootPart != null ? _vessel.rootPart.rb : null;
            if (rb == null)
            {
                return Rate;
            }
            Vector3d up = (_vessel.CoMD - _vessel.mainBody.position).normalized;
            // Unity's frame is left-handed: a positive spin about the local vertical turns the heading clockwise
            // seen from above, from north towards east.
            double raw = Vector3d.Dot(rb.angularVelocity, up) * Mathf.Rad2Deg;
            _rate = double.IsNaN(_rate) ? raw : _rate + (raw - _rate) * Math.Min(1.0, dt / RateSmoothing);
            return _rate;
        }

        /// <summary>
        /// How sharply a vessel turns for a full steering command, driving forward and in reverse, at each
        /// speed: the curvature of its path per unit of command, learned from what it did.
        /// </summary>
        private sealed class TurnModel
        {
            // Upper bounds of the speed bands, in metres per second; the last band has none.
            private static readonly double[] Bands = { 1.5, 3.0, 5.0, 8.0, 12.0, 18.0, 26.0 };

            // Taken until the rover has turned at some speed: a full command bends its path to a radius of
            // about 3 metres, a sharp turn for a small rover. Too sharp is the safe side: the command is then
            // smaller than needed, and the rate measured makes up for it.
            private const double Assumed = 0.35;

            // The weight a sample keeps at each new one: the model follows a rover whose turning changes, a
            // part lost or the ground getting softer, over a few seconds of turning.
            private const double Forget = 0.995;

            // What the sums must hold before a band is trusted: about a second of turning at a quarter of the
            // command and 2 m/s.
            private const double Trusted = 10.0;

            private static readonly Dictionary<uint, TurnModel> Models = new Dictionary<uint, TurnModel>();

            private readonly double[,] _xy = new double[2, Bands.Length + 1];
            private readonly double[,] _xx = new double[2, Bands.Length + 1];

            /// <summary>The model of a vessel, the same one at each drive until the game is closed.</summary>
            public static TurnModel For(Vessel v)
            {
                TurnModel model;
                if (!Models.TryGetValue(v.persistentId, out model))
                {
                    model = new TurnModel();
                    Models[v.persistentId] = model;
                }
                return model;
            }

            /// <summary>
            /// Learns from one step: at <paramref name="speed"/> m/s, the command that acts now
            /// (<paramref name="command"/>, positive to the right) turned the rover at <paramref name="rate"/>
            /// radians per second.
            /// </summary>
            public void Learn(bool reverse, double speed, double command, double rate)
            {
                // A command too small says nothing but noise.
                if (Math.Abs(command) < 0.05 || speed < 0.5)
                {
                    return;
                }
                // Least squares of the rate against command times speed, the slope being the curvature.
                int d = reverse ? 1 : 0;
                int b = Band(speed);
                double x = command * speed;
                _xy[d, b] = Forget * _xy[d, b] + x * rate;
                _xx[d, b] = Forget * _xx[d, b] + x * x;
            }

            /// <summary>The curvature of the path per unit of command at <paramref name="speed"/> m/s, per metre.</summary>
            public double Curvature(bool reverse, double speed)
            {
                int d = reverse ? 1 : 0;
                int b = Band(speed);
                // A band not learned yet takes the nearest slower one: the steering turns less the faster the
                // rover goes, so a slower band errs on the side of too small a command. Failing that, the nearest
                // faster one, the rate measured making up for it.
                for (int i = b; i >= 0; i--)
                {
                    if (_xx[d, i] >= Trusted)
                    {
                        return Learned(d, i);
                    }
                }
                for (int i = b + 1; i <= Bands.Length; i++)
                {
                    if (_xx[d, i] >= Trusted)
                    {
                        return Learned(d, i);
                    }
                }
                return Assumed;
            }

            private double Learned(int d, int b)
            {
                return Math.Max(0.01, Math.Min(1.0, _xy[d, b] / _xx[d, b]));
            }

            private static int Band(double speed)
            {
                int b = 0;
                while (b < Bands.Length && speed > Bands[b])
                {
                    b++;
                }
                return b;
            }
        }
    }

    /// <summary>
    /// Holds a rover's speed with the wheel throttle: proportional to the shortfall, plus what it takes to keep
    /// rolling at that speed, learned as it drives.
    /// </summary>
    internal sealed class SpeedHold
    {
        private const double Gain = 0.4;
        private const double Learning = 0.25;
        private double _cruise;

        /// <summary>
        /// The wheel throttle magnitude, 0 to 1, to bring <paramref name="actual"/> to <paramref name="wanted"/>
        /// metres per second, <paramref name="dt"/> seconds after the last call; 0 while
        /// <paramref name="braking"/>.
        /// </summary>
        public float Throttle(double wanted, double actual, double dt, bool braking)
        {
            if (braking)
            {
                return 0f;
            }
            double shortfall = wanted - actual;
            double throttle = Gain * shortfall + _cruise;
            // Learn the throttle that holds the speed only while the command is not at a limit: otherwise the
            // shortfall says nothing of it, and the throttle would overshoot once the speed is reached.
            if (throttle > 0.0 && throttle < 1.0)
            {
                _cruise = Math.Max(0.0, Math.Min(1.0, _cruise + Learning * shortfall * dt));
            }
            return Mathf.Clamp((float)throttle, 0f, 1f);
        }
    }
}
