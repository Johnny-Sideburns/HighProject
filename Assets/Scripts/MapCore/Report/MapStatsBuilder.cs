using System;
using System.Collections.Generic;

namespace HighProject.MapCore
{
    public static class MapStatsBuilder
    {
        // Keys whose values are counted for ways (raw tags, before classification).
        static readonly string[] CountedWayKeys =
        {
            "building", "highway", "natural", "waterway", "leisure", "landuse", "railway", "barrier", "bridge", "tunnel", "layer"
        };

        static readonly FeatureKind[] AllKinds = (FeatureKind[])Enum.GetValues(typeof(FeatureKind));

        public static MapStats Build(OsmDocument document, ClassificationOptions options)
        {
            OsmBounds bounds = document.GetBoundsOrComputed();
            var projection = new LocalProjection(bounds.CenterLat, bounds.CenterLon);
            Point2d southWest = projection.ToLocal(bounds.MinLat, bounds.MinLon);
            Point2d northEast = projection.ToLocal(bounds.MaxLat, bounds.MaxLon);

            var stats = new MapStats
            {
                Bounds = bounds,
                Projection = projection,
                WidthMeters = northEast.X - southWest.X,
                HeightMeters = northEast.Z - southWest.Z,
                NodeCount = document.Nodes.Count,
                TaggedNodeCount = document.TaggedNodes.Count,
                WayCount = document.Ways.Count,
                RelationCount = document.Relations.Count
            };

            foreach (OsmNode node in document.Nodes.Values)
            {
                if (!bounds.Contains(node.Lat, node.Lon))
                {
                    stats.NodesOutsideBounds++;
                }
            }

            foreach (OsmTaggedNode node in document.TaggedNodes)
            {
                CountKinds(stats.NodesByKind, FeatureClassifier.Classify(node.Tags, options));
            }

            foreach (OsmWay way in document.Ways)
            {
                CountWay(stats, document, way, options);
            }

            foreach (OsmRelation relation in document.Relations)
            {
                if (OsmTags.Is(relation.Tags, "type", "multipolygon"))
                {
                    stats.MultipolygonRelationCount++;
                }

                CountKinds(stats.RelationsByKind, FeatureClassifier.Classify(relation.Tags, options));
            }

            return stats;
        }

        static void CountWay(MapStats stats, OsmDocument document, OsmWay way, ClassificationOptions options)
        {
            foreach (long nodeId in way.NodeIds)
            {
                if (!document.Nodes.ContainsKey(nodeId))
                {
                    stats.WaysWithMissingNodes++;
                    break;
                }
            }

            FeatureKind kind = FeatureClassifier.Classify(way.Tags, options);
            CountKinds(stats.WaysByKind, kind);

            foreach (string key in CountedWayKeys)
            {
                string value = OsmTags.Get(way.Tags, key);
                if (value == null)
                {
                    continue;
                }

                if (!stats.WayTagValues.TryGetValue(key, out Dictionary<string, int> values))
                {
                    values = new Dictionary<string, int>();
                    stats.WayTagValues[key] = values;
                }

                values.TryGetValue(value, out int count);
                values[value] = count + 1;
            }

            if ((kind & FeatureKind.Building) != 0)
            {
                if (way.IsClosed)
                {
                    stats.ClosedBuildingWays++;
                }
                else
                {
                    stats.UnclosedBuildingWays++;
                }

                if (OsmTags.Has(way.Tags, "building:levels"))
                {
                    stats.BuildingsWithLevels++;
                }

                if (OsmTags.Has(way.Tags, "height"))
                {
                    stats.BuildingsWithHeight++;
                }
            }
        }

        static void CountKinds(Dictionary<FeatureKind, int> counts, FeatureKind kind)
        {
            foreach (FeatureKind single in AllKinds)
            {
                if (single == FeatureKind.None || (kind & single) == 0)
                {
                    continue;
                }

                counts.TryGetValue(single, out int count);
                counts[single] = count + 1;
            }
        }
    }
}
