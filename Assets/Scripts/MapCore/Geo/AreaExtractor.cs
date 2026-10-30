using System;
using System.Collections.Generic;

namespace HighProject.MapCore
{
    // A polygon taken from a closed way or a multipolygon relation, with the tags of that element.
    public sealed class AreaFeature
    {
        public long OsmId { get; }
        public bool IsRelation { get; }
        public AreaPolygon Polygon { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }

        public AreaFeature(long osmId, bool isRelation, AreaPolygon polygon, IReadOnlyDictionary<string, string> tags)
        {
            OsmId = osmId;
            IsRelation = isRelation;
            Polygon = polygon;
            Tags = tags;
        }
    }

    // Collects polygons for any kind of area (parks, lawns, plazas, ...) selected by a tag test.
    // Closed ways become polygons without holes, multipolygon relations become polygons with holes.
    // Unclosed ways are skipped (they are lines, not areas).
    public static class AreaExtractor
    {
        public static List<AreaFeature> Extract(
            OsmDocument document,
            LocalProjection projection,
            Func<IReadOnlyDictionary<string, string>, bool> isWanted)
        {
            var areas = new List<AreaFeature>();
            var consumedWays = new HashSet<long>();

            // Relations first, so that ways which only repeat a relation's outline are not painted twice.
            var relationAreas = new List<AreaFeature>();
            foreach (OsmRelation relation in document.Relations)
            {
                if (!OsmTags.Is(relation.Tags, "type", "multipolygon") || !isWanted(relation.Tags))
                {
                    continue;
                }

                foreach (OsmMember member in relation.Members)
                {
                    if (member.Type == OsmMemberType.Way)
                    {
                        consumedWays.Add(member.Ref);
                    }
                }

                MultipolygonResult result = MultipolygonBuilder.Build(relation, document, projection);
                foreach (AreaPolygon polygon in result.Polygons)
                {
                    relationAreas.Add(new AreaFeature(relation.Id, true, polygon, relation.Tags));
                }
            }

            foreach (OsmWay way in document.Ways)
            {
                if (!isWanted(way.Tags) || consumedWays.Contains(way.Id) || !way.IsClosed)
                {
                    continue;
                }

                if (WayGeometry.TryGetRing(way.NodeIds, document, projection, out Ring ring) == RingProblem.None)
                {
                    areas.Add(new AreaFeature(way.Id, false, new AreaPolygon(ring.AsCounterClockwise(), new Ring[0]), way.Tags));
                }
            }

            areas.AddRange(relationAreas);
            return areas;
        }
    }
}
