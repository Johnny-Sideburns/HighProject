using System.Runtime.CompilerServices;
using HighProject.MapCore;
using Unity.Mathematics;

namespace HighProject.Map
{
    // Read access to the map for systems and jobs. All methods are Burst compatible.
    // Positions are local metres: x east, y north (the scene's x and z axes).
    public static class MapBlobQueries
    {
        public static int CellCount(this ref MapBlob map)
        {
            return map.Width * map.Height;
        }

        public static bool InBounds(this ref MapBlob map, int x, int z)
        {
            return x >= 0 && x < map.Width && z >= 0 && z < map.Height;
        }

        public static int Index(this ref MapBlob map, int x, int z)
        {
            return z * map.Width + x;
        }

        // The cell containing a position, or false if the position is outside the map.
        public static bool TryGetCell(this ref MapBlob map, float2 position, out int x, out int z)
        {
            float2 local = (position - map.Origin) / map.CellSize;
            x = (int)math.floor(local.x);
            z = (int)math.floor(local.y);
            return map.InBounds(x, z);
        }

        // Centre of a cell in local metres.
        public static float2 CellCenter(this ref MapBlob map, int x, int z)
        {
            return map.Origin + (new float2(x, z) + 0.5f) * map.CellSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBlocked(this ref MapBlob map, int index)
        {
            return (map.Flags[index] & (byte)CellFlags.Blocked) != 0;
        }

        // Cells where agents may start: walkable and part of the main connected area.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSpawnable(this ref MapBlob map, int index)
        {
            return (map.Flags[index] & (byte)CellFlags.Spawnable) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasFlag(this ref MapBlob map, int index, CellFlags flag)
        {
            return (map.Flags[index] & (byte)flag) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte CostAt(this ref MapBlob map, int index)
        {
            return map.Cost[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SurfaceKind SurfaceAt(this ref MapBlob map, int index)
        {
            return (SurfaceKind)map.Surface[index];
        }

        // Index of the building covering this cell, or -1.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BuildingAt(this ref MapBlob map, int index)
        {
            return map.BuildingId[index];
        }

        // A position is walkable if it is inside the map and its cell is not blocked.
        public static bool IsWalkable(this ref MapBlob map, float2 position)
        {
            return map.TryGetCell(position, out int x, out int z) && !map.IsBlocked(map.Index(x, z));
        }
    }
}
