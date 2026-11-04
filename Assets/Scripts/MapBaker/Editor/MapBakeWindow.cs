using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HighProject.Map;
using HighProject.MapCore;
using UnityEditor;
using UnityEngine;

// Editor tooling like this follows what I built in earlier projects; adapted here for the map baker.
namespace HighProject.MapBaker
{
    // Window to bake an OSM file into the walkability grid in the background (the editor stays usable),
    // look at the result and inspect single cells. Open it with Tools > Map > Bake Window.
    public sealed class MapBakeWindow : EditorWindow
    {
        const float LegendSwatchSize = 12f;

        [SerializeField] MapBakeSettings settings;

        readonly OsmDocumentCache documentCache = new OsmDocumentCache();

        GridBakeJob job;
        int progressId = -1;
        Task<DeterminismResult> determinismCheck;

        GridBuildResult result;
        string resultOsmPath;
        Texture2D preview;
        string report = string.Empty;
        string status = "Assign a Map Bake Settings asset with an .osm file, then press Bake.";
        string hoverText = string.Empty;
        Vector2 reportScroll;
        bool showReport;

        [MenuItem("Tools/Map/Bake Window")]
        static void Open()
        {
            GetWindow<MapBakeWindow>("Map Bake");
        }

        void OnEnable()
        {
            wantsMouseMove = true;
            EditorApplication.update += Poll;
            AssemblyReloadEvents.beforeAssemblyReload += StopJob;
        }

        void OnDisable()
        {
            EditorApplication.update -= Poll;
            AssemblyReloadEvents.beforeAssemblyReload -= StopJob;
            StopJob();
            DestroyPreview();
        }

        void OnGUI()
        {
            DrawSettingsRow();
            DrawBakeRow();
            DrawProgress();

            EditorGUILayout.HelpBox(status, MessageType.None);

            if (result != null)
            {
                foreach (ValidationMessage message in result.Validation)
                {
                    EditorGUILayout.HelpBox(message.Text, ToMessageType(message.Severity));
                }

                showReport = EditorGUILayout.Foldout(showReport, "Report", true);
                if (showReport)
                {
                    reportScroll = EditorGUILayout.BeginScrollView(reportScroll, GUILayout.Height(150));
                    EditorGUILayout.SelectableLabel(report, EditorStyles.textArea, GUILayout.ExpandHeight(true));
                    EditorGUILayout.EndScrollView();
                }
            }

            DrawPreview();
            DrawLegend();
        }

        static MessageType ToMessageType(ValidationSeverity severity)
        {
            switch (severity)
            {
                case ValidationSeverity.Error: return MessageType.Error;
                case ValidationSeverity.Warning: return MessageType.Warning;
                default: return MessageType.Info;
            }
        }

        void DrawSettingsRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(job != null))
                {
                    settings = (MapBakeSettings)EditorGUILayout.ObjectField("Settings", settings, typeof(MapBakeSettings), false);
                }

