using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HighProject.MapCore
{
    public sealed class SurfacePaintReport
    {
        // Highway ways that were painted, by surface.
        public Dictionary<SurfaceKind, int> PaintedHighways { get; } = new Dictionary<SurfaceKind, int>();

        // Area features (parks, lawns, plazas) that were painted, by surface.
        public Dictionary<SurfaceKind, int> PaintedAreas { get; } = new Dictionary<SurfaceKind, int>();

        public int SkippedUnderground { get; set; }
        public int SkippedUnpaintableHighways { get; set; }
        public int SkippedHighwaysWithoutGeometry { get; set; }

        // Cells per surface after painting.
        public Dictionary<SurfaceKind, int> CellsBySurface { get; } = new Dictionary<SurfaceKind, int>();

        public override string ToString()
        {
            var text = new StringBuilder();
            text.AppendLine("Surface painting");
            text.AppendLine("  Areas painted: " + Format(PaintedAreas));
            text.AppendLine("  Highways painted: " + Format(PaintedHighways));
            text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  Highways skipped: {0} underground, {1} unpaintable type, {2} without usable geometry",
                SkippedUnderground, SkippedUnpaintableHighways, SkippedHighwaysWithoutGeometry));
            text.AppendLine("  Cells by surface: " + Format(CellsBySurface));
            return text.ToString();
        }

        static string Format(Dictionary<SurfaceKind, int> counts)
        {
            return counts.Count == 0
                ? "none"
                : string.Join(", ", counts.OrderBy(pair => (int)pair.Key).Select(pair => pair.Key + "=" + pair.Value));
        }
    }

    // Paints the Surface layer of the grid from OSM: areas first, then roads from big to small, then footpaths
    // and steps on top (so a footway crossing a road wins at the crossing). Within a group the order is by OSM id,
    // so the result does not depend on the order of elements in the file.
    public static class SurfacePainter
    {
        // Painting order of highway surfaces; later ones overwrite earlier ones.
        static readonly SurfaceKind[] HighwayOrder =
        {
            SurfaceKind.MajorRoad, SurfaceKind.MinorRoad, SurfaceKind.Footpath, SurfaceKind.Steps
        };

        public static SurfacePaintReport Paint(
            OsmDocument document,
            LocalProjection projection,
            ClassificationOptions classification,
            SurfaceOptions options,
            MapGrid grid)
        {
            var report = new SurfacePaintReport();

            PaintAreas(document, projection, options, grid, report);
            PaintHighways(document, projection, classification, options, grid, report);

            for (int i = 0; i < grid.Surface.Length; i++)
            {
                SurfaceKind surface = (SurfaceKind)grid.Surface[i];
                report.CellsBySurface.TryGetValue(surface, out int count);
                report.CellsBySurface[surface] = count + 1;
            }

            return report;
        }

        static void PaintAreas(OsmDocument document, LocalProjection projection, SurfaceOptions options, MapGrid grid, SurfacePaintReport report)
        {
            List<AreaFeature> areas = AreaExtractor.Extract(document, projection, tags => options.SurfaceForArea(tags).HasValue);

            var ordered = areas
                .Select(area => new { Area = area, Surface = options.SurfaceForArea(area.Tags).Value })
                .OrderBy(item => (int)item.Surface)
                .ThenBy(item => item.Area.OsmId)
                .ThenBy(item => item.Area.IsRelation);

            foreach (var item in ordered)
            {
                SurfaceKind surface = item.Surface;
                PolygonRasterizer.Fill(item.Area.Polygon, grid, index => grid.Surface[index] = (byte)surface);

                report.PaintedAreas.TryGetValue(surface, out int count);
                report.PaintedAreas[surface] = count + 1;
            }
        }

        static void PaintHighways(
            OsmDocument document,
            LocalProjection projection,
            ClassificationOptions classification,
            SurfaceOptions options,
            MapGrid grid,
            SurfacePaintReport report)
        {
            var candidates = new List<(OsmWay Way, SurfaceKind Surface, string Highway)>();

            foreach (OsmWay way in document.Ways)
            {
                FeatureKind kind = FeatureClassifier.Classify(way.Tags, classification);
                if ((kind & FeatureKind.Highway) == 0)
                {
                    continue;
                }

                // Pedestrian areas (area=yes) are painted as polygons by PaintAreas.
                if (OsmTags.Is(way.Tags, "area", "yes"))
                {
                    continue;
                }

                if ((kind & FeatureKind.Underground) != 0)
                {
                    report.SkippedUnderground++;
                    continue;
                }

                string highway = OsmTags.Get(way.Tags, "highway");
                SurfaceKind? surface = options.SurfaceForHighway(highway);
                if (!surface.HasValue)
                {
                    report.SkippedUnpaintableHighways++;
                    continue;
                }

                candidates.Add((way, surface.Value, highway));
            }

            foreach (SurfaceKind surface in HighwayOrder)
            {
                foreach (var candidate in candidates.Where(item => item.Surface == surface).OrderBy(item => item.Way.Id))
                {
                    if (!WayGeometry.TryGetPolyline(candidate.Way.NodeIds, document, projection, out Point2d[] polyline))
                    {
                        report.SkippedHighwaysWithoutGeometry++;
                        continue;
                    }

                    double width = options.HighwayWidth(candidate.Way.Tags, candidate.Highway);
                    byte value = (byte)surface;
                    LineRasterizer.Stroke(polyline, width, LineRasterizer.WatertightHalfWidth(grid.CellSize), grid, index => grid.Surface[index] = value);

                    report.PaintedHighways.TryGetValue(surface, out int count);
                    report.PaintedHighways[surface] = count + 1;
                }
            }
        }
    }
}
