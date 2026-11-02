using System;
using System.Collections.Generic;
using System.Linq;

namespace HighProject.MapCore
{
    // Cuts a polygon with holes into triangles (ear clipping). Holes are first joined to the outline by thin bridges,
    // which makes the polygon one closed loop that can be clipped like a plain polygon.
    // Vertices are the outer ring followed by each hole ring; the triangles index into that array and run
    // counter-clockwise (x east, z north).
    public static class Triangulator
    {
        const double Epsilon = 1e-9;

        // Returns false if the polygon could not be fully triangulated (self-intersecting input, a hole outside
        // the outline). The triangles found so far are still returned.
        public static bool Triangulate(AreaPolygon polygon, out Point2d[] vertices, out int[] triangles)
        {
            Ring outer = polygon.Outer.AsCounterClockwise();
            var points = new List<Point2d>();
            var loop = new List<int>();
            AddRing(outer, points, loop);

            var holes = new List<(List<int> Indices, double MaxX, int Order)>();
            foreach (Ring hole in polygon.Holes)
            {
                var indices = new List<int>();
                AddRing(hole.AsClockwise(), points, indices);
                holes.Add((indices, hole.MaxX, holes.Count));
            }

            bool success = true;
            foreach (var hole in holes.OrderByDescending(h => h.MaxX).ThenBy(h => h.Order))
            {
                if (!MergeHole(loop, hole.Indices, points))
                {
                    success = false;
                }
            }

            var result = new List<int>();
            success &= ClipEars(loop, points, result);

            vertices = points.ToArray();
            triangles = result.ToArray();
            return success;
        }

        static void AddRing(Ring ring, List<Point2d> points, List<int> indices)
        {
            foreach (Point2d point in ring.Points)
            {
                indices.Add(points.Count);
                points.Add(point);
            }
        }

        // Joins one hole to the loop with a bridge from the hole's rightmost vertex to a vertex of the loop
        // that it can see.
        static bool MergeHole(List<int> loop, List<int> hole, List<Point2d> points)
        {
            int rightmost = 0;
            for (int i = 1; i < hole.Count; i++)
            {
                if (points[hole[i]].X > points[hole[rightmost]].X)
                {
                    rightmost = i;
                }
            }

            Point2d m = points[hole[rightmost]];
            int bridgePosition = FindBridgePosition(loop, points, m);
            if (bridgePosition < 0)
            {
                return false;
            }

            // ... target, M, hole vertices around back to M, target again, ...
            var insert = new List<int>(hole.Count + 2);
            for (int i = 0; i <= hole.Count; i++)
            {
                insert.Add(hole[(rightmost + i) % hole.Count]);
            }

            insert.Add(loop[bridgePosition]);
            loop.InsertRange(bridgePosition + 1, insert);
            return true;
        }

        // Position in the loop of a vertex that can be seen from m looking to the right, or -1.
        static int FindBridgePosition(List<int> loop, List<Point2d> points, Point2d m)
        {
            // Nearest loop edge crossed by the ray from m towards +x.
            double bestX = double.MaxValue;
            int bestEdge = -1;
            for (int i = 0; i < loop.Count; i++)
            {
                Point2d a = points[loop[i]];
                Point2d b = points[loop[(i + 1) % loop.Count]];
                bool spans = (a.Z <= m.Z && b.Z >= m.Z) || (b.Z <= m.Z && a.Z >= m.Z);
                if (!spans || a.Z == b.Z)
                {
                    continue;
                }

                double x = a.X + (m.Z - a.Z) * (b.X - a.X) / (b.Z - a.Z);
                if (x >= m.X - Epsilon && x < bestX)
                {
                    bestX = x;
                    bestEdge = i;
                }
            }

            if (bestEdge < 0)
            {
                return -1;
            }

            int edgeStart = bestEdge;
            int edgeEnd = (bestEdge + 1) % loop.Count;
            Point2d pa = points[loop[edgeStart]];
            Point2d pb = points[loop[edgeEnd]];

            // The endpoint with the larger x is the first candidate; if the ray passes exactly through it, it is the answer.
            int candidate = pa.X > pb.X ? edgeStart : edgeEnd;
            Point2d p = points[loop[candidate]];
            var hit = new Point2d(bestX, m.Z);
            if (Math.Abs(p.Z - m.Z) <= Epsilon)
            {
                return ChoosePosition(loop, points, loop[candidate], m);
            }

            // Another vertex inside the triangle (m, hit, p) would block the view; pick the one closest to the ray.
            int best = candidate;
            double bestAngle = double.MaxValue;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < loop.Count; i++)
            {
                Point2d r = points[loop[i]];
                if (i == candidate || r.X < m.X || !InsideTriangle(r, m, hit, p))
                {
                    continue;
                }

                double dx = r.X - m.X;
                double dz = Math.Abs(r.Z - m.Z);
                double angle = dx > Epsilon ? dz / dx : double.MaxValue;
                double distance = dx * dx + dz * dz;
                if (angle < bestAngle || (Math.Abs(angle - bestAngle) <= Epsilon && distance < bestDistance))
                {
                    bestAngle = angle;
                    bestDistance = distance;
                    best = i;
                }
            }

