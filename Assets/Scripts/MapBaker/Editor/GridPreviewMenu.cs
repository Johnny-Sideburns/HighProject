using System.IO;
using HighProject.MapCore;
using UnityEditor;
using UnityEngine;

namespace HighProject.MapBaker
{
    // Quick check without settings: builds the grid for the selected .osm file (or the Norreport extract) in the
    // foreground and writes a picture to MapPreview/. The bake window does the same in the background with settings.
    public static class GridPreviewMenu
    {
        const string DefaultOsmPath = "Assets/MapData/OSM/copenhagen_norreport_1km.osm";

        [MenuItem("Tools/Map/Build Grid And Export Preview")]
        static void BuildAndExport()
        {
            string osmPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(osmPath) || !osmPath.EndsWith(".osm"))
            {
                osmPath = DefaultOsmPath;
            }

            if (!File.Exists(osmPath))
            {
                Debug.LogError($"OSM file not found: {osmPath}");
                return;
            }

            OsmDocument document = OsmReader.ReadFile(osmPath);
            GridBuildResult result = GridBuilder.Build(document, new GridBuildOptions());
            Debug.Log($"{osmPath}\n{result}");

            string imagePath = GridPreviewExporter.WritePng(osmPath, result.Grid);
            Debug.Log($"Grid preview written to {Path.GetFullPath(imagePath)}");
            EditorUtility.OpenWithDefaultApp(imagePath);
        }
    }
}
