using System.Diagnostics;
using System.IO;
using HighProject.MapCore;
using UnityEditor;
using Debug = UnityEngine.Debug;

// Editor tooling like this follows what I built in earlier projects; adapted here for the map baker.
namespace HighProject.MapBaker
{
    // Milestones 1a-1b: prints the statistics, building footprints and entrances of an OSM file to the Console.
    // Select a .osm file in the Project window first; without a selection the Norreport extract is used.
    public static class OsmReportMenu
    {
        const string DefaultOsmPath = "Assets/MapData/OSM/copenhagen_norreport_1km.osm";

        [MenuItem("Tools/Map/Print OSM Statistics")]
        static void PrintStatistics()
        {
            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".osm"))
            {
                assetPath = DefaultOsmPath;
            }

            if (!File.Exists(assetPath))
            {
                Debug.LogError($"OSM file not found: {assetPath}");
                return;
            }

            var timer = Stopwatch.StartNew();
            OsmDocument document = OsmReader.ReadFile(assetPath);
            long readMilliseconds = timer.ElapsedMilliseconds;
            MapStats stats = MapStatsBuilder.Build(document, new ClassificationOptions());

            Debug.Log($"{assetPath}\n{stats}\nRead in {readMilliseconds} ms, statistics in {timer.ElapsedMilliseconds - readMilliseconds} ms.");
        }
    }
}
