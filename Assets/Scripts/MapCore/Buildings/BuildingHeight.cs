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
        // Heights outside this range are treated as typos and ignored.
        const double MaxPlausibleMeters = 1000.0;

        // Accepts "12", "12.5", "12 m", "12m" and the comma decimal separator "12,5". Rejects anything else (feet, ranges, text).
        public static bool TryParseMeters(string text, out double meters)
        {
            meters = 0.0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            if (trimmed.EndsWith("m"))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1).TrimEnd();
            }

            trimmed = trimmed.Replace(',', '.');
            if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return false;
            }

            if (value <= 0.0 || value > MaxPlausibleMeters)
            {
                return false;
            }

            meters = value;
            return true;
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
                && levels * options.MetersPerLevel <= MaxPlausibleMeters)
            {
                source = HeightSource.Levels;
                return levels * options.MetersPerLevel;
            }

            source = HeightSource.Default;
            return options.DefaultHeightMeters;
        }
    }
}
