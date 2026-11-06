using System;
using System.Collections.Generic;

namespace HighProject.MapCore
{
    // Marks the grid cells near a polyline (roads, barriers, rails). A cell is covered when its centre is
    // within half the line width of the line.
    public static class LineRasterizer
    {
        // Smallest half width that cannot leave gaps: every cell a line passes through has its centre within
        // half a cell diagonal of the line, so the covered cells form a connected, leak-free chain.
        public static double WatertightHalfWidth(double cellSize)
        {
            return cellSize * Math.Sqrt(2.0) * 0.5;
        }

        // Calls visit(cellIndex) for the covered cells. A cell next to two segments can be reported more than once,
        // so visit must be safe to call repeatedly (setting a flag is). minHalfWidth lets thin lines such as walls
        // still form a closed barrier at coarse resolution; pass 0 to use the true width only.
        public static void Stroke(
            IReadOnlyList<Point2d> polyline,
            double width,
            double minHalfWidth,
            MapGrid grid,
            Action<int> visit)
        {
            double halfWidth = Math.Max(width * 0.5, minHalfWidth);

            if (polyline.Count == 1)
            {
                StrokeSegment(polyline[0], polyline[0], halfWidth, grid, visit);
                return;
            }

            for (int i = 0; i + 1 < polyline.Count; i++)
            {
                StrokeSegment(polyline[i], polyline[i + 1], halfWidth, grid, visit);
            }
        }

        static void StrokeSegment(Point2d a, Point2d b, double halfWidth, MapGrid grid, Action<int> visit)
        {
            int firstX = Math.Max(0, grid.CellXOf(Math.Min(a.X, b.X) - halfWidth));
            int lastX = Math.Min(grid.Width - 1, grid.CellXOf(Math.Max(a.X, b.X) + halfWidth));
            int firstZ = Math.Max(0, grid.CellZOf(Math.Min(a.Z, b.Z) - halfWidth));
            int lastZ = Math.Min(grid.Height - 1, grid.CellZOf(Math.Max(a.Z, b.Z) + halfWidth));

            for (int z = firstZ; z <= lastZ; z++)
            {
                double centerZ = grid.CellCenterZ(z);
                for (int x = firstX; x <= lastX; x++)
                {
                    var center = new Point2d(grid.CellCenterX(x), centerZ);
                    if (SegmentMath.DistanceToSegment(center, a, b) <= halfWidth)
                    {
                        visit(grid.Index(x, z));
                    }
                }
            }
        }
    }
}
