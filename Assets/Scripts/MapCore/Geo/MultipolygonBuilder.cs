using System.Collections.Generic;

namespace HighProject.MapCore
{
    public sealed class MultipolygonResult
    {
        public List<AreaPolygon> Polygons { get; } = new List<AreaPolygon>();

        // Member ways that the file does not contain.
        public int MissingMemberWays { get; set; }

        // Pieces that could not be joined into a closed ring.
        public int OpenChains { get; set; }

        // Closed chains that are not usable rings (missing nodes, too few points, no area).
        public int InvalidRings { get; set; }

        // Inner rings that lie inside no outer ring.
        public int UnassignedHoles { get; set; }
    }

    // Turns a multipolygon relation into polygons with holes. Tolerant: problems are counted, never thrown.
    public static class MultipolygonBuilder
    {
        public static MultipolygonResult Build(OsmRelation relation, OsmDocument document, LocalProjection projection)
        {
            var result = new MultipolygonResult();
            var outerChains = new List<long[]>();
            var innerChains = new List<long[]>();

            foreach (OsmMember member in relation.Members)
            {
                if (member.Type != OsmMemberType.Way)
                {
                    continue;
                }

                // OSM mappers sometimes leave the role empty for outer ways.
                bool isOuter = member.Role == "outer" || member.Role == string.Empty;
                bool isInner = member.Role == "inner";
                if (!isOuter && !isInner)
                {
                    continue;
                }

                OsmWay way = document.GetWay(member.Ref);
                if (way == null)
                {
                    result.MissingMemberWays++;
                    continue;
                }

                (isOuter ? outerChains : innerChains).Add(way.NodeIds);
            }

            List<Ring> outers = BuildRings(outerChains, document, projection, result);
            List<Ring> inners = BuildRings(innerChains, document, projection, result);

            for (int i = 0; i < outers.Count; i++)
            {
                outers[i] = outers[i].AsCounterClockwise();
            }

            var holesPerOuter = new List<Ring>[outers.Count];
            for (int i = 0; i < holesPerOuter.Length; i++)
            {
                holesPerOuter[i] = new List<Ring>();
            }

            foreach (Ring inner in inners)
            {
                int owner = FindSmallestContainingOuter(inner, outers);
                if (owner < 0)
                {
                    result.UnassignedHoles++;
                }
                else
                {
                    holesPerOuter[owner].Add(inner.AsClockwise());
                }
            }

            for (int i = 0; i < outers.Count; i++)
            {
                result.Polygons.Add(new AreaPolygon(outers[i], holesPerOuter[i]));
            }

            return result;
        }

        static List<Ring> BuildRings(List<long[]> chains, OsmDocument document, LocalProjection projection, MultipolygonResult result)
        {
            var rings = new List<Ring>();
            RingAssemblyResult assembled = RingAssembler.Assemble(chains);
            result.OpenChains += assembled.OpenChains.Count;

            foreach (long[] closed in assembled.ClosedRings)
            {
                if (WayGeometry.TryGetRing(closed, document, projection, out Ring ring) == RingProblem.None)
                {
                    rings.Add(ring);
                }
                else
                {
                    result.InvalidRings++;
                }
            }

            return rings;
        }

        // The smallest outer ring that contains most of the hole's vertices, or -1.
        static int FindSmallestContainingOuter(Ring hole, List<Ring> outers)
        {
            int best = -1;
            double bestArea = double.MaxValue;

            for (int i = 0; i < outers.Count; i++)
            {
                Ring outer = outers[i];
                if (outer.Area <= hole.Area || outer.Area >= bestArea)
                {
                    continue;
                }

                int inside = 0;
                foreach (Point2d point in hole.Points)
                {
                    if (outer.Contains(point))
                    {
                        inside++;
                    }
                }

                if (inside * 2 >= hole.Count)
                {
                    best = i;
                    bestArea = outer.Area;
                }
            }

            return best;
        }
    }
}
