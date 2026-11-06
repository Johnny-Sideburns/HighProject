using System;
using System.Globalization;
using System.IO;
using HighProject.MapCore;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HighProject.Map
{
    // Turns a MapAsset into the MapBlob the simulation reads.
    public static class MapBlobBuilder
    {
        // The caller owns the returned blob and must dispose it (or hand it to a baker, which does).
        public static BlobAssetReference<MapBlob> Build(MapAsset asset, Allocator allocator)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            if (!asset.IsConsistent())
            {
                throw new InvalidOperationException(
                    $"The map asset '{asset.name}' is damaged or was written by another version (format {asset.FormatVersion}, expected {MapAsset.CurrentFormatVersion}). Bake it again.");
            }

            GridLayers layers;
            try
            {
                layers = asset.Decode();
            }
            catch (InvalidDataException exception)
            {
                throw new InvalidOperationException(
                    $"The map asset '{asset.name}' has damaged grid data ({exception.Message}). Bake it again.", exception);
            }

            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref MapBlob root = ref builder.ConstructRoot<MapBlob>();
                root.CellSize = asset.CellSize;
                root.Origin = new float2((float)asset.OriginX, (float)asset.OriginZ);
                root.Width = asset.Width;
                root.Height = asset.Height;
                root.Checksum = ParseChecksum(asset.Checksum);

                int cells = asset.CellCount;
                CopyBytes(builder.Allocate(ref root.Flags, cells), layers.Flags);
                CopyBytes(builder.Allocate(ref root.Surface, cells), layers.Surface);
                CopyBytes(builder.Allocate(ref root.Cost, cells), layers.Cost);

                BlobBuilderArray<int> buildingIds = builder.Allocate(ref root.BuildingId, cells);
                byte[] idBytes = layers.BuildingIdBytes;
                for (int i = 0; i < cells; i++)
                {
                    buildingIds[i] = BitConverter.ToInt32(idBytes, i * 4);
                }

                MapBuildingData[] buildings = asset.Buildings ?? new MapBuildingData[0];
                BlobBuilderArray<MapBuildingBlob> buildingArray = builder.Allocate(ref root.Buildings, buildings.Length);
                for (int i = 0; i < buildings.Length; i++)
                {
                    buildingArray[i] = new MapBuildingBlob
                    {
                        OsmId = buildings[i].osmId,
                        HeightMeters = buildings[i].heightMeters,
                        AreaSquareMeters = buildings[i].areaSquareMeters,
                        Centroid = new float2(buildings[i].centroid.x, buildings[i].centroid.y)
                    };
                }

                MapEntranceData[] entrances = asset.Entrances ?? new MapEntranceData[0];
                BlobBuilderArray<MapEntranceBlob> entranceArray = builder.Allocate(ref root.Entrances, entrances.Length);
                for (int i = 0; i < entrances.Length; i++)
                {
                    entranceArray[i] = new MapEntranceBlob
                    {
                        OsmNodeId = entrances[i].osmNodeId,
                        Position = new float2(entrances[i].position.x, entrances[i].position.y),
                        BuildingIndex = entrances[i].buildingIndex
                    };
                }

                return builder.CreateBlobAssetReference<MapBlob>(allocator);
            }
            finally
            {
                builder.Dispose();
            }
        }

        static void CopyBytes(BlobBuilderArray<byte> target, byte[] source)
        {
            for (int i = 0; i < source.Length; i++)
            {
                target[i] = source[i];
            }
        }

        static ulong ParseChecksum(string hex)
        {
            return !string.IsNullOrEmpty(hex) && ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value)
                ? value
                : 0UL;
        }
    }
}
