using System.Collections.Generic;
using System.Globalization;

namespace HighProject.MapCore
{
    public enum HeightSource
    {
        Default,
        Levels,
        HeightTag
    }

    // Fallbacks for buildings without usable height information. Plain data, exposed as settings later.
    public sealed class BuildingHeightOptions
    {
        public double DefaultHeightMeters { get; set; } = 15.0;
        public double MetersPerLevel { get; set; } = 3.0;
    }

    public static class BuildingHeight
    {
        // Accepts "12", "12.5", "12 m", "12m" and "12,5"; see OsmMeasure.
        public static bool TryParseMeters(string text, out double meters)
        {
            return OsmMeasure.TryParseMeters(text, out meters);
        }

        // height tag first, then building:levels, then the default.
        public static double Resolve(IReadOnlyDictionary<string, string> tags, BuildingHeightOptions options, out HeightSource source)
        {
            if (TryParseMeters(OsmTags.Get(tags, "height"), out double height))
            {
                source = HeightSource.HeightTag;
                return height;
            }

            string levelsText = OsmTags.Get(tags, "building:levels");
            if (levelsText != null
                && double.TryParse(levelsText.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double levels)
                && levels > 0.0
                && levels * options.MetersPerLevel <= OsmMeasure.MaxPlausibleMeters)
            {
                source = HeightSource.Levels;
                return levels * options.MetersPerLevel;
            }

            source = HeightSource.Default;
            return options.DefaultHeightMeters;
        }
    }
}