                if (GUILayout.Button("Select", GUILayout.Width(60)) && settings != null)
                {
                    Selection.activeObject = settings;
                }
            }
        }

        void DrawBakeRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(job != null || settings == null))
                {
                    if (GUILayout.Button("Bake"))
                    {
                        StartBake();
                    }
                }

                using (new EditorGUI.DisabledScope(job == null))
                {
                    if (GUILayout.Button("Cancel"))
                    {
                        job.Cancel();
                    }
                }

                using (new EditorGUI.DisabledScope(job != null || settings == null || determinismCheck != null))
                {
                    if (GUILayout.Button("Check Determinism"))
                    {
                        StartDeterminismCheck();
                    }
                }

                using (new EditorGUI.DisabledScope(job != null || result == null))
                {
                    if (GUILayout.Button("Save Map Asset"))
                    {
                        SaveMapAsset();
                    }

                    if (GUILayout.Button("Export PNG"))
                    {
                        string path = GridPreviewExporter.WritePng(resultOsmPath, result.Grid);
                        status = $"Preview written to {Path.GetFullPath(path)}";
                        EditorUtility.OpenWithDefaultApp(path);
                    }
                }
            }
        }

        void DrawProgress()
        {
            if (job == null)
            {
                return;
            }

            Rect rect = EditorGUILayout.GetControlRect();
            EditorGUI.ProgressBar(rect, job.Progress, $"{job.Phase} ({job.ElapsedMilliseconds} ms)");
        }

        void DrawPreview()
        {
            Rect area = GUILayoutUtility.GetRect(0f, 0f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (preview == null || result == null)
            {
                return;
            }

            float scale = Mathf.Min(area.width / preview.width, area.height / preview.height);
            var size = new Vector2(preview.width * scale, preview.height * scale);
            var drawRect = new Rect(area.x + (area.width - size.x) * 0.5f, area.y + (area.height - size.y) * 0.5f, size.x, size.y);
            GUI.DrawTexture(drawRect, preview, ScaleMode.StretchToFill, false);

            Event current = Event.current;
            if ((current.type == EventType.MouseMove || current.type == EventType.MouseDown) && drawRect.Contains(current.mousePosition))
            {
                MapGrid grid = result.Grid;
                float u = (current.mousePosition.x - drawRect.x) / drawRect.width;
                float v = 1f - (current.mousePosition.y - drawRect.y) / drawRect.height;
                double worldX = grid.OriginX + u * grid.Width * grid.CellSize;
                double worldZ = grid.OriginZ + v * grid.Height * grid.CellSize;
                hoverText = GridInspector.DescribePosition(grid, result.Connectivity, result.Buildings.Footprints, worldX, worldZ);
                Repaint();
            }

            // A click reports the map position of the spot, to type into an override point.
            if (current.type == EventType.MouseDown && current.button == 0 && drawRect.Contains(current.mousePosition))
            {
                MapGrid grid = result.Grid;
                double worldX = grid.OriginX + (current.mousePosition.x - drawRect.x) / drawRect.width * grid.Width * grid.CellSize;
                double worldZ = grid.OriginZ + (1f - (current.mousePosition.y - drawRect.y) / drawRect.height) * grid.Height * grid.CellSize;
                EditorGUIUtility.systemCopyBuffer = $"{worldX:F1}, {worldZ:F1}";
                status = $"Position x = {worldX:F1} (east), y = {worldZ:F1} (north) metres from the map centre; copied to the clipboard. Use it as a point of an override in the settings.";
            }

            EditorGUILayout.LabelField(hoverText, EditorStyles.miniLabel);
        }

        void DrawLegend()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach ((string name, byte r, byte g, byte b) in GridPreviewPalette.Legend)
                {
                    Rect swatch = GUILayoutUtility.GetRect(LegendSwatchSize, LegendSwatchSize, GUILayout.Width(LegendSwatchSize), GUILayout.Height(LegendSwatchSize));
                    EditorGUI.DrawRect(swatch, new Color32(r, g, b, 255));
                    GUILayout.Label(name, EditorStyles.miniLabel);
                }

                GUILayout.FlexibleSpace();
            }
        }

        // Writes the last bake as a MapAsset (grid, buildings, entrances, building meshes, ground texture) for the SubScene.
        // The asset is only highlighted in the Project window, not selected: selecting it would open it in the Inspector.
        void SaveMapAsset()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            MapAsset asset = MapAssetWriter.Write(result, resultOsmPath, settings != null ? settings.BuildingMeshTileSize : 250f);
            status = $"Saved {AssetDatabase.GetAssetPath(asset)} in {timer.ElapsedMilliseconds} ms ({asset.PackedLayerBytes / 1024} KB of grid data, " +
                     $"{asset.BuildingMeshes.Length} building meshes, checksum {asset.Checksum}). Assign it to a MapAuthoring in your SubScene, or use Tools > Map > Add Map To Scene.";
            EditorGUIUtility.PingObject(asset);
        }

        // Bakes the map once normally and 8 times at the same time on worker threads, then compares the checksums.
        void StartDeterminismCheck()
        {
            string osmPath = settings.OsmPath;
            if (string.IsNullOrEmpty(osmPath) || !osmPath.EndsWith(".osm") || !File.Exists(osmPath))
            {
                status = "The settings have no valid .osm file assigned.";
                return;
            }

            // The settings asset is a Unity object and may only be read on the main thread, so every bake gets its
            // own options object created here; the worker threads only see plain data.
            const int parallelRuns = 8;
            GridBuildOptions[] options = Enumerable.Range(0, parallelRuns + 1).Select(_ => settings.ToBuildOptions()).ToArray();
            int next = -1;
            Func<GridBuildOptions> createOptions = () => options[Interlocked.Increment(ref next)];

            status = $"Checking determinism: 1 normal bake and {parallelRuns} at the same time...";
            determinismCheck = Task.Run(() => GridDeterminismCheck.Run(OsmReader.ReadFile(osmPath), createOptions, parallelRuns));
        }

        void FinishDeterminismCheck()
        {
            Task<DeterminismResult> finished = determinismCheck;
            determinismCheck = null;

            if (finished.IsFaulted)
            {
                Exception error = finished.Exception.GetBaseException();
                status = $"Determinism check failed: {error.Message}";
                Debug.LogException(error);
            }
            else
            {
                status = finished.Result.ToString();
                if (finished.Result.AllIdentical)
                {
                    Debug.Log(status);
                }
                else
                {
                    Debug.LogError(status);
                }
            }

            Repaint();
        }

        void StartBake()
        {
            string osmPath = settings.OsmPath;
            if (string.IsNullOrEmpty(osmPath) || !osmPath.EndsWith(".osm") || !File.Exists(osmPath))
            {
                status = "The settings have no valid .osm file assigned.";
                return;
            }

            // Everything the worker thread needs is copied here, on the main thread, before it starts.
            GridBuildOptions options = settings.ToBuildOptions();
            resultOsmPath = osmPath;
            job = GridBakeJob.Start(osmPath, options, documentCache);
            status = "Baking in the background...";

            progressId = Progress.Start("Map bake", osmPath);
            Progress.RegisterCancelCallback(progressId, () =>
            {
                job?.Cancel();
                return true;
            });
        }

        // Runs every editor update: forwards progress and picks up the finished job on the main thread.
        void Poll()
        {
            if (determinismCheck != null && determinismCheck.IsCompleted)
            {
                FinishDeterminismCheck();
            }

            if (job == null)
            {
                return;
            }

            if (Progress.Exists(progressId))
            {
                Progress.Report(progressId, job.Progress, job.Phase);
            }

            if (!job.IsFinished)
            {
                Repaint();
                return;
            }

            Finish();
        }

        void Finish()
        {
            GridBakeJob finished = job;
            job = null;

            switch (finished.State)
            {
                case BakeJobState.Succeeded:
                    result = finished.Result;
                    report = result.ToString();
                    DestroyPreview();
                    preview = GridPreviewExporter.CreateTexture(result.Grid);
                    status = $"Baked in {finished.ElapsedMilliseconds} ms" +
                             (finished.DocumentWasReused ? " (parsed file reused)" : string.Empty) +
                             $", checksum {result.Checksum}. Hover over the picture to inspect cells.";
                    Debug.Log($"{resultOsmPath}\n{report}");
                    FinishProgress(Progress.Status.Succeeded);
                    break;

                case BakeJobState.Canceled:
                    status = "Bake canceled.";
                    FinishProgress(Progress.Status.Canceled);
                    break;

                default:
                    status = $"Bake failed: {finished.Error?.Message}";
                    Debug.LogException(finished.Error);
                    FinishProgress(Progress.Status.Failed);
                    break;
            }

            Repaint();
        }

        void FinishProgress(Progress.Status progressStatus)
        {
            if (Progress.Exists(progressId))
            {
                Progress.Finish(progressId, progressStatus);
            }

            progressId = -1;
        }

        // Called before a script reload and when the window closes: a running bake must not outlive them.
        void StopJob()
        {
            if (job != null)
            {
                job.Cancel();
                job = null;
            }

            if (progressId >= 0)
            {
                FinishProgress(Progress.Status.Canceled);
            }
        }

        void DestroyPreview()
        {
            if (preview != null)
            {
                DestroyImmediate(preview);
                preview = null;
            }
        }
    }
}
