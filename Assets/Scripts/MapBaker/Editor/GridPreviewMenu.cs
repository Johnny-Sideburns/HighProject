using System.IO;
using HighProject.MapCore;
using UnityEditor;
using UnityEngine;

namespace HighProject.MapBaker
{
    // Builds the walkability grid for an OSM file and writes a picture of it to MapPreview/ in the project folder
    // (outside Assets, so Unity does not import it). Select a .osm file in the Project window first; without a
    // selection the Norreport extract is used. This runs in the foreground (about half a second for 1 km at 1 m);
    // the background version with progress and cancel comes with the bake window.
    public static class GridPreviewMenu
    {
        const string DefaultOsmPath = "Assets/MapData/OSM/copenhagen_norreport_1km.osm";
        const string OutputFolder = "MapPreview";

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

            string imagePath = WritePreview(osmPath, result.Grid);
            Debug.Log($"Grid preview written to {Path.GetFullPath(imagePath)}");
            EditorUtility.OpenWithDefaultApp(imagePath);
        }

        static string WritePreview(string osmPath, MapGrid grid)
        {
            // Unity textures start at the bottom row, so the southern row goes first.
            var texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGB24, false);
            texture.LoadRawTextureData(GridPreviewPalette.RenderRgb(grid, false));
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            Directory.CreateDirectory(OutputFolder);
            string path = Path.Combine(OutputFolder, Path.GetFileNameWithoutExtension(osmPath) + "_grid.png");
            File.WriteAllBytes(path, png);
            return path;
        }
    }
}