            return ChoosePosition(loop, points, loop[best], m);
        }

        // A vertex can appear several times in the loop (after earlier bridges); use the occurrence whose corner
        // opens towards m.
        static int ChoosePosition(List<int> loop, List<Point2d> points, int vertexIndex, Point2d m)
        {
            int first = -1;
            for (int i = 0; i < loop.Count; i++)
            {
                if (loop[i] != vertexIndex)
                {
                    continue;
                }

                if (first < 0)
                {
                    first = i;
                }

                Point2d previous = points[loop[(i + loop.Count - 1) % loop.Count]];
                Point2d next = points[loop[(i + 1) % loop.Count]];
                if (InsideCorner(previous, points[vertexIndex], next, m))
                {
                    return i;
                }
            }

            return first;
        }

        // True if m lies in the interior angle of the counter-clockwise corner previous -> vertex -> next.
        static bool InsideCorner(Point2d previous, Point2d vertex, Point2d next, Point2d m)
        {
            if (Cross(previous, vertex, next) >= 0.0)
            {
                // Convex corner: m must be left of both edges.
                return Cross(previous, vertex, m) >= -Epsilon && Cross(vertex, next, m) >= -Epsilon;
            }

            // Reflex corner: m just must not be in the outside wedge.
            return !(Cross(vertex, previous, m) >= Epsilon && Cross(next, vertex, m) >= Epsilon);
        }

        static bool ClipEars(List<int> loop, List<Point2d> points, List<int> triangles)
        {
            var remaining = new List<int>(loop);
            int guard = remaining.Count * remaining.Count + 16;

            while (remaining.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int previous = (i + remaining.Count - 1) % remaining.Count;
                    int next = (i + 1) % remaining.Count;
                    Point2d a = points[remaining[previous]];
                    Point2d b = points[remaining[i]];
                    Point2d c = points[remaining[next]];

                    double cross = Cross(a, b, c);
                    if (Math.Abs(cross) <= Epsilon)
                    {
                        // Collinear or duplicated point: removing it does not change the shape.
                        remaining.RemoveAt(i);
                        clipped = true;
                        break;
                    }

                    if (cross > 0.0 && IsEar(remaining, points, previous, i, next))
                    {
                        triangles.Add(remaining[previous]);
                        triangles.Add(remaining[i]);
                        triangles.Add(remaining[next]);
                        remaining.RemoveAt(i);
                        clipped = true;
                        break;
                    }
                }

                if (!clipped)
                {
                    return false;
                }
            }

            if (remaining.Count == 3)
            {
                Point2d a = points[remaining[0]];
                Point2d b = points[remaining[1]];
                Point2d c = points[remaining[2]];
                if (Cross(a, b, c) > Epsilon)
                {
                    triangles.Add(remaining[0]);
                    triangles.Add(remaining[1]);
                    triangles.Add(remaining[2]);
                }

                return true;
            }

            return remaining.Count < 3;
        }

        static bool IsEar(List<int> remaining, List<Point2d> points, int previous, int current, int next)
        {
            Point2d a = points[remaining[previous]];
            Point2d b = points[remaining[current]];
            Point2d c = points[remaining[next]];

            for (int i = 0; i < remaining.Count; i++)
            {
                if (i == previous || i == current || i == next)
                {
                    continue;
                }

                Point2d p = points[remaining[i]];

                // A bridge repeats a vertex; copies of the triangle's own corners do not block it.
                if (SamePosition(p, a) || SamePosition(p, b) || SamePosition(p, c))
                {
                    continue;
                }

                if (InsideTriangle(p, a, b, c))
                {
                    return false;
                }
            }

            return true;
        }

        static bool SamePosition(Point2d p, Point2d q)
        {
            return Math.Abs(p.X - q.X) <= Epsilon && Math.Abs(p.Z - q.Z) <= Epsilon;
        }

        // Inside or on the border of the counter-clockwise triangle (a, b, c); also accepts clockwise triangles.
        static bool InsideTriangle(Point2d p, Point2d a, Point2d b, Point2d c)
        {
            double d1 = Cross(a, b, p);
            double d2 = Cross(b, c, p);
            double d3 = Cross(c, a, p);
            bool hasNegative = d1 < -Epsilon || d2 < -Epsilon || d3 < -Epsilon;
            bool hasPositive = d1 > Epsilon || d2 > Epsilon || d3 > Epsilon;
            return !(hasNegative && hasPositive);
        }

        // Twice the signed area of the triangle (a, b, c); positive when it runs counter-clockwise.
        static double Cross(Point2d a, Point2d b, Point2d c)
        {
            return (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);
        }
    }
}
