using System;
using System.Collections.Generic;

// Based on my own earlier project that worked with OpenStreetMap (OSM) data; adapted here to feed the grid and the ECS map blob.
namespace HighProject.MapCore
{
    public enum EntranceMatchKind
    {
        // The entrance node is a vertex of the building outline (the usual way entrances are mapped).
        Vertex,

        // The node is not on the outline but within the tolerance of one of its edges.
        Edge
    }

    public sealed class BuildingEntrance
    {
        public long NodeId { get; }
        public Point2d Position { get; }
        public int FootprintIndex { get; }
        public EntranceMatchKind MatchKind { get; }

        public BuildingEntrance(long nodeId, Point2d position, int footprintIndex, EntranceMatchKind matchKind)
        {
            NodeId = nodeId;
            Position = position;
            FootprintIndex = footprintIndex;
            MatchKind = matchKind;
        }
    }

    public sealed class EntranceMatchResult
    {
        public List<BuildingEntrance> Entrances { get; } = new List<BuildingEntrance>();

        // Entrance nodes that belong to no building (within the tolerance).
        public List<long> UnmatchedNodeIds { get; } = new List<long>();
    }

    // Assigns entrance=* nodes to building footprints. Not used by the simulation yet; baked now so that
    // entering buildings later does not need a new map format.
    public static class EntranceMatcher
    {
        public static EntranceMatchResult Match(
            OsmDocument document,
            LocalProjection projection,
            ClassificationOptions classification,
            IReadOnlyList<BuildingFootprint> footprints,
            double maxEdgeDistanceMeters = 1.5)
        {
            var result = new EntranceMatchResult();
            Dictionary<long, int> vertexOwner = BuildVertexIndex(footprints);

            foreach (OsmTaggedNode node in document.TaggedNodes)
            {
                if ((FeatureClassifier.Classify(node.Tags, classification) & FeatureKind.Entrance) == 0)
                {
                    continue;
                }

                Point2d position = projection.ToLocal(node.Lat, node.Lon);

                if (vertexOwner.TryGetValue(node.Id, out int owner))
                {
                    result.Entrances.Add(new BuildingEntrance(node.Id, position, owner, EntranceMatchKind.Vertex));
                    continue;
                }

                int nearest = FindNearestFootprint(position, footprints, maxEdgeDistanceMeters);
                if (nearest >= 0)
                {
                    result.Entrances.Add(new BuildingEntrance(node.Id, position, nearest, EntranceMatchKind.Edge));
                }
                else
                {
                    result.UnmatchedNodeIds.Add(node.Id);
                }
            }

            return result;
        }

        // Node id -> first footprint that has this node on its outer ring or a hole.
        static Dictionary<long, int> BuildVertexIndex(IReadOnlyList<BuildingFootprint> footprints)
        {
            var index = new Dictionary<long, int>();
            foreach (BuildingFootprint footprint in footprints)
            {
                AddVertices(index, footprint.Polygon.Outer, footprint.Index);
                foreach (Ring hole in footprint.Polygon.Holes)
                {
                    AddVertices(index, hole, footprint.Index);
                }
            }

            return index;
        }

        static void AddVertices(Dictionary<long, int> index, Ring ring, int footprintIndex)
        {
            if (ring.NodeIds == null)
            {
                return;
            }

            foreach (long id in ring.NodeIds)
            {
                if (!index.ContainsKey(id))
                {
                    index[id] = footprintIndex;
                }
            }
        }

        static int FindNearestFootprint(Point2d point, IReadOnlyList<BuildingFootprint> footprints, double maxDistance)
        {
            int best = -1;
            double bestDistance = maxDistance;

            foreach (BuildingFootprint footprint in footprints)
            {
                Ring outer = footprint.Polygon.Outer;
                if (point.X < outer.MinX - maxDistance || point.X > outer.MaxX + maxDistance
                    || point.Z < outer.MinZ - maxDistance || point.Z > outer.MaxZ + maxDistance)
                {
                    continue;
                }

                double distance = DistanceToOutline(point, outer);
                foreach (Ring hole in footprint.Polygon.Holes)
                {
                    distance = Math.Min(distance, DistanceToOutline(point, hole));
                }

                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = footprint.Index;
                }
            }

            return best;
        }

        static double DistanceToOutline(Point2d point, Ring ring)
        {
            double best = double.MaxValue;
            int previous = ring.Count - 1;
            for (int i = 0; i < ring.Count; i++)
            {
                best = Math.Min(best, SegmentMath.DistanceToSegment(point, ring.Points[previous], ring.Points[i]));
                previous = i;
            }

            return best;
        }
    }
}
