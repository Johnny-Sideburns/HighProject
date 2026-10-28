using System.Collections.Generic;

namespace HighProject.MapCore
{
    // Which tag values count as what. Plain data so the editor tool can expose it as settings later.
    public sealed class ClassificationOptions
    {
        // barrier values that block movement.
        public HashSet<string> HardBarrierTypes { get; } = new HashSet<string>
        {
            "wall", "retaining_wall", "fence", "hedge", "guard_rail", "city_wall"
        };

        // barrier values that are only decoration for movement purposes.
        public HashSet<string> SoftBarrierTypes { get; } = new HashSet<string>
        {
            "kerb", "planter", "bollard", "cycle_barrier", "block"
        };

        // building=* values that do not form a solid footprint.
        public HashSet<string> OpenRoofBuildingTypes { get; } = new HashSet<string>
        {
            "roof", "carport", "canopy"
        };

        // tunnel=* values that mean the way is underground.
        public HashSet<string> UndergroundTunnelTypes { get; } = new HashSet<string>
        {
            "yes", "culvert"
        };
    }
}
