using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HighProject.MapCore
{
    public enum ValidationSeverity
    {
        // Worth knowing, nothing to fix.
        Info,

        // The map is usable but something probably is not what you want.
        Warning,

        // The map cannot be used for the simulation as it is.
        Error
    }

    public sealed class ValidationMessage
    {
        public ValidationSeverity Severity { get; }
        public string Text { get; }

        public ValidationMessage(ValidationSeverity severity, string text)
        {
            Severity = severity;
            Text = text;
        }

        public override string ToString()
        {
            return $"{Severity}: {Text}";
        }
    }

    // Reads a finished bake and says in plain words what might be wrong with it.
    public static class GridValidator
    {
        // Cells larger than this can erase footpaths and alleys, which are often only 1.5 to 2 m wide.
        public const double LargestSafeCellSize = 1.5;

        // More cells than this uses a lot of memory (7 bytes per cell plus the connectivity pass).
        public const int HugeCellCount = 20000000;

        const double LowSpawnShare = 0.5;
        const double HighDefaultHeightShare = 0.5;

        public static List<ValidationMessage> Validate(GridBuildResult result, GridBuildOptions options)
        {
            var messages = new List<ValidationMessage>();

            CheckSpawnArea(result, messages);
            CheckBuildings(result, messages);
            CheckEntrances(result, messages);
            CheckGrid(result, options, messages);
            return messages;
        }

        static void CheckSpawnArea(GridBuildResult result, List<ValidationMessage> messages)
        {
            int walkable = result.Connectivity.WalkableCells;
            if (walkable == 0)
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Error,
                    "There are no walkable cells at all. The whole map is blocked: check the bounds of the OSM file and the cell size."));
                return;
            }

            if (result.SpawnableCells == 0)
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Error,
                    "No cell is spawnable, so no agent could start anywhere. Check the minimum area size for spawning."));
                return;
            }

            double share = (double)result.SpawnableCells / walkable;
            if (share < LowSpawnShare)
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Warning, Format(
                    "Only {0:F1}% of the walkable cells can be used for spawning; the rest is cut off from the main area by walls, water or buildings. " +
                    "Open a gap with an override, or check the barrier settings.", 100.0 * share)));
            }
            else
            {
                int cutOff = walkable - result.SpawnableCells;
                if (cutOff > 0)
                {
                    messages.Add(new ValidationMessage(ValidationSeverity.Info, Format(
                        "{0} walkable cells ({1:F1}%) are cut off from the main area (sealed courtyards, fenced grounds) and are not used for spawning.",
                        cutOff, 100.0 * cutOff / walkable)));
                }
            }
        }

        static void CheckBuildings(GridBuildResult result, List<ValidationMessage> messages)
        {
            BuildingExtractionResult buildings = result.Buildings;
            if (buildings.Footprints.Count == 0)
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Warning, "No buildings were found in the OSM file."));
                return;
            }

            int wayProblems = buildings.WayProblems.Values.Sum();
            int relationProblems = buildings.RelationsWithoutPolygons + buildings.OpenChains + buildings.InvalidRings
                                   + buildings.UnassignedHoles + buildings.MissingMemberWays;
            if (wayProblems + relationProblems > 0)
            {
                string reasons = string.Join(", ", buildings.WayProblems
                    .OrderBy(pair => (int)pair.Key)
                    .Select(pair => $"{pair.Value} building way(s) {pair.Key}"));
                messages.Add(new ValidationMessage(ValidationSeverity.Warning, Format(
                    "Some buildings could not be used: {0}{1}relations without outline {2}, open outline pieces {3}, invalid rings {4}, holes outside any outline {5}, missing member ways {6}.",
                    reasons, reasons.Length > 0 ? "; " : string.Empty,
                    buildings.RelationsWithoutPolygons, buildings.OpenChains, buildings.InvalidRings, buildings.UnassignedHoles, buildings.MissingMemberWays)));
            }

            int defaulted = buildings.CountWithHeightSource(HeightSource.Default);
            double share = (double)defaulted / buildings.Footprints.Count;
            if (share >= HighDefaultHeightShare)
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Info, Format(
                    "{0} of {1} buildings ({2:F0}%) have no height or levels in OpenStreetMap and use the default height. This only affects how the 3D buildings look.",
                    defaulted, buildings.Footprints.Count, 100.0 * share)));
            }
        }

        static void CheckEntrances(GridBuildResult result, List<ValidationMessage> messages)
        {
            int unmatched = result.Entrances.UnmatchedNodeIds.Count;
            if (unmatched > 0)
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Info, Format(
                    "{0} entrance points are not on any building (usually entrances to parks, stairs and tunnels).", unmatched)));
            }
        }

        static void CheckGrid(GridBuildResult result, GridBuildOptions options, List<ValidationMessage> messages)
        {
            if (options.CellSize > LargestSafeCellSize)
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Warning, Format(
                    "The cell size is {0} m. Footpaths and alleys are often only 1.5 to 2 m wide, so cells this large can erase them and split the map.",
                    options.CellSize)));
            }

            if (result.Grid.CellCount > HugeCellCount)
            {
                messages.Add(new ValidationMessage(ValidationSeverity.Warning, Format(
                    "The grid has {0} cells. That needs a lot of memory and slows every step; use a larger cell size or a smaller area.",
                    result.Grid.CellCount)));
            }
        }

        static string Format(string format, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }
    }
}
