using System;
using System.IO;
using HighProject.MapCore;
using UnityEngine;

namespace HighProject.Map
{
    [Serializable]
    public struct MapBuildingData
    {
        public long osmId;
        public bool isRelation;
        public float heightMeters;

        // 0 = default, 1 = from building:levels, 2 = from the height tag (see HighProject.MapCore.HeightSource).
        public byte heightSource;
        public float areaSquareMeters;
        public Vector2 centroid;
    }

    [Serializable]
    public struct MapEntranceData
    {
        public long osmNodeId;
        public Vector2 position;

        // Index into the buildings array.
        public int buildingIndex;
    }

    // The baked map: grid layers, building list, entrances, render meshes and ground texture. Written by the Map Bake
    // window, read by the SubScene baker, which turns it into a MapBlob that every world can read.
    //
    // The per-cell layers are stored as ONE compressed byte array (a few hundred KB for a million cells), never as
    // separate arrays of millions of bytes: Unity's Inspector, previews and serializer cost memory per array element,
    // and a 7 million element array made the editor allocate gigabytes and freeze. Use MapAssetEditor (the custom
    // Inspector) to look at an asset; the fields are hidden from the default Inspector on purpose.
    public sealed class MapAsset : ScriptableObject
    {
        // Bump when the layout of the stored data changes. Version 2 = compressed layers.
        public const int CurrentFormatVersion = 2;

        [SerializeField, HideInInspector] int formatVersion;
        [SerializeField, HideInInspector] string checksum;
        [SerializeField, HideInInspector] string sourcePath;
        [SerializeField, HideInInspector] double centerLatitude;
        [SerializeField, HideInInspector] double centerLongitude;
        [SerializeField, HideInInspector] float cellSize;
        [SerializeField, HideInInspector] double originX;
        [SerializeField, HideInInspector] double originZ;
        [SerializeField, HideInInspector] int width;
        [SerializeField, HideInInspector] int height;
        [SerializeField, HideInInspector] byte[] packedLayers;
        [SerializeField, HideInInspector] MapBuildingData[] buildings;
        [SerializeField, HideInInspector] MapEntranceData[] entrances;
        [SerializeField, HideInInspector] Mesh[] buildingMeshes;
        [SerializeField, HideInInspector] Texture2D groundTexture;

        public int FormatVersion => formatVersion;

        // Identifies the exact baked content (see MapGrid.ComputeChecksum).
        public string Checksum => checksum;

        public string SourcePath => sourcePath;
        public double CenterLatitude => centerLatitude;
        public double CenterLongitude => centerLongitude;
        public float CellSize => cellSize;

        // Local position (metres) of the lower left corner of cell (0, 0).
        public double OriginX => originX;
        public double OriginZ => originZ;

        public int Width => width;
        public int Height => height;
        public int CellCount => width * height;

        public MapBuildingData[] Buildings => buildings;
        public MapEntranceData[] Entrances => entrances;
        public Mesh[] BuildingMeshes => buildingMeshes;

        // One pixel per grid cell, colours by ground type; covers the whole grid.
        public Texture2D GroundTexture => groundTexture;

        // Size of the stored (compressed) layers in bytes.
        public int PackedLayerBytes => packedLayers != null ? packedLayers.Length : 0;

        // Size the layers have after unpacking: flags, surface, cost (one byte each) and a four byte building id per cell.
        public long UnpackedLayerBytes => (long)CellCount * 7;

        // Each of these unpacks all layers again. To read several, call Decode() once and use the result.
        public byte[] Flags => Decode().Flags;
        public byte[] Surface => Decode().Surface;
        public byte[] Cost => Decode().Cost;

        // Four bytes per cell (little endian int), -1 = no building.
        public byte[] BuildingIdBytes => Decode().BuildingIdBytes;

        // Unpacks the per-cell layers (a few milliseconds for a million cells).
        // Throws InvalidDataException if the stored data is damaged.
        public GridLayers Decode()
        {
            if (packedLayers == null || CellCount <= 0)
            {
                throw new InvalidDataException("The map asset has no grid layers.");
            }

            return GridLayerCodec.Unpack(packedLayers, CellCount);
        }

        // Cheap check of the header and that layers are present; a damaged payload is only found by Decode().
        public bool IsConsistent()
        {
            return formatVersion == CurrentFormatVersion
                   && width > 0 && height > 0
                   && packedLayers != null && packedLayers.Length > 0;
        }

        // Fills the asset. Used by the editor after a bake; packs the layers (about 50 ms for a million cells).
        public void Assign(MapAssetContent content)
        {
            var layers = new GridLayers(content.Flags, content.Surface, content.Cost, content.BuildingIdBytes);
            packedLayers = GridLayerCodec.Pack(layers, content.Width * content.Height);

            formatVersion = CurrentFormatVersion;
            checksum = content.Checksum;
            sourcePath = content.SourcePath;
            centerLatitude = content.CenterLatitude;
            centerLongitude = content.CenterLongitude;
            cellSize = content.CellSize;
            originX = content.OriginX;
            originZ = content.OriginZ;
            width = content.Width;
            height = content.Height;
            buildings = content.Buildings;
            entrances = content.Entrances;
            buildingMeshes = content.BuildingMeshes;
            groundTexture = content.GroundTexture;
        }
    }

    // Plain bundle of everything a MapAsset stores, so the editor can fill an asset in one call.
    public sealed class MapAssetContent
    {
        public string Checksum { get; set; }
        public string SourcePath { get; set; }
        public double CenterLatitude { get; set; }
        public double CenterLongitude { get; set; }
        public float CellSize { get; set; }
        public double OriginX { get; set; }
        public double OriginZ { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Flags { get; set; }
        public byte[] Surface { get; set; }
        public byte[] Cost { get; set; }
        public byte[] BuildingIdBytes { get; set; }
        public MapBuildingData[] Buildings { get; set; }
        public MapEntranceData[] Entrances { get; set; }
        public Mesh[] BuildingMeshes { get; set; }
        public Texture2D GroundTexture { get; set; }
    }
}
