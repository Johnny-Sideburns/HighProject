using System.Collections.Generic;

// Based on my own earlier project that worked with OpenStreetMap (OSM) data; adapted here to feed the grid and the ECS map blob.
namespace HighProject.MapCore
{
    public static class OsmTags
    {
        public static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

        public static bool Has(IReadOnlyDictionary<string, string> tags, string key)
        {
            return tags.ContainsKey(key);
        }

        public static string Get(IReadOnlyDictionary<string, string> tags, string key)
        {
            return tags.TryGetValue(key, out string value) ? value : null;
        }

        public static bool Is(IReadOnlyDictionary<string, string> tags, string key, string value)
        {
            return tags.TryGetValue(key, out string actual) && actual == value;
        }
    }
}
