using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HighProject.MapCore
{
    // Settings for what blocks movement. Plain data so the editor tool can expose it later.
    // Which barrier types block is decided by ClassificationOptions.HardBarrierTypes.
    public sealed class BlockerOptions
    {
        // railway=* values that are blocked when the track is on the surface (not underground, not on a bridge).
        public HashSet<string> BlockingRailTypes { get; } = new HashSet<string> { "rail", "light_rail" };

        // Width of the blocked band along a surface track, in metres.
        public double RailBandWidthMeters { get; set; } = 3.0;

        // Radius of the opening cleared around a gate node, in metres. At least half a cell diagonal is needed
        // to guarantee a passable opening.
        public double GateOpeningRadiusMeters { get; set; } = 1.0;

        // Width used for gates and building passages that have no width tag, in metres.
        public double DefaultPassageWidthMeters { get; set; } = 2.0;
    }

    public sealed class BlockerReport
    {
        public int BuildingFootprints { get; set; }
        public int WaterAreas { get; set; }
        public int BarrierWays { get; set; }
        public int RailWays { get; set; }
        public int VentilationShafts { get; set; }

        public int BridgesCleared { get; set; }
        public int BuildingPassagesCleared { get; set; }
        public int GateWaysCleared { get; set; }
        public int GateNodesCleared { get; set; }

        public int BlockedCells { get; set; }
        public int BuildingCells { get; set; }
        public int WaterCells { get; set; }
        public int BarrierCells { get; set; }
        public int RailCells { get; set; }

        public override string ToString()
        {
            var text = new StringBuilder();
            text.AppendLine("Blockers");
            text.AppendLine(Line("  Painted: {0} buildings, {1} water areas, {2} barrier ways, {3} rail ways, {4} ventilation shafts",
                BuildingFootprints, WaterAreas, BarrierWays, RailWays, VentilationShafts));
            text.AppendLine(Line("  Cleared: {0} bridges, {1} building passages, {2} gate ways, {3} gate nodes",
                BridgesCleared, BuildingPassagesCleared, GateWaysCleared, GateNodesCleared));
            text.AppendLine(Line("  Cells: {0} blocked ({1} building, {2} water, {3} barrier, {4} rail)",
                BlockedCells, BuildingCells, WaterCells, BarrierCells, RailCells));
            return text.ToString();
        }

        static string Line(string format, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }
    }

    // Marks the cells agents cannot enter and then reopens the places where they can pass anyway.
    // Order: blockers (buildings, water, barriers, rail), then clearings (bridges, building passages, gates).
    public static class BlockerPainter
    {
        const CellFlags ClearedByBridge = CellFlags.Water | CellFlags.Rail | CellFlags.Barrier;
        const CellFlags SolidFlags = CellFlags.Building | CellFlags.Water | CellFlags.Barrier | CellFlags.Rail;

        // Removes one kind of blocker from a cell. The cell stays blocked if any other blocker is still there.
        static void Unblock(MapGrid grid, int index, CellFlags remove)
        {
            grid.ClearFlag(index, remove);
            if (!grid.HasAnyFlag(index, SolidFlags))
            {
                grid.ClearFlag(index, CellFlags.Blocked);
            }
        }

        public static BlockerReport Paint(
            OsmDocument document,
            LocalProjection projection,
            ClassificationOptions classification,
            SurfaceOptions surface,
            BlockerOptions options,
            IReadOnlyList<BuildingFootprint> footprints,
            MapGrid grid)
        {
            var report = new BlockerReport();

            PaintBuildings(footprints, grid, report);
            PaintWater(document, projection, classification, grid, report);
            PaintBarriers(document, projection, classification, grid, report);
            PaintRail(document, projection, classification, options, grid, report);
            PaintVentilationShafts(document, projection, grid, report);

            ClearBridges(document, projection, classification, surface, grid, report);
            ClearBuildingPassages(document, projection, classification, surface, options, grid, report);
            ClearGateWays(document, projection, classification, options, grid, report);
            ClearGateNodes(document, projection, classification, options, grid, report);

            report.BlockedCells = grid.CountFlag(CellFlags.Blocked);
            report.BuildingCells = grid.CountFlag(CellFlags.Building);
            report.WaterCells = grid.CountFlag(CellFlags.Water);
            report.BarrierCells = grid.CountFlag(CellFlags.Barrier);
            report.RailCells = grid.CountFlag(CellFlags.Rail);
            return report;
        }

        static void PaintBuildings(IReadOnlyList<BuildingFootprint> footprints, MapGrid grid, BlockerReport report)
        {
            foreach (BuildingFootprint footprint in footprints)
            {
                int id = footprint.Index;
                PolygonRasterizer.Fill(footprint.Polygon, grid, index =>
                {
                    grid.SetFlag(index, CellFlags.Blocked | CellFlags.Building);
                    grid.BuildingId[index] = id;
                });
                report.BuildingFootprints++;
            }
        }

        static void PaintWater(OsmDocument document, LocalProjection projection, ClassificationOptions classification, MapGrid grid, BlockerReport report)
        {
            List<AreaFeature> water = AreaExtractor.Extract(document, projection,
                tags => (FeatureClassifier.Classify(tags, classification) & FeatureKind.Water) != 0);

            foreach (AreaFeature area in water.OrderBy(a => a.OsmId).ThenBy(a => a.IsRelation))
            {
                PolygonRasterizer.Fill(area.Polygon, grid, index => grid.SetFlag(index, CellFlags.Blocked | CellFlags.Water));
                report.WaterAreas++;
            }
        }

        static void PaintBarriers(OsmDocument document, LocalProjection projection, ClassificationOptions classification, MapGrid grid, BlockerReport report)
        {
            double halfWidth = LineRasterizer.WatertightHalfWidth(grid.CellSize);

            foreach (OsmWay way in document.Ways.OrderBy(w => w.Id))
            {
                FeatureKind kind = FeatureClassifier.Classify(way.Tags, classification);
                if ((kind & FeatureKind.HardBarrier) == 0 || (kind & FeatureKind.Underground) != 0)
                {
                    continue;
                }

                if (WayGeometry.TryGetPolyline(way.NodeIds, document, projection, out Point2d[] line))
                {
                    LineRasterizer.Stroke(line, 0.0, halfWidth, grid, index => grid.SetFlag(index, CellFlags.Blocked | CellFlags.Barrier));
                    report.BarrierWays++;
                }
            }
        }

        static void PaintRail(
            OsmDocument document,
            LocalProjection projection,
            ClassificationOptions classification,
            BlockerOptions options,
            MapGrid grid,
            BlockerReport report)
        {
            foreach (OsmWay way in document.Ways.OrderBy(w => w.Id))
            {
                FeatureKind kind = FeatureClassifier.Classify(way.Tags, classification);
                string rail = OsmTags.Get(way.Tags, "railway");
                bool onSurface = (kind & (FeatureKind.Underground | FeatureKind.Bridge)) == 0;
                if (rail == null || !options.BlockingRailTypes.Contains(rail) || !onSurface)
                {
                    continue;
                }

                if (WayGeometry.TryGetPolyline(way.NodeIds, document, projection, out Point2d[] line))
                {
                    LineRasterizer.Stroke(line, options.RailBandWidthMeters, LineRasterizer.WatertightHalfWidth(grid.CellSize), grid,
                        index => grid.SetFlag(index, CellFlags.Blocked | CellFlags.Rail));
                    report.RailWays++;
                }
            }
        }

        // Ventilation shafts of the metro are small solid towers.
        static void PaintVentilationShafts(OsmDocument document, LocalProjection projection, MapGrid grid, BlockerReport report)
        {
            List<AreaFeature> shafts = AreaExtractor.Extract(document, projection, tags => OsmTags.Is(tags, "railway", "ventilation_shaft"));
            foreach (AreaFeature shaft in shafts.OrderBy(a => a.OsmId))
            {
                PolygonRasterizer.Fill(shaft.Polygon, grid, index => grid.SetFlag(index, CellFlags.Blocked | CellFlags.Rail));
                report.VentilationShafts++;
            }
        }

        // A bridge deck reopens cells blocked by water, rail or a barrier below it, but not by a building.
        static void ClearBridges(
            OsmDocument document,
            LocalProjection projection,
            ClassificationOptions classification,
            SurfaceOptions surface,
            MapGrid grid,
            BlockerReport report)
        {
            foreach (OsmWay way in document.Ways.OrderBy(w => w.Id))
            {
                FeatureKind kind = FeatureClassifier.Classify(way.Tags, classification);
                if ((kind & FeatureKind.Bridge) == 0 || (kind & FeatureKind.Highway) == 0 || (kind & FeatureKind.Underground) != 0)
                {
                    continue;
                }

                string highway = OsmTags.Get(way.Tags, "highway");
                if (!surface.SurfaceForHighway(highway).HasValue
                    || !WayGeometry.TryGetPolyline(way.NodeIds, document, projection, out Point2d[] line))
                {
                    continue;
                }

                double width = surface.HighwayWidth(way.Tags, highway);
                LineRasterizer.Stroke(line, width, LineRasterizer.WatertightHalfWidth(grid.CellSize), grid,
                    index => Unblock(grid, index, ClearedByBridge));
                report.BridgesCleared++;
            }
        }

        // tunnel=building_passage: a walkable way through a building at ground level.
        static void ClearBuildingPassages(
            OsmDocument document,
            LocalProjection projection,
            ClassificationOptions classification,
            SurfaceOptions surface,
            BlockerOptions options,
            MapGrid grid,
            BlockerReport report)
        {
            foreach (OsmWay way in document.Ways.OrderBy(w => w.Id))
            {
                FeatureKind kind = FeatureClassifier.Classify(way.Tags, classification);
                if ((kind & FeatureKind.BuildingPassage) == 0 || !WayGeometry.TryGetPolyline(way.NodeIds, document, projection, out Point2d[] line))
                {
                    continue;
                }

                string highway = OsmTags.Get(way.Tags, "highway");
                double width = highway != null ? surface.HighwayWidth(way.Tags, highway) : options.DefaultPassageWidthMeters;
                LineRasterizer.Stroke(line, width, LineRasterizer.WatertightHalfWidth(grid.CellSize), grid, index =>
                {
                    Unblock(grid, index, CellFlags.Building);
                    grid.BuildingId[index] = MapGrid.NoBuilding;
                });
                report.BuildingPassagesCleared++;
            }
        }

        // barrier=gate drawn as a way: an opening along the gate.
        static void ClearGateWays(
            OsmDocument document,
            LocalProjection projection,
            ClassificationOptions classification,
            BlockerOptions options,
            MapGrid grid,
            BlockerReport report)
        {
            foreach (OsmWay way in document.Ways.OrderBy(w => w.Id))
            {
                if ((FeatureClassifier.Classify(way.Tags, classification) & FeatureKind.Gate) == 0
                    || !WayGeometry.TryGetPolyline(way.NodeIds, document, projection, out Point2d[] line))
                {
                    continue;
                }

                LineRasterizer.Stroke(line, options.DefaultPassageWidthMeters, LineRasterizer.WatertightHalfWidth(grid.CellSize), grid,
                    index => Unblock(grid, index, CellFlags.Barrier));
                report.GateWaysCleared++;
            }
        }

        // barrier=gate on a single node: a round opening in the wall.
        static void ClearGateNodes(
            OsmDocument document,
            LocalProjection projection,
            ClassificationOptions classification,
            BlockerOptions options,
            MapGrid grid,
            BlockerReport report)
        {
            double radius = System.Math.Max(options.GateOpeningRadiusMeters, LineRasterizer.WatertightHalfWidth(grid.CellSize));

            foreach (OsmTaggedNode node in document.TaggedNodes.OrderBy(n => n.Id))
            {
                if ((FeatureClassifier.Classify(node.Tags, classification) & FeatureKind.Gate) == 0)
                {
                    continue;
                }

                Point2d position = projection.ToLocal(node.Lat, node.Lon);
                LineRasterizer.Stroke(new[] { position }, radius * 2.0, 0.0, grid, index => Unblock(grid, index, CellFlags.Barrier));
                report.GateNodesCleared++;
            }
        }
    }
}
