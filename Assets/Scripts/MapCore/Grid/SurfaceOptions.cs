using System;
using System.Collections.Generic;

namespace HighProject.MapCore
{
    // How highway classes map to surfaces, how wide they are drawn and what each surface costs to cross.
    // Plain data so the editor tool can expose it as settings later.
    public sealed class SurfaceOptions
    {
        // Movement cost per SurfaceKind (lower = preferred). Index with (int)SurfaceKind.
        public byte[] CostBySurface { get; } = CreateDefaultCosts();

        // Width in metres when a way has no usable width tag.
        public Dictionary<string, double> DefaultHighwayWidths { get; } = new Dictionary<string, double>
        {
            { "footway", 2.0 }, { "path", 1.5 }, { "pedestrian", 5.0 }, { "cycleway", 2.5 }, { "bridleway", 2.0 },
            { "platform", 3.0 }, { "steps", 2.0 },
            { "service", 4.0 }, { "residential", 6.0 }, { "living_street", 6.0 }, { "unclassified", 6.0 }, { "track", 3.0 },
            { "tertiary", 8.0 }, { "tertiary_link", 5.0 }, { "secondary", 10.0 }, { "secondary_link", 6.0 },
            { "primary", 12.0 }, { "primary_link", 7.0 }, { "trunk", 12.0 }, { "trunk_link", 7.0 },
            { "motorway", 14.0 }, { "motorway_link", 7.0 }
        };

        // Used for highway values that are not in DefaultHighwayWidths but still get painted.
        public double FallbackHighwayWidth { get; set; } = 4.0;

        static byte[] CreateDefaultCosts()
        {
            var costs = new byte[Enum.GetValues(typeof(SurfaceKind)).Length];
            costs[(int)SurfaceKind.Ground] = 3;
            costs[(int)SurfaceKind.Grass] = 2;
            costs[(int)SurfaceKind.Woodland] = 4;
            costs[(int)SurfaceKind.Plaza] = 1;
            costs[(int)SurfaceKind.Footpath] = 1;
            costs[(int)SurfaceKind.Steps] = 5;
            costs[(int)SurfaceKind.MinorRoad] = 2;
            costs[(int)SurfaceKind.MajorRoad] = 3;
            return costs;
        }

        // Which surface a highway=* value paints, or null if it is not painted
        // (proposed or under construction roads, indoor corridors, elevators, ...).
        public SurfaceKind? SurfaceForHighway(string highway)
        {
            switch (highway)
            {
                case "footway":
                case "path":
                case "pedestrian":
                case "cycleway":
                case "bridleway":
                case "platform":
                    return SurfaceKind.Footpath;
                case "steps":
                    return SurfaceKind.Steps;
                case "service":
                case "residential":
                case "living_street":
                case "unclassified":
                case "track":
                    return SurfaceKind.MinorRoad;
                case "tertiary":
                case "tertiary_link":
                case "secondary":
                case "secondary_link":
                case "primary":
                case "primary_link":
                case "trunk":
                case "trunk_link":
                case "motorway":
                case "motorway_link":
                    return SurfaceKind.MajorRoad;
                default:
                    return null;
            }
        }

        // Which surface an area (leisure/landuse/natural) paints, or null if it does not change the ground.
        public SurfaceKind? SurfaceForArea(IReadOnlyDictionary<string, string> tags)
        {
            string highway = OsmTags.Get(tags, "highway");
            if (highway == "pedestrian" && OsmTags.Is(tags, "area", "yes"))
            {
                return SurfaceKind.Plaza;
            }

            switch (OsmTags.Get(tags, "leisure"))
            {
                case "park":
                case "garden":
                case "playground":
                case "pitch":
                case "dog_park":
                case "recreation_ground":
                case "common":
                    return SurfaceKind.Grass;
                case "outdoor_seating":
                    return SurfaceKind.Plaza;
            }

            switch (OsmTags.Get(tags, "landuse"))
            {
                case "grass":
                case "meadow":
                case "flowerbed":
                case "recreation_ground":
                case "village_green":
                case "cemetery":
                    return SurfaceKind.Grass;
                case "forest":
                    return SurfaceKind.Woodland;
            }

            if (OsmTags.Is(tags, "natural", "shrubbery") || OsmTags.Is(tags, "natural", "wood"))
            {
                return SurfaceKind.Woodland;
            }

            return null;
        }

        public double HighwayWidth(IReadOnlyDictionary<string, string> tags, string highway)
        {
            if (OsmMeasure.TryParseMeters(OsmTags.Get(tags, "width"), out double width))
            {
                return width;
            }

            return DefaultHighwayWidths.TryGetValue(highway, out double byClass) ? byClass : FallbackHighwayWidth;
        }
    }
}
