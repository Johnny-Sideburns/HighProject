using System;

namespace HighProject.MapCore
{
    // What an OSM element means for the simulation map. One element can have several kinds,
    // e.g. a bridge is both Highway and Bridge.
    [Flags]
    public enum FeatureKind
    {
        None = 0,

        // Solid footprint (blocks movement). Excludes open roofs, see OpenRoof.
        Building = 1 << 0,

        // building=roof, carport, ...: a roof over walkable ground, must not block movement.
        OpenRoof = 1 << 1,

        // Area water (natural=water, waterway=riverbank). Blocks movement.
        Water = 1 << 2,

        // Linear waterway (canal, ditch, stream, ...). Not blocking in v1.
        Waterway = 1 << 3,

        Highway = 1 << 4,
        Leisure = 1 << 5,
        Landuse = 1 << 6,
        Railway = 1 << 7,

        // barrier=wall/fence/hedge/... (configurable). Blocks movement.
        HardBarrier = 1 << 8,

        // barrier=kerb, planter, ... Does not block in v1.
        SoftBarrier = 1 << 9,

        // barrier=gate: an opening in a hard barrier.
        Gate = 1 << 10,

        // bridge=yes etc.: restores walkability across water or other blockers beneath.
        Bridge = 1 << 11,

        // tunnel=yes/culvert, layer below 0, or subway: not on the surface, ignored for the map.
        Underground = 1 << 12,

        // tunnel=building_passage: a walkable passage through a building at ground level.
        BuildingPassage = 1 << 13,

        // entrance=* on a node.
        Entrance = 1 << 14
    }
}
