using System;

namespace HighProject.MapCore
{
    // A regular grid over the map in local metres. Cell (x, z) covers
    // [OriginX + x * CellSize, OriginX + (x + 1) * CellSize) in x and likewise in z.
    // The three layers are separate arrays (struct of arrays): Flags and Cost are read by the movement code
    // every tick, BuildingId rarely.
    public sealed class MapGrid
    {
        public const byte DefaultOpenGroundCost = 3;
        public const int NoBuilding = -1;

        public double CellSize { get; }
        public double OriginX { get; }
        public double OriginZ { get; }
        public int Width { get; }
        public int Height { get; }

        public byte[] Flags { get; }
        public byte[] Cost { get; }
        public int[] BuildingId { get; }

        public int CellCount => Width * Height;

        public MapGrid(double originX, double originZ, int width, int height, double cellSize, byte defaultCost = DefaultOpenGroundCost)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "The grid needs at least one cell.");
            }

            if (cellSize <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "The cell size must be positive.");
            }

            OriginX = originX;
            OriginZ = originZ;
            Width = width;
            Height = height;
            CellSize = cellSize;

            Flags = new byte[width * height];
            Cost = new byte[width * height];
            BuildingId = new int[width * height];

            if (defaultCost != 0)
            {
                Array.Fill(Cost, defaultCost);
            }

            Array.Fill(BuildingId, NoBuilding);
        }

        // A grid that covers the given rectangle (in local metres), rounding the size up to whole cells.
        public static MapGrid CreateCovering(double minX, double minZ, double maxX, double maxZ, double cellSize, byte defaultCost = DefaultOpenGroundCost)
        {
            int width = Math.Max(1, (int)Math.Ceiling((maxX - minX) / cellSize));
            int height = Math.Max(1, (int)Math.Ceiling((maxZ - minZ) / cellSize));
            return new MapGrid(minX, minZ, width, height, cellSize, defaultCost);
        }

        public int Index(int x, int z)
        {
            return z * Width + x;
        }

        public int CellX(int index)
        {
            return index % Width;
        }

        public int CellZ(int index)
        {
            return index / Width;
        }

        public bool InBounds(int x, int z)
        {
            return x >= 0 && x < Width && z >= 0 && z < Height;
        }

        // Cell containing a world position. May be outside the grid; check with InBounds.
        public int CellXOf(double worldX)
        {
            return (int)Math.Floor((worldX - OriginX) / CellSize);
        }

        public int CellZOf(double worldZ)
        {
            return (int)Math.Floor((worldZ - OriginZ) / CellSize);
        }

        public double CellCenterX(int x)
        {
            return OriginX + (x + 0.5) * CellSize;
        }

        public double CellCenterZ(int z)
        {
            return OriginZ + (z + 0.5) * CellSize;
        }

        public bool HasFlag(int index, CellFlags flag)
        {
            return (Flags[index] & (byte)flag) != 0;
        }

        public void SetFlag(int index, CellFlags flag)
        {
            Flags[index] |= (byte)flag;
        }

        public void ClearFlag(int index, CellFlags flag)
        {
            Flags[index] &= (byte)~(byte)flag;
        }

        public bool IsBlocked(int index)
        {
            return (Flags[index] & (byte)CellFlags.Blocked) != 0;
        }

        public int CountFlag(CellFlags flag)
        {
            int count = 0;
            for (int i = 0; i < Flags.Length; i++)
            {
                if ((Flags[i] & (byte)flag) != 0)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
