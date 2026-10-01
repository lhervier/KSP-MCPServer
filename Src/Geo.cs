using System;
using UnityEngine;

namespace com.github.lhervier.ksp.mcpserver
{
    /// <summary>Directions and distances on the surface around a vessel, in its local north/east frame.</summary>
    internal static class Geo
    {
        /// <summary>The local north and east unit vectors at a vessel, in world space.</summary>
        public static void NorthEast(Vessel v, out Vector3d north, out Vector3d east)
        {
            // Taken from two nearby points of the surface rather than from the body's axes: no handedness to
            // get right, and the same frame latitude and longitude are given in.
            const double step = 1e-4;
            CelestialBody body = v.mainBody;
            Vector3d here = body.GetWorldSurfacePosition(v.latitude, v.longitude, v.altitude);
            north = (body.GetWorldSurfacePosition(v.latitude + step, v.longitude, v.altitude) - here).normalized;
            east = (body.GetWorldSurfacePosition(v.latitude, v.longitude + step, v.altitude) - here).normalized;
        }

        /// <summary>The direction the vessel's control point faces, in degrees from north towards east.</summary>
        public static double Heading(Vessel v)
        {
            Vector3d north, east;
            NorthEast(v, out north, out east);
            Vector3d forward = v.ReferenceTransform.up;
            return Normalize(Math.Atan2(Vector3d.Dot(forward, east), Vector3d.Dot(forward, north)) * 180.0 / Math.PI);
        }

        /// <summary>
        /// Where a surface point lies from the vessel: <paramref name="northMetres"/> and
        /// <paramref name="eastMetres"/> along the local axes.
        /// </summary>
        public static void Offset(Vessel v, double latitude, double longitude, out double northMetres, out double eastMetres)
        {
            Vector3d north, east;
            NorthEast(v, out north, out east);
            CelestialBody body = v.mainBody;
            Vector3d delta = body.GetWorldSurfacePosition(latitude, longitude, v.altitude)
                - body.GetWorldSurfacePosition(v.latitude, v.longitude, v.altitude);
            northMetres = Vector3d.Dot(delta, north);
            eastMetres = Vector3d.Dot(delta, east);
        }

        /// <summary>An angle brought into [0, 360).</summary>
        public static double Normalize(double degrees)
        {
            degrees %= 360.0;
            return degrees < 0 ? degrees + 360.0 : degrees;
        }

        /// <summary>The signed difference <paramref name="to"/> − <paramref name="from"/>, in (−180, 180].</summary>
        public static double Delta(double from, double to)
        {
            double d = Normalize(to - from);
            return d > 180.0 ? d - 360.0 : d;
        }
    }
}
