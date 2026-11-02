using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;

namespace HighProject.MapCore
{
    // Everything that influences the baked grid. Plain data so the editor tool can expose it as settings later.
    public sealed class GridBuildOptions
    {
        public double CellSize { get; set; } = 1.0;
        public ClassificationOptions Classification { get; } = new ClassificationOptions();
        public BuildingHeightOptions BuildingHeights { get; } = new BuildingHeightOptions();
        public SurfaceOptions Surface { get; } = new SurfaceOptions();
        public BlockerOptions Blockers { get; } = new BlockerOptions();
        public SpawnMaskOptions Spawn { get; } = new SpawnMaskOptions();
    }

    public sealed class GridBuildResult
    {
        public OsmBounds Bounds { get; set; }
        public LocalProjection Projection { get; set; }
        public MapGrid Grid { get; set; }
        public BuildingExtractionResult Buildings { get; set; }
        public EntranceMatchResult Entrances { get; set; }
        public SurfacePaintReport Surfaces { get; set; }
        public BlockerReport Blockers { get; set; }
        public ConnectivityResult Connectivity { get; set; }
        public int SpawnableCells { get; set; }

        // Wall-clock time per stage, in the order the stages ran.
        public List<KeyValuePair<string, long>> StageMilliseconds { get; } = new List<KeyValuePair<string, long>>();

        // Identifies the exact grid content; equal for two builds of the same data and settings.
        public string Checksum => Grid.ComputeChecksum().ToString("x16", CultureInfo.InvariantCulture);

        public override string ToString()
        {
            var text = new StringBuilder();
            text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "Grid {0} x {1} cells of {2} m (origin {3:F1}, {4:F1} m), checksum {5}",
                Grid.Width, Grid.Height, Grid.CellSize, Grid.OriginX, Grid.OriginZ, Checksum));
            text.AppendLine(Buildings.ToString().TrimEnd());
            text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "Entrances: {0} matched to buildings, {1} not on any building", Entrances.Entrances.Count, Entrances.UnmatchedNodeIds.Count));
            text.AppendLine(Surfaces.ToString().TrimEnd());
            text.AppendLine(Blockers.ToString().TrimEnd());
            text.AppendLine(Connectivity.Describe(Grid, 5).TrimEnd());
            text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "Spawnable cells: {0} of {1} walkable", SpawnableCells, Connectivity.WalkableCells));

            var timing = new StringBuilder("Stage times (ms):");
            foreach (KeyValuePair<string, long> stage in StageMilliseconds)
            {
                timing.Append(string.Format(CultureInfo.InvariantCulture, " {0} {1},", stage.Key, stage.Value));
            }

            text.AppendLine(timing.ToString().TrimEnd(','));
            return text.ToString();
        }
    }

    // Runs the whole chain: buildings, entrances, grid, surfaces, blockers, costs, connectivity, spawn mask.
    // Free of Unity types, so it can run on a worker thread. Progress and cancellation are checked between stages.
    public static class GridBuilder
    {
        const int StageCount = 8;

        public static GridBuildResult Build(
            OsmDocument document,
            GridBuildOptions options,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new GridBuildResult();
            var timer = new Stopwatch();
            int stage = 0;

            void Run(string name, Action work)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report((float)stage / StageCount);
                timer.Restart();
                work();
                result.StageMilliseconds.Add(new KeyValuePair<string, long>(name, timer.ElapsedMilliseconds));
                stage++;
            }

            OsmBounds bounds = document.GetBoundsOrComputed();
            LocalProjection projection = new LocalProjection(bounds.CenterLat, bounds.CenterLon);
            result.Bounds = bounds;
            result.Projection = projection;

            Run("buildings", () => result.Buildings = BuildingExtractor.Extract(
                document, projection, options.Classification, options.BuildingHeights));

            Run("entrances", () => result.Entrances = EntranceMatcher.Match(
                document, projection, options.Classification, result.Buildings.Footprints));

            Run("grid", () =>
            {
                Point2d southWest = projection.ToLocal(bounds.MinLat, bounds.MinLon);
                Point2d northEast = projection.ToLocal(bounds.MaxLat, bounds.MaxLon);
                result.Grid = MapGrid.CreateCovering(southWest.X, southWest.Z, northEast.X, northEast.Z, options.CellSize);
            });

            Run("surfaces", () => result.Surfaces = SurfacePainter.Paint(
                document, projection, options.Classification, options.Surface, result.Grid));

            Run("blockers", () => result.Blockers = BlockerPainter.Paint(
                document, projection, options.Classification, options.Surface, options.Blockers, result.Buildings.Footprints, result.Grid));

            Run("costs", () => result.Grid.ApplySurfaceCosts(options.Surface.CostBySurface));

            Run("connectivity", () => result.Connectivity = ConnectivityAnalyzer.Analyze(result.Grid));

            Run("spawn", () => result.SpawnableCells = SpawnMask.Apply(result.Grid, result.Connectivity, options.Spawn));

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(1f);
            return result;
        }
    }
}
