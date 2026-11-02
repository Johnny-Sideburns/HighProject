using System;

namespace HighProject.MapCore
{
    // A regular grid over the map in local metres. Cell (x, z) covers
    // [OriginX + x * CellSize, OriginX + (x + 1) * CellSize) in x and likewise in z.
    // The layers are separate arrays (struct of arrays): Flags and Cost are read by the movement code
    // every tick, Surface and BuildingId rarely.
    public sealed class MapGrid
    {
        public const byte DefaultOpenGroundCost = 3;
        public const int NoBuilding = -1;
        public const byte BlockedCost = 255;

        public double CellSize { get; }
        public double OriginX { get; }
        public double OriginZ { get; }
        public int Width { get; }
        public int Height { get; }

        public byte[] Flags { get; }
        public byte[] Surface { get; }
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
            Surface = new byte[width * height];
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

        public SurfaceKind SurfaceAt(int index)
        {
            return (SurfaceKind)Surface[index];
        }

        public void SetSurface(int index, SurfaceKind surface)
        {
            Surface[index] = (byte)surface;
        }

        // Recomputes Cost from Surface for every cell. Blocked cells get cost 255 ("never enter").
        public void ApplySurfaceCosts(byte[] costBySurface)
        {
            for (int i = 0; i < Cost.Length; i++)
            {
                Cost[i] = (Flags[i] & (byte)CellFlags.Blocked) != 0 ? BlockedCost : costBySurface[Surface[i]];
            }
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

        // True if at least one of the given flags is set.
        public bool HasAnyFlag(int index, CellFlags flags)
        {
            return (Flags[index] & (byte)flags) != 0;
        }

        public bool IsBlocked(int index)
        {
            return (Flags[index] & (byte)CellFlags.Blocked) != 0;
        }

        // 64-bit FNV-1a over the grid size and all layers. Two grids with the same content have the same checksum,
        // which makes it easy to see that two bakes (or a threaded and a single-threaded bake) agree.
        public ulong ComputeChecksum()
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;

            hash = Mix(hash, (ulong)Width, prime);
            hash = Mix(hash, (ulong)Height, prime);
            hash = Mix(hash, (ulong)BitConverter.DoubleToInt64Bits(CellSize), prime);
            hash = Mix(hash, (ulong)BitConverter.DoubleToInt64Bits(OriginX), prime);
            hash = Mix(hash, (ulong)BitConverter.DoubleToInt64Bits(OriginZ), prime);

            for (int i = 0; i < Flags.Length; i++)
            {
                hash = (hash ^ Flags[i]) * prime;
                hash = (hash ^ Surface[i]) * prime;
                hash = (hash ^ Cost[i]) * prime;
                hash = Mix(hash, (ulong)(uint)BuildingId[i], prime);
            }

            return hash;
        }

        static ulong Mix(ulong hash, ulong value, ulong prime)
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash = (hash ^ ((value >> shift) & 0xFF)) * prime;
            }

            return hash;
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
