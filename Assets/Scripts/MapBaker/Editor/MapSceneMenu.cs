using HighProject.Map;
using UnityEditor;
using UnityEngine;

namespace HighProject.MapBaker
{
    // Puts the baked map into the open scene with one click.
    public static class MapSceneMenu
    {
        const string DefaultOsmPath = "Assets/MapData/OSM/copenhagen_norreport_1km.osm";

        // Uses the selected MapAsset, or the one saved for the Norreport extract.
        [MenuItem("Tools/Map/Add Map To Scene")]
        static void AddMapToScene()
        {
            var asset = Selection.activeObject as MapAsset;
            if (asset == null)
            {
                asset = AssetDatabase.LoadAssetAtPath<MapAsset>(MapAssetWriter.AssetPathFor(DefaultOsmPath));
            }

            if (asset == null)
            {
                EditorUtility.DisplayDialog("Add Map To Scene",
                    "No map asset found. Bake the map in the Map Bake window and press Save Map Asset first, or select a MapAsset in the Project window.", "OK");
                return;
            }

            var mapObject = new GameObject("Map");
            Undo.RegisterCreatedObjectUndo(mapObject, "Add map to scene");
            MapRenderer renderer = Undo.AddComponent<MapRenderer>(mapObject);

            var serialized = new SerializedObject(renderer);
            serialized.FindProperty("mapAsset").objectReferenceValue = asset;
            serialized.ApplyModifiedProperties();

            // The component was enabled before the asset was assigned, so it has built nothing yet.
            renderer.Rebuild();

            Selection.activeGameObject = mapObject;
            Debug.Log($"Added '{asset.name}' to the scene. The map is centred on the origin and spans {asset.Width * asset.CellSize:F0} x {asset.Height * asset.CellSize:F0} m.", mapObject);
        }
    }
}
