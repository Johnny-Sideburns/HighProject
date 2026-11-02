using System;
using System.IO;
using HighProject.Map;
using HighProject.MapCore;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HighProject.MapBaker
{
    // Saves a bake result as a MapAsset in Assets/MapData/Baked/ (not committed to git). Saving again for the same
    // .osm file updates the existing asset in place, so references to it (the SubScene's MapAuthoring) stay valid.
    public static class MapAssetWriter
    {
        public const string OutputFolder = "Assets/MapData/Baked";

        public static string AssetPathFor(string osmPath)
        {
            return $"{OutputFolder}/{Path.GetFileNameWithoutExtension(osmPath)}_Map.asset";
        }

        // Plain-data part of the conversion (everything except the meshes); public so it can be tested in the editor.
        public static MapAssetContent CreateContent(GridBuildResult result, string osmPath)
        {
            MapGrid grid = result.Grid;

            var buildings = new MapBuildingData[result.Buildings.Footprints.Count];
            for (int i = 0; i < buildings.Length; i++)
            {
                BuildingFootprint footprint = result.Buildings.Footprints[i];
                Ring outer = footprint.Polygon.Outer;
                buildings[i] = new MapBuildingData
                {
                    osmId = footprint.OsmId,
                    isRelation = footprint.IsRelation,
                    heightMeters = (float)footprint.HeightMeters,
                    heightSource = (byte)footprint.HeightSource,
                    areaSquareMeters = (float)footprint.Polygon.Area,
                    centroid = new Vector2((float)((outer.MinX + outer.MaxX) * 0.5), (float)((outer.MinZ + outer.MaxZ) * 0.5))
                };
            }

            var entrances = new MapEntranceData[result.Entrances.Entrances.Count];
            for (int i = 0; i < entrances.Length; i++)
            {
                BuildingEntrance entrance = result.Entrances.Entrances[i];
                entrances[i] = new MapEntranceData
                {
                    osmNodeId = entrance.NodeId,
                    position = new Vector2((float)entrance.Position.X, (float)entrance.Position.Z),
                    buildingIndex = entrance.FootprintIndex
                };
            }

            var idBytes = new byte[grid.BuildingId.Length * sizeof(int)];
            Buffer.BlockCopy(grid.BuildingId, 0, idBytes, 0, idBytes.Length);

            return new MapAssetContent
            {
                Checksum = result.Checksum,
                SourcePath = osmPath,
                CenterLatitude = result.Bounds.CenterLat,
                CenterLongitude = result.Bounds.CenterLon,
                CellSize = (float)grid.CellSize,
                OriginX = grid.OriginX,
                OriginZ = grid.OriginZ,
                Width = grid.Width,
                Height = grid.Height,
                Flags = (byte[])grid.Flags.Clone(),
                Surface = (byte[])grid.Surface.Clone(),
                Cost = (byte[])grid.Cost.Clone(),
                BuildingIdBytes = idBytes,
                Buildings = buildings,
                Entrances = entrances,
                BuildingMeshes = new Mesh[0],
                GroundTexture = null
            };
        }

        // One pixel per cell, southern row first (the texture's bottom row), see GridPreviewPalette.GroundColorOf.
        public static Texture2D CreateGroundTexture(MapGrid grid)
        {
            var texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGB24, true)
            {
                name = "Ground",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 4
            };
            texture.SetPixelData(GridPreviewPalette.RenderGroundRgb(grid), 0);
            texture.Apply(true);
            return texture;
        }

        public static Mesh CreateMesh(BuildingMeshChunk chunk)
        {
            var vertices = new Vector3[chunk.VertexCount];
            var normals = new Vector3[chunk.VertexCount];
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = new Vector3(chunk.Positions[i * 3], chunk.Positions[i * 3 + 1], chunk.Positions[i * 3 + 2]);
                normals[i] = new Vector3(chunk.Normals[i * 3], chunk.Normals[i * 3 + 1], chunk.Normals[i * 3 + 2]);
            }

            var mesh = new Mesh
            {
                name = $"Buildings_{chunk.TileX}_{chunk.TileZ}",
                indexFormat = IndexFormat.UInt32
            };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(chunk.RoofIndices, 0);
            mesh.SetTriangles(chunk.WallIndices, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        // Creates or updates the asset and returns it.
        public static MapAsset Write(GridBuildResult result, string osmPath, double meshTileSizeMeters)
        {
            MapAssetContent content = CreateContent(result, osmPath);
            BuildingMeshResult meshes = BuildingMeshBuilder.Build(result.Buildings.Footprints, meshTileSizeMeters);
            if (meshes.RoofFailures.Count > 0)
            {
                Debug.LogWarning($"{meshes.RoofFailures.Count} building roofs could not be triangulated completely (footprint indices: {string.Join(", ", meshes.RoofFailures)}).");
            }

            EnsureFolder();
            string path = AssetPathFor(osmPath);
            MapAsset asset = AssetDatabase.LoadAssetAtPath<MapAsset>(path);
            bool isNew = asset == null;
            if (isNew)
            {
                asset = ScriptableObject.CreateInstance<MapAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            else
            {
                RemoveOldSubAssets(path);
            }

            var meshAssets = new Mesh[meshes.Chunks.Count];
            for (int i = 0; i < meshAssets.Length; i++)
            {
                meshAssets[i] = CreateMesh(meshes.Chunks[i]);
                AssetDatabase.AddObjectToAsset(meshAssets[i], asset);
            }

            Texture2D ground = CreateGroundTexture(result.Grid);
            AssetDatabase.AddObjectToAsset(ground, asset);

            content.BuildingMeshes = meshAssets;
            content.GroundTexture = ground;
            asset.Assign(content);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/MapData"))
            {
                AssetDatabase.CreateFolder("Assets", "MapData");
            }

            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder("Assets/MapData", "Baked");
            }
        }

        // The meshes and the ground texture are rebuilt on every save; drop the previous ones.
        static void RemoveOldSubAssets(string assetPath)
        {
            foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
            {
                if (sub is Mesh || sub is Texture2D)
                {
                    UnityEngine.Object.DestroyImmediate(sub, true);
                }
            }
        }
    }
}
