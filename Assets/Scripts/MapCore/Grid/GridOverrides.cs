using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HighProject.MapCore
{
    public enum OverrideKind
    {
        // Makes the area impassable (a roadblock, a closed street).
        Block,

        // Makes the area walkable, whatever was there (a door in a fence, a gap in a wall, a passage through a building).
        // Wins over Block where both apply.
        Open,

        // Agents may not start in the area (they can still walk through it).
        ExcludeFromSpawn,

        // Agents may start in the area even if it is cut off from the main area (a sealed courtyard). Blocked cells stay
        // unusable. Wins over ExcludeFromSpawn where both apply.
        IncludeInSpawn
    }

    public sealed class AreaOverride
    {
        public string Name { get; }
        public OverrideKind Kind { get; }

        // Counter-clockwise outline in local metres (x east, z north).
        public Ring Area { get; }

        public AreaOverride(string name, OverrideKind kind, Ring area)
        {
            Name = name;
            Kind = kind;
            Area = area;
        }
    }

    // Hand-made corrections applied on top of what OpenStreetMap says. Areas are polygons in the same local metres
    // as the map (the Map Bake window shows them when you hover over the picture).
    public sealed class GridOverrides
    {
        public List<AreaOverride> Areas { get; } = new List<AreaOverride>();

        // Overrides that were refused, with the reason, so they can be reported instead of silently ignored.
        public List<string> Rejected { get; } = new List<string>();

        public bool IsEmpty => Areas.Count == 0 && Rejected.Count == 0;

        // Adds an area from its corner points (any orientation). Returns false and records why if the points do not
        // form an area.
        public bool TryAdd(string name, OverrideKind kind, IReadOnlyList<Point2d> points)
        {
            string label = string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name;

            if (points == null || points.Count < 3)
            {
                Rejected.Add($"'{label}' needs at least 3 points but has {(points == null ? 0 : points.Count)}.");
                return false;
            }

            foreach (Point2d point in points)
            {
                if (double.IsNaN(point.X) || double.IsNaN(point.Z) || double.IsInfinity(point.X) || double.IsInfinity(point.Z))
                {
                    Rejected.Add($"'{label}' has a point that is not a number.");
                    return false;
                }
            }

            var ring = new Ring(points.ToArray());
            if (ring.Area < 1e-6)
            {
                Rejected.Add($"'{label}' has no area (all points on one line).");
                return false;
            }

            Areas.Add(new AreaOverride(label, kind, ring.AsCounterClockwise()));
            return true;
        }
    }

    public sealed class OverrideResult
    {
        public string Name { get; }
        public OverrideKind Kind { get; }

        // How many cells the override really changed; 0 means it had no effect (outside the map, or nothing to change).
        public int CellsChanged { get; }

        public OverrideResult(string name, OverrideKind kind, int cellsChanged)
        {
            Name = name;
            Kind = kind;
            CellsChanged = cellsChanged;
        }
    }

    public sealed class OverrideReport
    {
        public List<OverrideResult> Results { get; } = new List<OverrideResult>();

        public override string ToString()
        {
            if (Results.Count == 0)
            {
                return "Overrides: none";
            }

            var text = new StringBuilder("Overrides:");
            foreach (OverrideResult result in Results)
            {
                text.Append(string.Format(CultureInfo.InvariantCulture, " {0} ({1}) changed {2} cells;", result.Name, result.Kind, result.CellsChanged));
            }

            return text.ToString().TrimEnd(';');
        }
    }
}
