using System;

namespace HighProject.MapCore
{
    // A point in the local map plane, in metres: X = east, Z = north (the x and z axes of the Unity scene).
    public readonly struct Point2d
    {
        public readonly double X;
        public readonly double Z;

        public Point2d(double x, double z)
        {
            X = x;
            Z = z;
        }

        public override string ToString() => FormattableString.Invariant($"({X:F2}, {Z:F2})");
    }
}
