using System;
using System.Collections.Generic;

namespace HighProject.MapCore
{
    // Marks the grid cells whose centre lies inside a polygon (outer ring minus holes, even-odd rule).
    // Deterministic and free of races: it only reports cell indices, the caller decides what to write.
    public static class PolygonRasterizer
    {
        // Calls visit(cellIndex) once for every covered cell and returns how many cells that was.
        // Polygons that stick out of the grid are clipped.
        public static int Fill(AreaPolygon polygon, MapGrid grid, Action<int> visit)
        {
            Ring outer = polygon.Outer;
            double cellSize = grid.CellSize;

            // Rows whose centre line lies in [MinZ, MaxZ). Edges use the half-open rule below, so a row is
            // only crossed by an edge that really spans it, and no vertex is counted twice.
            int firstRow = Math.Max(0, (int)Math.Ceiling((outer.MinZ - grid.OriginZ) / cellSize - 0.5));
            int lastRow = Math.Min(grid.Height - 1, (int)Math.Ceiling((outer.MaxZ - grid.OriginZ) / cellSize - 0.5) - 1);

            var crossings = new List<double>();
            int covered = 0;

            for (int z = firstRow; z <= lastRow; z++)
            {
                double rowZ = grid.CellCenterZ(z);

                crossings.Clear();
                AddCrossings(outer, rowZ, crossings);
                foreach (Ring hole in polygon.Holes)
                {
                    AddCrossings(hole, rowZ, crossings);
                }

                crossings.Sort();

                // Pairs of crossings bound the inside spans (even-odd).
                for (int i = 0; i + 1 < crossings.Count; i += 2)
                {
                    // Cells whose centre x lies in [crossings[i], crossings[i + 1]).
                    int firstColumn = Math.Max(0, (int)Math.Ceiling((crossings[i] - grid.OriginX) / cellSize - 0.5));
                    int lastColumn = Math.Min(grid.Width - 1, (int)Math.Ceiling((crossings[i + 1] - grid.OriginX) / cellSize - 0.5) - 1);

                    for (int x = firstColumn; x <= lastColumn; x++)
                    {
                        visit(grid.Index(x, z));
                        covered++;
                    }
                }
            }

            return covered;
        }

        static void AddCrossings(Ring ring, double rowZ, List<double> crossings)
        {
            Point2d[] points = ring.Points;
            int previous = points.Length - 1;
            for (int i = 0; i < points.Length; i++)
            {
                Point2d a = points[previous];
                Point2d b = points[i];
                if ((a.Z <= rowZ) != (b.Z <= rowZ))
                {
                    crossings.Add(a.X + (rowZ - a.Z) * (b.X - a.X) / (b.Z - a.Z));
                }

                previous = i;
            }
        }
    }
}
