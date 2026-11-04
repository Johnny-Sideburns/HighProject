using System.Globalization;

// Based on my own earlier project that worked with OpenStreetMap (OSM) data; adapted here to feed the grid and the ECS map blob.
namespace HighProject.MapCore
{
    public static class OsmMeasure
    {
        // Values above this are treated as typos and ignored.
        public const double MaxPlausibleMeters = 1000.0;

        // Accepts "12", "12.5", "12 m", "12m" and the comma decimal separator "12,5". Rejects anything else
        // (feet, ranges, text), non-positive values and implausibly large ones.
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
    }
}
