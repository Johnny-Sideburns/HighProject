using System.Collections.Generic;

namespace HighProject.MapCore
{
    // Applies GridOverrides to a grid. Terrain overrides (Block, then Open) run after the OSM blockers and before
    // the connectivity analysis; spawn overrides (Exclude, then Include) run after the spawn mask.
    // Within one kind the order of the list does not matter (each only adds or only removes), so the result is
    // the same whatever order the overrides were written in.
    public static class OverridePainter
    {
        const CellFlags SolidFlags = CellFlags.Building | CellFlags.Water | CellFlags.Barrier | CellFlags.Rail;

        public static void ApplyTerrain(GridOverrides overrides, MapGrid grid, OverrideReport report)
        {
            foreach (AreaOverride area in InKind(overrides, OverrideKind.Block))
            {
                int changed = 0;
                PolygonRasterizer.Fill(new AreaPolygon(area.Area, new Ring[0]), grid, index =>
                {
                    if (!grid.IsBlocked(index))
                    {
                        changed++;
                    }

                    grid.SetFlag(index, CellFlags.Blocked | CellFlags.Barrier);
                });
                report.Results.Add(new OverrideResult(area.Name, area.Kind, changed));
            }

            foreach (AreaOverride area in InKind(overrides, OverrideKind.Open))
            {
                int changed = 0;
                PolygonRasterizer.Fill(new AreaPolygon(area.Area, new Ring[0]), grid, index =>
                {
                    if (!grid.IsBlocked(index))
                    {
                        return;
                    }

                    grid.ClearFlag(index, CellFlags.Blocked | SolidFlags);
                    grid.BuildingId[index] = MapGrid.NoBuilding;
                    changed++;
                });
                report.Results.Add(new OverrideResult(area.Name, area.Kind, changed));
            }
        }

        public static void ApplySpawn(GridOverrides overrides, MapGrid grid, OverrideReport report)
        {
            foreach (AreaOverride area in InKind(overrides, OverrideKind.ExcludeFromSpawn))
            {
                int changed = 0;
                PolygonRasterizer.Fill(new AreaPolygon(area.Area, new Ring[0]), grid, index =>
                {
                    if (grid.HasFlag(index, CellFlags.Spawnable))
                    {
                        grid.ClearFlag(index, CellFlags.Spawnable);
                        changed++;
                    }
                });
                report.Results.Add(new OverrideResult(area.Name, area.Kind, changed));
            }

            foreach (AreaOverride area in InKind(overrides, OverrideKind.IncludeInSpawn))
            {
                int changed = 0;
                PolygonRasterizer.Fill(new AreaPolygon(area.Area, new Ring[0]), grid, index =>
                {
                    if (!grid.IsBlocked(index) && !grid.HasFlag(index, CellFlags.Spawnable))
                    {
                        grid.SetFlag(index, CellFlags.Spawnable);
                        changed++;
                    }
                });
                report.Results.Add(new OverrideResult(area.Name, area.Kind, changed));
            }
        }

        // Overrides of one kind in a fixed order (by name, then by position) so the report does not depend on the
        // order they were entered in.
        static IEnumerable<AreaOverride> InKind(GridOverrides overrides, OverrideKind kind)
        {
            var list = new List<AreaOverride>();
            foreach (AreaOverride area in overrides.Areas)
            {
                if (area.Kind == kind)
                {
                    list.Add(area);
                }
            }

            list.Sort((left, right) =>
            {
                int byName = string.CompareOrdinal(left.Name, right.Name);
                if (byName != 0)
                {
                    return byName;
                }

                int byX = left.Area.MinX.CompareTo(right.Area.MinX);
                return byX != 0 ? byX : left.Area.MinZ.CompareTo(right.Area.MinZ);
            });
            return list;
        }
    }
}
