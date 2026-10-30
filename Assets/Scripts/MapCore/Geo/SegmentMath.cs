using System;

namespace HighProject.MapCore
{
    public static class SegmentMath
    {
        // Shortest distance from a point to the segment a-b (not the infinite line).
        public static double DistanceToSegment(Point2d p, Point2d a, Point2d b)
        {
            double dx = b.X - a.X;
            double dz = b.Z - a.Z;
            double lengthSquared = dx * dx + dz * dz;
            double t = lengthSquared > 0.0 ? ((p.X - a.X) * dx + (p.Z - a.Z) * dz) / lengthSquared : 0.0;
            t = Math.Max(0.0, Math.Min(1.0, t));

            double ex = p.X - (a.X + t * dx);
            double ez = p.Z - (a.Z + t * dz);
            return Math.Sqrt(ex * ex + ez * ez);
        }
    }
}
