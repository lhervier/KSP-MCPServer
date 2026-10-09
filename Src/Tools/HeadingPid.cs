using System;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>
    /// Holds a heading with the wheel steering: proportional to the heading error, with a gain that falls
    /// with speed (the same steering turns a fast rover much more), derivative to damp the swing from
    /// side to side, and a small bounded integral for a steady pull to one side; nothing at all inside a
    /// small dead band.
    /// </summary>
    internal sealed class HeadingPid
    {
        private double _integral;
        private double _lastError = double.NaN;

        // Errors under this are left alone: the heading read every physics step quivers on bumpy ground,
        // and chasing that quiver is what makes a fast rover weave.
        private const double DeadBandDegrees = 2.0;

        /// <summary>
        /// The wheel steering, −1 to 1 and positive to the left, for a heading error in degrees (positive when
        /// the heading wanted lies to the right), at a speed in metres per second, <paramref name="dt"/>
        /// seconds after the last call.
        /// </summary>
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
}
