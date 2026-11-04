using System.Collections.Generic;

// Based on my own earlier project that worked with OpenStreetMap (OSM) data; adapted here to feed the grid and the ECS map blob.
namespace HighProject.MapCore
{
    public sealed class RingAssemblyResult
    {
        // Closed node-id chains (first id == last id).
        public List<long[]> ClosedRings { get; } = new List<long[]>();

        // Chains that could not be closed.
        public List<long[]> OpenChains { get; } = new List<long[]>();
    }

    // Joins way pieces that share end nodes into closed rings, as needed for multipolygon relations
    // whose outer or inner outline is split over several ways.
    // Greedy and deterministic (input order). Junctions where more than two pieces meet are resolved
    // in input order, which is good enough for ordinary multipolygons.
    public static class RingAssembler
    {
        public static RingAssemblyResult Assemble(IReadOnlyList<long[]> chains)
        {
            var result = new RingAssemblyResult();
            var remaining = new List<long[]>();
            foreach (long[] chain in chains)
            {
                if (chain != null && chain.Length >= 2)
                {
                    remaining.Add(chain);
                }
            }

            while (remaining.Count > 0)
            {
                var current = new List<long>(remaining[0]);
                remaining.RemoveAt(0);

                while (current[0] != current[current.Count - 1])
                {
                    if (!TryExtend(current, remaining))
                    {
                        break;
                    }
                }

                if (current[0] == current[current.Count - 1] && current.Count >= 4)
                {
                    result.ClosedRings.Add(current.ToArray());
                }
                else
                {
                    result.OpenChains.Add(current.ToArray());
                }
            }

            return result;
        }

        // Appends a piece at the end of the chain, or else prepends one at its start. Returns false if nothing fits.
        static bool TryExtend(List<long> current, List<long[]> remaining)
        {
            long first = current[0];
            long last = current[current.Count - 1];

            for (int i = 0; i < remaining.Count; i++)
            {
                long[] piece = remaining[i];
                if (piece[0] == last)
                {
                    for (int k = 1; k < piece.Length; k++)
                    {
                        current.Add(piece[k]);
                    }
                }
                else if (piece[piece.Length - 1] == last)
                {
                    for (int k = piece.Length - 2; k >= 0; k--)
                    {
                        current.Add(piece[k]);
                    }
                }
                else
                {
                    continue;
                }

                remaining.RemoveAt(i);
                return true;
            }

            for (int i = 0; i < remaining.Count; i++)
            {
                long[] piece = remaining[i];
                var prefix = new List<long>(piece.Length);
                if (piece[piece.Length - 1] == first)
                {
                    for (int k = 0; k < piece.Length - 1; k++)
                    {
                        prefix.Add(piece[k]);
                    }
                }
                else if (piece[0] == first)
                {
                    for (int k = piece.Length - 1; k >= 1; k--)
                    {
                        prefix.Add(piece[k]);
                    }
                }
                else
                {
                    continue;
                }

                current.InsertRange(0, prefix);
                remaining.RemoveAt(i);
                return true;
            }

            return false;
        }
    }
}
