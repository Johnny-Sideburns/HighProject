using System.Collections.Generic;

namespace HighProject.MapCore
{
    public enum RingProblem
    {
        None,
        MissingNode,
        NotClosed,
        TooFewPoints,
        ZeroArea
    }

    public static class WayGeometry
    {
        // Areas smaller than this (square metres) are treated as degenerate.
        const double MinimumArea = 1e-6;

        // Builds a ring from a closed chain of node ids (first id == last id). Consecutive duplicate points are dropped.
        public static RingProblem TryGetRing(IReadOnlyList<long> nodeIds, OsmDocument document, LocalProjection projection, out Ring ring)
        {
            ring = null;

            if (nodeIds.Count < 4 || nodeIds[0] != nodeIds[nodeIds.Count - 1])
            {
                return nodeIds.Count >= 4 ? RingProblem.NotClosed : RingProblem.TooFewPoints;
            }

            var points = new List<Point2d>(nodeIds.Count);
            var ids = new List<long>(nodeIds.Count);

            // The last id repeats the first one; skip it so the ring is stored open.
            for (int i = 0; i < nodeIds.Count - 1; i++)
            {
                long id = nodeIds[i];
                if (!document.Nodes.TryGetValue(id, out OsmNode node))
                {
                    return RingProblem.MissingNode;
                }

                Point2d point = projection.ToLocal(node.Lat, node.Lon);
                if (points.Count > 0 && points[points.Count - 1].X == point.X && points[points.Count - 1].Z == point.Z)
                {
                    continue;
                }

                points.Add(point);
                ids.Add(id);
            }

            // The first point may also equal the last kept point (a duplicate before the closing node).
            if (points.Count > 1 && points[0].X == points[points.Count - 1].X && points[0].Z == points[points.Count - 1].Z)
            {
                points.RemoveAt(points.Count - 1);
                ids.RemoveAt(ids.Count - 1);
            }

            if (points.Count < 3)
            {
                return RingProblem.TooFewPoints;
            }

            var candidate = new Ring(points.ToArray(), ids.ToArray());
            if (candidate.Area < MinimumArea)
            {
                return RingProblem.ZeroArea;
            }

            ring = candidate;
            return RingProblem.None;
        }

        // Projects the nodes of an open or closed way to a polyline. Nodes missing from the file are skipped and
        // consecutive duplicate points dropped. Returns false when fewer than two points remain.
        public static bool TryGetPolyline(IReadOnlyList<long> nodeIds, OsmDocument document, LocalProjection projection, out Point2d[] polyline)
        {
            var points = new List<Point2d>(nodeIds.Count);
            foreach (long id in nodeIds)
            {
                if (!document.Nodes.TryGetValue(id, out OsmNode node))
                {
                    continue;
                }

                Point2d point = projection.ToLocal(node.Lat, node.Lon);
                if (points.Count > 0 && points[points.Count - 1].X == point.X && points[points.Count - 1].Z == point.Z)
                {
                    continue;
                }

                points.Add(point);
            }

            polyline = points.Count >= 2 ? points.ToArray() : null;
            return polyline != null;
        }
    }
}
