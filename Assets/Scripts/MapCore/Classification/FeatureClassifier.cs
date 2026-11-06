using System.Collections.Generic;
using System.Globalization;

// Based on my own earlier project that worked with OpenStreetMap (OSM) data; adapted here to feed the grid and the ECS map blob.
namespace HighProject.MapCore
{
    // Decides what an OSM element means for the map, from its tags only.
    public static class FeatureClassifier
    {
        public static FeatureKind Classify(IReadOnlyDictionary<string, string> tags, ClassificationOptions options)
        {
            FeatureKind kind = FeatureKind.None;

            string building = OsmTags.Get(tags, "building");
            if (building != null && building != "no")
            {
                kind |= options.OpenRoofBuildingTypes.Contains(building) ? FeatureKind.OpenRoof : FeatureKind.Building;
            }

            if (OsmTags.Is(tags, "natural", "water") || OsmTags.Is(tags, "waterway", "riverbank"))
            {
                kind |= FeatureKind.Water;
            }
            else if (OsmTags.Has(tags, "waterway"))
            {
                kind |= FeatureKind.Waterway;
            }

            if (OsmTags.Has(tags, "highway"))
            {
                kind |= FeatureKind.Highway;
            }

            if (OsmTags.Has(tags, "leisure"))
            {
                kind |= FeatureKind.Leisure;
            }

            if (OsmTags.Has(tags, "landuse"))
            {
                kind |= FeatureKind.Landuse;
            }

            if (OsmTags.Has(tags, "railway"))
            {
                kind |= FeatureKind.Railway;
            }

            string barrier = OsmTags.Get(tags, "barrier");
            if (barrier != null)
            {
                if (barrier == "gate")
                {
                    kind |= FeatureKind.Gate;
                }
                else if (options.HardBarrierTypes.Contains(barrier))
                {
                    kind |= FeatureKind.HardBarrier;
                }
                else
                {
                    kind |= FeatureKind.SoftBarrier;
                }
            }

            string bridge = OsmTags.Get(tags, "bridge");
            if (bridge != null && bridge != "no")
            {
                kind |= FeatureKind.Bridge;
            }

            string tunnel = OsmTags.Get(tags, "tunnel");
            if (tunnel == "building_passage")
            {
                kind |= FeatureKind.BuildingPassage;
            }
            else if ((tunnel != null && options.UndergroundTunnelTypes.Contains(tunnel))
                     || HasNegativeLayer(tags)
                     || OsmTags.Is(tags, "railway", "subway"))
            {
                kind |= FeatureKind.Underground;
            }

            string entrance = OsmTags.Get(tags, "entrance");
            if (entrance != null && entrance != "no")
            {
                kind |= FeatureKind.Entrance;
            }

            return kind;
        }

        static bool HasNegativeLayer(IReadOnlyDictionary<string, string> tags)
        {
            string layer = OsmTags.Get(tags, "layer");
            return layer != null
                   && int.TryParse(layer, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                   && value < 0;
        }
    }
}
