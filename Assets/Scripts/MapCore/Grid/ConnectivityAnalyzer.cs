using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HighProject.MapCore
{
    public sealed class ComponentInfo
    {
        public int Id { get; }
        public int CellCount { get; }

        // Bounding box in cell coordinates (inclusive).
        public int MinX { get; }
        public int MinZ { get; }
        public int MaxX { get; }
        public int MaxZ { get; }

        // The surface that covers most of the component's cells.
        public SurfaceKind DominantSurface { get; }

        public ComponentInfo(int id, int cellCount, int minX, int minZ, int maxX, int maxZ, SurfaceKind dominantSurface)
        {
            Id = id;
            CellCount = cellCount;
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
            DominantSurface = dominantSurface;
        }
    }

    public sealed class ConnectivityResult
    {
        // Component id per cell, or -1 for blocked cells. Ids follow the scan order of the grid (row by row,
        // starting at cell 0), so they are the same on every machine.
        public int[] ComponentIdByCell { get; }

        // Components in id order.
        public IReadOnlyList<ComponentInfo> Components { get; }

        public int WalkableCells { get; }

        // The component with the most cells (the lowest id wins a tie), or -1 when nothing is walkable.
        public int LargestComponentId { get; }

        public ConnectivityResult(int[] componentIdByCell, IReadOnlyList<ComponentInfo> components, int walkableCells)
        {
            ComponentIdByCell = componentIdByCell;
            Components = components;
            WalkableCells = walkableCells;

            int largest = -1;
            int largestCount = 0;
            foreach (ComponentInfo component in components)
            {
                if (component.CellCount > largestCount)
                {
                    largest = component.Id;
                    largestCount = component.CellCount;
                }
            }

            LargestComponentId = largest;
        }

        // Text summary with the largest components, their location in local metres and what they are made of.
        public string Describe(MapGrid grid, int topCount = 8)
        {
            var text = new StringBuilder();
            int largest = LargestComponentId >= 0 ? Components[LargestComponentId].CellCount : 0;
            int smallIslands = Components.Count(component => component.CellCount < 100);

            text.AppendLine("Connectivity");
            text.AppendLine(Line("  Walkable cells: {0}; components: {1} ({2} with fewer than 100 cells)", WalkableCells, Components.Count, smallIslands));
            if (WalkableCells > 0)
            {
                text.AppendLine(Line("  Largest component: {0} cells = {1:F1}% of the walkable cells", largest, 100.0 * largest / WalkableCells));
            }

            foreach (ComponentInfo component in Components.OrderByDescending(c => c.CellCount).ThenBy(c => c.Id).Take(topCount))
            {
                double minX = grid.OriginX + component.MinX * grid.CellSize;
                double minZ = grid.OriginZ + component.MinZ * grid.CellSize;
                double maxX = grid.OriginX + (component.MaxX + 1) * grid.CellSize;
                double maxZ = grid.OriginZ + (component.MaxZ + 1) * grid.CellSize;
                text.AppendLine(Line("    #{0}: {1} cells, mostly {2}, x {3:F0}..{4:F0}, z {5:F0}..{6:F0}",
                    component.Id, component.CellCount, component.DominantSurface, minX, maxX, minZ, maxZ));
            }

            return text.ToString();
        }

        static string Line(string format, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }
    }

    // Finds the connected areas of walkable cells. Agents move in free 2D but cannot squeeze diagonally between
    // two blocked corners, so a path exists exactly when the cells are connected through shared edges
    // (4-neighbour connectivity). Iterative (no recursion), so it is safe on large grids.
    public static class ConnectivityAnalyzer
    {
        public static ConnectivityResult Analyze(MapGrid grid)
        {
            int width = grid.Width;
            int height = grid.Height;
            var componentIdByCell = new int[grid.CellCount];
            Array.Fill(componentIdByCell, -1);

            var components = new List<ComponentInfo>();
            var queue = new int[grid.CellCount];
            var surfaceCounts = new int[Enum.GetValues(typeof(SurfaceKind)).Length];
            int walkable = 0;

            for (int start = 0; start < grid.CellCount; start++)
            {
                if (grid.IsBlocked(start))
                {
                    continue;
                }

                walkable++;
                if (componentIdByCell[start] >= 0)
                {
                    continue;
                }

                int id = components.Count;
                int head = 0;
                int tail = 0;
                queue[tail++] = start;
                componentIdByCell[start] = id;
                Array.Clear(surfaceCounts, 0, surfaceCounts.Length);

                int minX = int.MaxValue;
                int minZ = int.MaxValue;
                int maxX = int.MinValue;
                int maxZ = int.MinValue;

                while (head < tail)
                {
                    int cell = queue[head++];
                    int x = cell % width;
                    int z = cell / width;

                    surfaceCounts[grid.Surface[cell]]++;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (z < minZ) minZ = z;
                    if (z > maxZ) maxZ = z;

                    if (x > 0) Visit(grid, componentIdByCell, queue, ref tail, cell - 1, id);
                    if (x < width - 1) Visit(grid, componentIdByCell, queue, ref tail, cell + 1, id);
                    if (z > 0) Visit(grid, componentIdByCell, queue, ref tail, cell - width, id);
                    if (z < height - 1) Visit(grid, componentIdByCell, queue, ref tail, cell + width, id);
                }

                components.Add(new ComponentInfo(id, tail, minX, minZ, maxX, maxZ, DominantSurface(surfaceCounts)));
            }

            return new ConnectivityResult(componentIdByCell, components, walkable);
        }

        static void Visit(MapGrid grid, int[] componentIdByCell, int[] queue, ref int tail, int neighbour, int id)
        {
            if (componentIdByCell[neighbour] < 0 && !grid.IsBlocked(neighbour))
            {
                componentIdByCell[neighbour] = id;
                queue[tail++] = neighbour;
            }
        }

        static SurfaceKind DominantSurface(int[] counts)
        {
            int best = 0;
            for (int i = 1; i < counts.Length; i++)
            {
                if (counts[i] > counts[best])
                {
                    best = i;
                }
            }

            return (SurfaceKind)best;
        }
    }
}
