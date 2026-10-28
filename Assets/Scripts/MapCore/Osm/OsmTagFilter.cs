using System.Collections.Generic;

namespace HighProject.MapCore
{
    // Only these tag keys are kept while reading, which keeps memory small on big extracts.
    public static class OsmTagFilter
    {
        static readonly HashSet<string> RelevantKeys = new HashSet<string>
        {
            "building", "building:levels", "height", "min_height", "roof:levels",
            "highway", "width", "surface", "area", "access", "foot",
            "natural", "waterway", "leisure", "landuse", "railway",
            "barrier", "entrance", "bridge", "tunnel", "layer", "covered",
            "type"
        };

        public static bool IsRelevantKey(string key)
        {
            return RelevantKeys.Contains(key);
        }
    }
}
