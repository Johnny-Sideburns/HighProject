using System;

namespace HighProject.MapCore
{
    // A closed polygon outline in local metres. The last point is NOT repeated: [a, b, c] means a-b-c-a.
    // NodeIds (optional) holds the OSM node id of each point, so later steps can match entrances to vertices.
    public sealed class Ring
    {
        public Point2d[] Points { get; }
        public long[] NodeIds { get; }

        public double MinX { get; }
        public double MinZ { get; }
        public double MaxX { get; }
        public double MaxZ { get; }

        // Positive when the points run counter-clockwise (x east, z north).
        public double SignedArea { get; }

        public int Count => Points.Length;
        public double Area => Math.Abs(SignedArea);
        public bool IsCounterClockwise => SignedArea > 0.0;

        public Ring(Point2d[] points, long[] nodeIds = null)
        {
            if (points == null || points.Length < 3)
            {
                throw new ArgumentException("A ring needs at least 3 points.", nameof(points));
            }

            if (nodeIds != null && nodeIds.Length != points.Length)
            {
                throw new ArgumentException("nodeIds must have one entry per point.", nameof(nodeIds));
            }

            Points = points;
            NodeIds = nodeIds;

            double minX = double.MaxValue;
            double minZ = double.MaxValue;
            double maxX = double.MinValue;
            double maxZ = double.MinValue;
            double twiceArea = 0.0;

            for (int i = 0; i < points.Length; i++)
            {
                Point2d current = points[i];
                Point2d next = points[(i + 1) % points.Length];
                twiceArea += current.X * next.Z - next.X * current.Z;

                minX = Math.Min(minX, current.X);
                maxX = Math.Max(maxX, current.X);
                minZ = Math.Min(minZ, current.Z);
                maxZ = Math.Max(maxZ, current.Z);
            }

            SignedArea = twiceArea * 0.5;
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
        }

        // Even-odd ray casting. Points exactly on the outline may count as inside or outside.
        public bool Contains(Point2d point)
        {
            if (point.X < MinX || point.X > MaxX || point.Z < MinZ || point.Z > MaxZ)
            {
                return false;
            }

            bool inside = false;
            int previous = Points.Length - 1;
            for (int i = 0; i < Points.Length; i++)
            {
                Point2d a = Points[i];
                Point2d b = Points[previous];
                if ((a.Z > point.Z) != (b.Z > point.Z)
                    && point.X < (b.X - a.X) * (point.Z - a.Z) / (b.Z - a.Z) + a.X)
                {
                    inside = !inside;
                }

                previous = i;
            }

            return inside;
        }

        public Ring Reversed()
        {
            int count = Points.Length;
            var points = new Point2d[count];
            long[] nodeIds = NodeIds != null ? new long[count] : null;

            for (int i = 0; i < count; i++)
            {
                points[i] = Points[count - 1 - i];
                if (nodeIds != null)
                {
                    nodeIds[i] = NodeIds[count - 1 - i];
                }
            }

            return new Ring(points, nodeIds);
        }

        // Counter-clockwise copy (or this ring if it already is).
        public Ring AsCounterClockwise()
        {
            return IsCounterClockwise ? this : Reversed();
        }

        // Clockwise copy (or this ring if it already is).
        public Ring AsClockwise()
        {
            return IsCounterClockwise ? Reversed() : this;
        }
    }
}
