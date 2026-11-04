using System.Collections.Generic;

// Based on my own earlier project that worked with OpenStreetMap (OSM) data; adapted here to feed the grid and the ECS map blob.
namespace HighProject.MapCore
{
    // An area with an outer outline (counter-clockwise) and holes (clockwise), e.g. a building with a courtyard.
    public sealed class AreaPolygon
    {
        public Ring Outer { get; }
        public IReadOnlyList<Ring> Holes { get; }

        public AreaPolygon(Ring outer, IReadOnlyList<Ring> holes)
        {
            Outer = outer;
            Holes = holes;
        }

        // Outer area minus the holes.
        public double Area
        {
            get
            {
                double area = Outer.Area;
                foreach (Ring hole in Holes)
                {
                    area -= hole.Area;
                }

                return area;
            }
        }
    }
}
