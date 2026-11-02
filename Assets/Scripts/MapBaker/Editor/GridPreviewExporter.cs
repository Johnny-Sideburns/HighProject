using System.IO;
using HighProject.MapCore;
using UnityEngine;

namespace HighProject.MapBaker
{
    // Turns a grid into a Unity texture or a PNG file.
    public static class GridPreviewExporter
    {
        const string OutputFolder = "MapPreview";

        // The caller owns the texture and must destroy it. Pixels are not smoothed so that single cells stay visible.
        public static Texture2D CreateTexture(MapGrid grid)
        {
            // Unity textures start at the bottom row, so the southern row goes first.
            var texture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.LoadRawTextureData(GridPreviewPalette.RenderRgb(grid, false));
            texture.Apply();
            return texture;
        }

        // Writes MapPreview/<osm name>_grid.png in the project folder (outside Assets, so Unity does not import it).
        public static string WritePng(string osmPath, MapGrid grid)
        {
            Texture2D texture = CreateTexture(grid);
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            Directory.CreateDirectory(OutputFolder);
            string path = Path.Combine(OutputFolder, Path.GetFileNameWithoutExtension(osmPath) + "_grid.png");
            File.WriteAllBytes(path, png);
            return path;
        }
    }
}
