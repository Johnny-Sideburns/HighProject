using System;
using System.Collections.Generic;
using System.Linq;

namespace HighProject.MapCore
{
    // Mesh data for all buildings whose centre lies in one square tile. Plain arrays, so it is independent of Unity;
    // the editor turns it into a Mesh. Coordinates follow the Unity scene: x east, y up, z north. Triangles wind
    // clockwise as seen from outside, which is what Unity treats as the front side.
    public sealed class BuildingMeshChunk
    {
        public int TileX { get; }
        public int TileZ { get; }

        // x, y, z for every vertex (three floats each).
        public float[] Positions { get; }

        // One unit normal per vertex, same layout as Positions.
        public float[] Normals { get; }

        public int[] RoofIndices { get; }
        public int[] WallIndices { get; }
        public int BuildingCount { get; }

        public int VertexCount => Positions.Length / 3;

        public BuildingMeshChunk(int tileX, int tileZ, float[] positions, float[] normals, int[] roofIndices, int[] wallIndices, int buildingCount)
        {
            TileX = tileX;
            TileZ = tileZ;
            Positions = positions;
            Normals = normals;
            RoofIndices = roofIndices;
            WallIndices = wallIndices;
            BuildingCount = buildingCount;
        }
    }

    public sealed class BuildingMeshResult
    {
        // Chunks ordered by tile (x, then z), so the result is the same on every machine.
        public List<BuildingMeshChunk> Chunks { get; } = new List<BuildingMeshChunk>();

        // Footprints whose roof could not be fully triangulated (they still get walls and whatever roof was found).
        public List<int> RoofFailures { get; } = new List<int>();

        public int TotalVertices => Chunks.Sum(chunk => chunk.VertexCount);
        public int TotalTriangles => Chunks.Sum(chunk => (chunk.RoofIndices.Length + chunk.WallIndices.Length) / 3);
    }

    public static class BuildingMeshBuilder
    {
        public static BuildingMeshResult Build(IReadOnlyList<BuildingFootprint> footprints, double tileSizeMeters)
        {
            if (tileSizeMeters <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(tileSizeMeters), "The tile size must be positive.");
            }

            var result = new BuildingMeshResult();

            var byTile = new SortedDictionary<(int X, int Z), List<BuildingFootprint>>(new TileComparer());
            foreach (BuildingFootprint footprint in footprints)
            {
                Ring outer = footprint.Polygon.Outer;
                int tileX = (int)Math.Floor((outer.MinX + outer.MaxX) * 0.5 / tileSizeMeters);
                int tileZ = (int)Math.Floor((outer.MinZ + outer.MaxZ) * 0.5 / tileSizeMeters);

                if (!byTile.TryGetValue((tileX, tileZ), out List<BuildingFootprint> list))
                {
                    list = new List<BuildingFootprint>();
                    byTile[(tileX, tileZ)] = list;
                }

                list.Add(footprint);
            }

            foreach (KeyValuePair<(int X, int Z), List<BuildingFootprint>> tile in byTile)
            {
                result.Chunks.Add(BuildChunk(tile.Key.X, tile.Key.Z, tile.Value, result.RoofFailures));
            }

            return result;
        }

        static BuildingMeshChunk BuildChunk(int tileX, int tileZ, List<BuildingFootprint> footprints, List<int> roofFailures)
        {
            var positions = new List<float>();
            var normals = new List<float>();
            var roofIndices = new List<int>();
            var wallIndices = new List<int>();

            foreach (BuildingFootprint footprint in footprints.OrderBy(f => f.Index))
            {
                float height = (float)footprint.HeightMeters;
                AddRoof(footprint, height, positions, normals, roofIndices, roofFailures);
                AddWalls(footprint.Polygon.Outer, height, positions, normals, wallIndices);
                foreach (Ring hole in footprint.Polygon.Holes)
                {
                    AddWalls(hole, height, positions, normals, wallIndices);
                }
            }

            return new BuildingMeshChunk(tileX, tileZ, positions.ToArray(), normals.ToArray(), roofIndices.ToArray(), wallIndices.ToArray(), footprints.Count);
        }

        static void AddRoof(BuildingFootprint footprint, float height, List<float> positions, List<float> normals, List<int> indices, List<int> failures)
        {
            if (!Triangulator.Triangulate(footprint.Polygon, out Point2d[] vertices, out int[] triangles))
            {
                failures.Add(footprint.Index);
            }

            int baseIndex = positions.Count / 3;
            foreach (Point2d vertex in vertices)
            {
                positions.Add((float)vertex.X);
                positions.Add(height);
                positions.Add((float)vertex.Z);
                normals.Add(0f);
                normals.Add(1f);
                normals.Add(0f);
            }

            // The triangulator returns counter-clockwise triangles in (x, z); seen from above that is the back side
            // for Unity, so the order of the last two corners is swapped.
            for (int i = 0; i < triangles.Length; i += 3)
            {
                indices.Add(baseIndex + triangles[i]);
                indices.Add(baseIndex + triangles[i + 2]);
                indices.Add(baseIndex + triangles[i + 1]);
            }
        }

        // One flat quad per edge. The outer ring runs counter-clockwise and holes clockwise, so the outward side
        // (away from the building material) is always to the right of the edge direction.
        static void AddWalls(Ring ring, float height, List<float> positions, List<float> normals, List<int> indices)
        {
            Point2d[] points = ring.Points;
            for (int i = 0; i < points.Length; i++)
            {
                Point2d a = points[i];
                Point2d b = points[(i + 1) % points.Length];

                double dx = b.X - a.X;
                double dz = b.Z - a.Z;
                double length = Math.Sqrt(dx * dx + dz * dz);
                if (length < 1e-9)
                {
                    continue;
                }

                float normalX = (float)(dz / length);
                float normalZ = (float)(-dx / length);

                int baseIndex = positions.Count / 3;
                AddVertex(positions, normals, a.X, 0f, a.Z, normalX, normalZ);
                AddVertex(positions, normals, b.X, 0f, b.Z, normalX, normalZ);
                AddVertex(positions, normals, b.X, height, b.Z, normalX, normalZ);
                AddVertex(positions, normals, a.X, height, a.Z, normalX, normalZ);

                // (a bottom, a top, b top) and (a bottom, b top, b bottom).
                indices.Add(baseIndex);
                indices.Add(baseIndex + 3);
                indices.Add(baseIndex + 2);
                indices.Add(baseIndex);
                indices.Add(baseIndex + 2);
                indices.Add(baseIndex + 1);
            }
        }

        static void AddVertex(List<float> positions, List<float> normals, double x, float y, double z, float normalX, float normalZ)
        {
            positions.Add((float)x);
            positions.Add(y);
            positions.Add((float)z);
            normals.Add(normalX);
            normals.Add(0f);
            normals.Add(normalZ);
        }

        sealed class TileComparer : IComparer<(int X, int Z)>
        {
            public int Compare((int X, int Z) left, (int X, int Z) right)
            {
                int byX = left.X.CompareTo(right.X);
                return byX != 0 ? byX : left.Z.CompareTo(right.Z);
            }
        }
    }
}
