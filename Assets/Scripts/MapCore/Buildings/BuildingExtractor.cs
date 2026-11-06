using System.Collections.Generic;

// Based on my own earlier project that worked with OpenStreetMap (OSM) data; adapted here to feed the grid and the ECS map blob.
namespace HighProject.MapCore
{
    // Collects the solid building footprints (ways and multipolygon relations) from an OSM document.
    // Open roofs (building=roof, carport) are not footprints, see FeatureKind.OpenRoof.
    public static class BuildingExtractor
    {
        public static BuildingExtractionResult Extract(
            OsmDocument document,
            LocalProjection projection,
            ClassificationOptions classification,
            BuildingHeightOptions heights)
        {
            var result = new BuildingExtractionResult();

            HashSet<long> relationMemberWays = CollectBuildingRelationMemberWays(document, classification);

            foreach (OsmWay way in document.Ways)
            {
                if ((FeatureClassifier.Classify(way.Tags, classification) & FeatureKind.Building) == 0)
                {
                    continue;
                }

                if (relationMemberWays.Contains(way.Id))
                {
                    result.DuplicateWaysSkipped++;
                    continue;
                }

                RingProblem problem = WayGeometry.TryGetRing(way.NodeIds, document, projection, out Ring ring);
                if (problem != RingProblem.None)
                {
                    result.WayProblems.TryGetValue(problem, out int count);
                    result.WayProblems[problem] = count + 1;
                    continue;
                }

                var polygon = new AreaPolygon(ring.AsCounterClockwise(), new Ring[0]);
                AddFootprint(result, way.Id, false, polygon, way.Tags, heights);
            }

            foreach (OsmRelation relation in document.Relations)
            {
                if ((FeatureClassifier.Classify(relation.Tags, classification) & FeatureKind.Building) == 0)
                {
                    continue;
                }

                if (!OsmTags.Is(relation.Tags, "type", "multipolygon"))
                {
                    result.RelationsIgnored++;
                    continue;
                }

                MultipolygonResult multipolygon = MultipolygonBuilder.Build(relation, document, projection);
                result.MissingMemberWays += multipolygon.MissingMemberWays;
                result.OpenChains += multipolygon.OpenChains;
                result.InvalidRings += multipolygon.InvalidRings;
                result.UnassignedHoles += multipolygon.UnassignedHoles;

                if (multipolygon.Polygons.Count == 0)
                {
                    result.RelationsWithoutPolygons++;
                    continue;
                }

                foreach (AreaPolygon polygon in multipolygon.Polygons)
                {
                    AddFootprint(result, relation.Id, true, polygon, relation.Tags, heights);
                }
            }

            return result;
        }

        static void AddFootprint(
            BuildingExtractionResult result,
            long osmId,
            bool isRelation,
            AreaPolygon polygon,
            IReadOnlyDictionary<string, string> tags,
            BuildingHeightOptions heights)
        {
            double height = BuildingHeight.Resolve(tags, heights, out HeightSource source);
            string type = OsmTags.Get(tags, "building");
            result.Footprints.Add(new BuildingFootprint(result.Footprints.Count, osmId, isRelation, polygon, height, source, type));
        }

        static HashSet<long> CollectBuildingRelationMemberWays(OsmDocument document, ClassificationOptions classification)
        {
            var memberWays = new HashSet<long>();
            foreach (OsmRelation relation in document.Relations)
            {
                if ((FeatureClassifier.Classify(relation.Tags, classification) & FeatureKind.Building) == 0
                    || !OsmTags.Is(relation.Tags, "type", "multipolygon"))
                {
                    continue;
                }

                foreach (OsmMember member in relation.Members)
                {
                    if (member.Type == OsmMemberType.Way)
                    {
                        memberWays.Add(member.Ref);
                    }
                }
            }

            return memberWays;
        }
    }
}
