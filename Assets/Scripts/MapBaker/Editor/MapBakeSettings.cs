using System;
using System.Collections.Generic;
using HighProject.MapCore;
using UnityEditor;
using UnityEngine;

namespace HighProject.MapBaker
{
    // Everything that controls how an OSM file becomes a walkability grid. Create one with
    // Assets > Create > Map > Bake Settings, assign the .osm file and use the Map Bake window.
    [CreateAssetMenu(fileName = "MapBakeSettings", menuName = "Map/Bake Settings")]
    public sealed class MapBakeSettings : ScriptableObject
    {
        [Serializable]
        struct SurfaceCost
        {
            public SurfaceKind surface;
            [Range(1, 254)] public int cost;
        }

        [Serializable]
        class OverrideArea
        {
            public string name = "Override";
            public OverrideKind kind = OverrideKind.Block;
            public List<Vector2> points = new List<Vector2>();
        }

        [Header("Source")]
        [Tooltip("The OpenStreetMap XML file (.osm) to bake.")]
        [SerializeField] DefaultAsset osmFile = null;

        [Header("Grid")]
        [Tooltip("Edge length of one grid cell in metres. 1 m keeps narrow footpaths and alleys; 2 m is faster but can lose them.")]
        [SerializeField, Min(0.25f)] float cellSize = 1f;

        [Header("Buildings")]
        [Tooltip("Height used when a building has neither a height nor a levels tag (only affects the 3D meshes).")]
        [SerializeField, Min(1f)] float defaultBuildingHeight = 15f;
        [SerializeField, Min(1f)] float metersPerLevel = 3f;

        [Tooltip("Buildings are merged into one mesh per square tile of this size in metres (fewer draw calls, still culled by distance).")]
        [SerializeField, Min(25f)] float buildingMeshTileSize = 250f;

        [Header("Barriers")]
        [Tooltip("barrier=* values that block movement. Remove an entry to make that kind of barrier passable.")]
        [SerializeField] List<string> hardBarrierTypes = new List<string> { "wall", "retaining_wall", "fence", "hedge", "guard_rail", "city_wall" };

        [Header("Rail and gates")]
        [SerializeField, Min(0.5f)] float railBandWidth = 3f;
        [Tooltip("Radius of the opening cleared around a gate point in a wall, in metres.")]
        [SerializeField, Min(0.5f)] float gateOpeningRadius = 1f;

        [Header("Spawning")]
        [Tooltip("0 = agents may only start in the largest connected walkable area. Otherwise every area with at least this many cells counts.")]
        [SerializeField, Min(0)] int minSpawnComponentCells = 0;

        [Header("Manual overrides")]
        [Tooltip("Hand-made corrections on top of the OpenStreetMap data. Each one is a polygon (at least 3 points). " +
                 "Points are in metres: x = east, y = north, measured from the centre of the map. Click on the picture in the Map Bake window to see the position of a spot. " +
                 "Block = impassable, Open = force walkable (a door in a fence), Exclude / Include From Spawn = where agents may start. Open wins over Block.")]
        [SerializeField] List<OverrideArea> overrides = new List<OverrideArea>();

        [Header("Movement cost per ground type (lower = preferred)")]
        [SerializeField] SurfaceCost[] surfaceCosts = new SurfaceCost[0];

        public DefaultAsset OsmFile => osmFile;
        public float BuildingMeshTileSize => buildingMeshTileSize;

        // Project-relative path of the .osm file, or an empty string if none is assigned.
        public string OsmPath => osmFile != null ? AssetDatabase.GetAssetPath(osmFile) : string.Empty;

        public GridBuildOptions ToBuildOptions()
        {
            var options = new GridBuildOptions { CellSize = cellSize };
            options.BuildingHeights.DefaultHeightMeters = defaultBuildingHeight;
            options.BuildingHeights.MetersPerLevel = metersPerLevel;

            options.Classification.HardBarrierTypes.Clear();
            foreach (string type in hardBarrierTypes)
            {
                if (!string.IsNullOrWhiteSpace(type))
                {
                    options.Classification.HardBarrierTypes.Add(type.Trim());
                }
            }

            options.Blockers.RailBandWidthMeters = railBandWidth;
            options.Blockers.GateOpeningRadiusMeters = gateOpeningRadius;
            options.Spawn.MinComponentCells = minSpawnComponentCells;

            foreach (OverrideArea area in overrides)
            {
                var points = new List<Point2d>(area.points.Count);
                foreach (Vector2 point in area.points)
                {
                    points.Add(new Point2d(point.x, point.y));
                }

                // A bad override is not an error here: it is recorded and shown as a warning after the bake.
                options.Overrides.TryAdd(area.name, area.kind, points);
            }

            foreach (SurfaceCost entry in surfaceCosts)
            {
                options.Surface.CostBySurface[(int)entry.surface] = (byte)Mathf.Clamp(entry.cost, 1, 254);
            }

            return options;
        }

        void Reset()
        {
            FillMissingCosts();
        }

        void OnValidate()
        {
            FillMissingCosts();
        }

        // Keeps one entry per ground type, in enum order, so every type always has a cost.
        void FillMissingCosts()
        {
            byte[] defaults = new SurfaceOptions().CostBySurface;
            var kinds = (SurfaceKind[])Enum.GetValues(typeof(SurfaceKind));
            var complete = new SurfaceCost[kinds.Length];

            for (int i = 0; i < kinds.Length; i++)
            {
                complete[i] = new SurfaceCost { surface = kinds[i], cost = defaults[(int)kinds[i]] };
                foreach (SurfaceCost existing in surfaceCosts)
                {
                    if (existing.surface == kinds[i])
                    {
                        complete[i].cost = Mathf.Clamp(existing.cost, 1, 254);
                    }
                }
            }

            bool same = surfaceCosts.Length == complete.Length;
            for (int i = 0; same && i < complete.Length; i++)
            {
                same = surfaceCosts[i].surface == complete[i].surface && surfaceCosts[i].cost == complete[i].cost;
            }

            if (!same)
            {
                surfaceCosts = complete;
            }
        }
    }
}
