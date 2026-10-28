using System.Collections.Generic;

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
