namespace HighProject.MapCore
{
    // What the ground of a cell is made of, independent of whether it can be entered (that is CellFlags.Blocked).
    // Used for movement cost now and for visuals and behaviour later.
    public enum SurfaceKind : byte
    {
        // Open ground with no better information (courtyards, squares not mapped as plazas, ...).
        Ground = 0,

        // Parks, gardens, lawns, pitches, playgrounds.
        Grass = 1,

        // landuse=forest and similar.
        Woodland = 2,

        // Pedestrian areas (highway=pedestrian mapped as an area).
        Plaza = 3,

        // footway, path, pedestrian streets, cycleway.
        Footpath = 4,

        Steps = 5,

        // service, residential, living_street, unclassified, track.
        MinorRoad = 6,

        // tertiary and bigger roads.
        MajorRoad = 7
    }
}
