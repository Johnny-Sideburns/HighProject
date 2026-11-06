using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HighProject.MapCore
{
    // Human-readable description of one grid cell, for the preview window and for debugging.
    public static class GridInspector
    {
        // Describes the cell under a position in local metres, or says the position is outside the grid.
        public static string DescribePosition(MapGrid grid, ConnectivityResult connectivity, IReadOnlyList<BuildingFootprint> footprints, double worldX, double worldZ)
        {
            int x = grid.CellXOf(worldX);
            int z = grid.CellZOf(worldZ);
            return grid.InBounds(x, z)
                ? DescribeCell(grid, connectivity, footprints, x, z)
                : string.Format(CultureInfo.InvariantCulture, "({0:F1}, {1:F1}) m is outside the grid", worldX, worldZ);
        }

        public static string DescribeCell(MapGrid grid, ConnectivityResult connectivity, IReadOnlyList<BuildingFootprint> footprints, int x, int z)
        {
            int index = grid.Index(x, z);
            var text = new StringBuilder();

            text.Append(string.Format(CultureInfo.InvariantCulture, "Cell ({0}, {1}) at ({2:F1}, {3:F1}) m: {4}, cost {5}",
                x, z, grid.CellCenterX(x), grid.CellCenterZ(z), grid.SurfaceAt(index), grid.Cost[index]));

            text.Append("; flags: ").Append(FlagNames(grid.Flags[index]));

            int buildingId = grid.BuildingId[index];
            if (buildingId != MapGrid.NoBuilding)
            {
                text.Append(string.Format(CultureInfo.InvariantCulture, "; building #{0}", buildingId));
                if (footprints != null && buildingId < footprints.Count)
                {
                    BuildingFootprint footprint = footprints[buildingId];
                    text.Append(string.Format(CultureInfo.InvariantCulture, " (OSM {0}{1}, {2}, {3:F1} m high from {4}, {5:F0} m2)",
                        footprint.OsmId, footprint.IsRelation ? " relation" : " way", footprint.BuildingType ?? "building",
                        footprint.HeightMeters, footprint.HeightSource, footprint.Polygon.Area));
                }
            }

            if (connectivity != null)
            {
                int component = connectivity.ComponentIdByCell[index];
                if (component >= 0)
                {
                    text.Append(string.Format(CultureInfo.InvariantCulture, "; area #{0} with {1} cells{2}",
                        component, connectivity.Components[component].CellCount,
                        component == connectivity.LargestComponentId ? " (main area)" : " (cut off)"));
                }
            }

            return text.ToString();
        }

        static string FlagNames(byte flags)
        {
            if (flags == 0)
            {
                return "none";
            }

            var names = new List<string>();
            foreach (CellFlags flag in new[]
            {
                CellFlags.Blocked, CellFlags.Building, CellFlags.Water, CellFlags.Barrier, CellFlags.Rail, CellFlags.Spawnable
            })
            {
                if ((flags & (byte)flag) != 0)
                {
                    names.Add(flag.ToString());
                }
            }

            return string.Join(", ", names);
        }
    }
}
