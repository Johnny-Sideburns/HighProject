using HighProject.Map;
using UnityEditor;
using UnityEngine;

// Editor tooling like this follows what I built in earlier projects; adapted here for the map baker.
namespace HighProject.MapBaker
{
    // Inspector for a baked map: a short summary and no data. The grid layers are hidden on purpose (see MapAsset):
    // drawing millions of array elements is what froze the editor.
    [CustomEditor(typeof(MapAsset))]
    public sealed class MapAssetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var asset = (MapAsset)target;

            if (!asset.IsConsistent())
            {
                EditorGUILayout.HelpBox(
                    $"This map asset is empty or was written by another version (format {asset.FormatVersion}, expected {MapAsset.CurrentFormatVersion}). " +
                    "Bake the map again in the Map Bake window and press Save Map Asset.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Source", asset.SourcePath);
            EditorGUILayout.LabelField("Checksum", asset.Checksum);
            EditorGUILayout.LabelField("Centre (lat, lon)", $"{asset.CenterLatitude:F5}, {asset.CenterLongitude:F5}");
            EditorGUILayout.LabelField("Grid", $"{asset.Width} x {asset.Height} cells of {asset.CellSize} m ({asset.Width * asset.CellSize:F0} x {asset.Height * asset.CellSize:F0} m)");
            EditorGUILayout.LabelField("Origin (lower left)", $"{asset.OriginX:F1}, {asset.OriginZ:F1} m");
            EditorGUILayout.LabelField("Buildings", asset.Buildings != null ? asset.Buildings.Length.ToString() : "0");
            EditorGUILayout.LabelField("Entrances", asset.Entrances != null ? asset.Entrances.Length.ToString() : "0");
            EditorGUILayout.LabelField("Building meshes", asset.BuildingMeshes != null ? asset.BuildingMeshes.Length.ToString() : "0");
            EditorGUILayout.LabelField("Ground texture", asset.GroundTexture != null ? $"{asset.GroundTexture.width} x {asset.GroundTexture.height}" : "none");
            EditorGUILayout.LabelField("Layer data", $"{asset.PackedLayerBytes / 1024} KB packed ({asset.UnpackedLayerBytes / (1024 * 1024)} MB unpacked)");
        }

        // The default preview would try to draw the asset's data.
        public override bool HasPreviewGUI()
        {
            return false;
        }
    }
}
