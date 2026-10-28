using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HighProject.MapCore
{
    // One solid building outline. A relation with several outer rings produces several footprints with the same OsmId.
    public sealed class BuildingFootprint
    {
        // Position in the extraction result (0, 1, 2, ...); this is the id used by later map data.
        public int Index { get; }
        public long OsmId { get; }
        public bool IsRelation { get; }
        public AreaPolygon Polygon { get; }
        public double HeightMeters { get; }
        public HeightSource HeightSource { get; }

        // Value of the building=* tag ("yes", "apartments", ...).
        public string BuildingType { get; }

        public BuildingFootprint(int index, long osmId, bool isRelation, AreaPolygon polygon, double heightMeters, HeightSource heightSource, string buildingType)
        {
            Index = index;
            OsmId = osmId;
            IsRelation = isRelation;
            Polygon = polygon;
            HeightMeters = heightMeters;
            HeightSource = heightSource;
            BuildingType = buildingType;
        }
    }

    public sealed class BuildingExtractionResult
    {
        public List<BuildingFootprint> Footprints { get; } = new List<BuildingFootprint>();

        // Building ways that were not usable, by reason.
        public Dictionary<RingProblem, int> WayProblems { get; } = new Dictionary<RingProblem, int>();

        // Building ways that are also members of a building relation (the relation provides the outline).
        public int DuplicateWaysSkipped { get; set; }

        // Building relations that are not multipolygons (e.g. type=building) and are ignored.
        public int RelationsIgnored { get; set; }

        // Multipolygon building relations that produced no polygon at all.
        public int RelationsWithoutPolygons { get; set; }

        // Summed over all building relations, see MultipolygonResult.
        public int MissingMemberWays { get; set; }
        public int OpenChains { get; set; }
        public int InvalidRings { get; set; }
        public int UnassignedHoles { get; set; }

        public int HoleCount
        {
            get
            {
                int holes = 0;
                foreach (BuildingFootprint footprint in Footprints)
                {
                    holes += footprint.Polygon.Holes.Count;
                }

                return holes;
            }
        }

        // Sum of footprint areas (holes subtracted), in square metres.
        public double TotalArea
        {
            get
            {
                double area = 0.0;
                foreach (BuildingFootprint footprint in Footprints)
                {
                    area += footprint.Polygon.Area;
                }

                return area;
            }
        }

        public override string ToString()
        {
            int fromRelations = Footprints.Count(footprint => footprint.IsRelation);
            string problems = WayProblems.Count == 0
                ? "none"
                : string.Join(", ", WayProblems.OrderBy(pair => (int)pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));

            return string.Join(Environment.NewLine, new[]
            {
                "Building footprints",
                Invariant($"  Footprints: {Footprints.Count} ({Footprints.Count - fromRelations} from ways, {fromRelations} from relations), {HoleCount} holes"),
                Invariant($"  Total area: {TotalArea:F0} m2"),
                Invariant($"  Height from: height tag {CountWithHeightSource(HeightSource.HeightTag)}, levels {CountWithHeightSource(HeightSource.Levels)}, default {CountWithHeightSource(HeightSource.Default)}"),
                Invariant($"  Skipped: {DuplicateWaysSkipped} ways duplicated by a relation, {RelationsIgnored} non-multipolygon relations"),
                Invariant($"  Problems: way problems {problems}; relations without polygons {RelationsWithoutPolygons}, missing member ways {MissingMemberWays}, open chains {OpenChains}, invalid rings {InvalidRings}, unassigned holes {UnassignedHoles}")
            });
        }

        static string Invariant(FormattableString text)
        {
            return text.ToString(CultureInfo.InvariantCulture);
        }

        public int CountWithHeightSource(HeightSource source)
        {
            int count = 0;
            foreach (BuildingFootprint footprint in Footprints)
            {
                if (footprint.HeightSource == source)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
