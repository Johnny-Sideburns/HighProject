using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HighProject.MapCore
{
    // Counts describing what an OSM document contains and how it classifies. Used for the bake report.
    public sealed class MapStats
    {
        public OsmBounds Bounds { get; set; }
        public LocalProjection Projection { get; set; }
        public double WidthMeters { get; set; }
        public double HeightMeters { get; set; }

        public int NodeCount { get; set; }
        public int TaggedNodeCount { get; set; }
        public int WayCount { get; set; }
        public int RelationCount { get; set; }
        public int MultipolygonRelationCount { get; set; }

        // Nodes that lie outside the file's bounds (ways crossing the border bring such nodes along).
        public int NodesOutsideBounds { get; set; }

        // Ways that reference a node id the file does not contain.
        public int WaysWithMissingNodes { get; set; }

        public int ClosedBuildingWays { get; set; }
        public int UnclosedBuildingWays { get; set; }
        public int BuildingsWithLevels { get; set; }
        public int BuildingsWithHeight { get; set; }

        public Dictionary<FeatureKind, int> WaysByKind { get; } = new Dictionary<FeatureKind, int>();
        public Dictionary<FeatureKind, int> RelationsByKind { get; } = new Dictionary<FeatureKind, int>();
        public Dictionary<FeatureKind, int> NodesByKind { get; } = new Dictionary<FeatureKind, int>();

        // For ways: tag key -> (tag value -> number of ways). Raw tag counts, independent of classification.
        public Dictionary<string, Dictionary<string, int>> WayTagValues { get; } = new Dictionary<string, Dictionary<string, int>>();

        public int WayCountWithKey(string key)
        {
            return WayTagValues.TryGetValue(key, out Dictionary<string, int> values) ? values.Values.Sum() : 0;
        }

        public int WaysOfKind(FeatureKind kind)
        {
            return WaysByKind.TryGetValue(kind, out int count) ? count : 0;
        }

        public override string ToString()
        {
            var text = new StringBuilder();
            text.AppendLine("OSM map statistics");
            text.AppendLine(Invariant($"  Centre: {Bounds.CenterLat:F5}, {Bounds.CenterLon:F5}; extent {WidthMeters:F0} m x {HeightMeters:F0} m (east-west x north-south)"));
            text.AppendLine(Invariant($"  Nodes: {NodeCount} ({TaggedNodeCount} tagged, {NodesOutsideBounds} outside the bounds)"));
            text.AppendLine(Invariant($"  Ways: {WayCount} ({WaysWithMissingNodes} with missing nodes)"));
            text.AppendLine(Invariant($"  Relations: {RelationCount} ({MultipolygonRelationCount} multipolygons)"));
            text.AppendLine(Invariant($"  Buildings (ways): {ClosedBuildingWays} closed, {UnclosedBuildingWays} unclosed; {BuildingsWithLevels} with levels, {BuildingsWithHeight} with height"));

            AppendKinds(text, "Ways by kind", WaysByKind);
            AppendKinds(text, "Relations by kind", RelationsByKind);
            AppendKinds(text, "Nodes by kind", NodesByKind);

            text.AppendLine("  Way tags (key: total, top values):");
            foreach (KeyValuePair<string, Dictionary<string, int>> entry in WayTagValues.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                string top = string.Join(", ", entry.Value
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                    .Take(6)
                    .Select(pair => $"{pair.Key}={pair.Value}"));
                text.AppendLine(Invariant($"    {entry.Key}: {entry.Value.Values.Sum()} ({top})"));
            }

            return text.ToString();
        }

        static string Invariant(FormattableString text)
        {
            return text.ToString(CultureInfo.InvariantCulture);
        }

        static void AppendKinds(StringBuilder text, string title, Dictionary<FeatureKind, int> counts)
        {
            text.AppendLine(Invariant($"  {title}:"));
            foreach (KeyValuePair<FeatureKind, int> entry in counts.OrderBy(pair => (int)pair.Key))
            {
                text.AppendLine(Invariant($"    {entry.Key}: {entry.Value}"));
            }
        }
    }
}
