using System;

namespace HighProject.MapCore
{
    // Per-cell facts about the ground. Stored as one byte per cell.
    [Flags]
    public enum CellFlags : byte
    {
        None = 0,

        // Agents cannot enter this cell (building, water, hard barrier, fenced rail).
        Blocked = 1 << 0,

        Building = 1 << 1,
        Water = 1 << 2,
        Barrier = 1 << 3,
        Rail = 1 << 4,

        // Walkable cell that belongs to the main connected walkable area (set by the connectivity step).
        Spawnable = 1 << 5
    }
}
