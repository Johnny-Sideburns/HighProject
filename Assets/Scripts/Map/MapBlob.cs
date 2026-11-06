using Unity.Entities;
using Unity.Mathematics;

namespace HighProject.Map
{
    public struct MapBuildingBlob
    {
        public long OsmId;
        public float HeightMeters;
        public float AreaSquareMeters;
        public float2 Centroid;
    }

    public struct MapEntranceBlob
    {
        public long OsmNodeId;
        public float2 Position;
        public int BuildingIndex;
    }

    // The map as the simulation sees it: plain unmanaged data, readable from Burst jobs and identical in every
    // world (it is baked from a SubScene, which the server and every client load).
    // Cell (x, z) covers [Origin + (x, z) * CellSize, Origin + (x + 1, z + 1) * CellSize); index = z * Width + x.
    // Use the extension methods in MapBlobQueries instead of reading the arrays directly.
    public struct MapBlob
    {
        public float CellSize;
        public float2 Origin;
        public int Width;
        public int Height;
        public ulong Checksum;

        // See HighProject.MapCore.CellFlags.
        public BlobArray<byte> Flags;

        // See HighProject.MapCore.SurfaceKind.
        public BlobArray<byte> Surface;

        // Movement cost per cell, 255 = blocked.
        public BlobArray<byte> Cost;

        // Index into Buildings, or -1.
        public BlobArray<int> BuildingId;

        public BlobArray<MapBuildingBlob> Buildings;
        public BlobArray<MapEntranceBlob> Entrances;
    }

    // Attached to one entity in every world that loads the map SubScene.
    public struct MapSingleton : IComponentData
    {
        public BlobAssetReference<MapBlob> Blob;
    }
}
