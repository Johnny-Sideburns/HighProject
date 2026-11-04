# MapCore

Turns raw OpenStreetMap (OSM) data into a grid of 1 m cells that says where agents may walk. Plain C# with no Unity dependencies, so it can run on a background thread.

| Folder | Contents |
|---|---|
| `Osm` | Streaming reader for `.osm` XML files, the data model (nodes, ways, relations), tag helpers. |
| `Geo` | Latitude/longitude to metres (`LocalProjection`), rings and polygons with holes, multipolygon assembly, ear-clipping triangulation, segment maths. |
| `Classification` | Decides what each OSM feature is (building, road, water, barrier, rail, ...). |
| `Buildings` | Building footprints, heights (from tags, default 15 m), entrances matched to buildings, mesh data for drawing. |
| `Grid` | The grid itself (`MapGrid`), rasterizers for polygons and lines, ground surfaces, blocked cells, connectivity and spawn mask, manual overrides, validation, determinism check, checksum, background bake job, compression of the layers. |
| `Report` | Statistics about a bake. |

`GridBuilder` runs all steps in order and returns the finished grid. The result is stored by the `Map` module.

Unit tests are kept locally in `Assets/Tests/EditMode/MapCore` (not committed).

## Origin
`Osm`, `Geo`, `Classification` and `Buildings` are based on an earlier project of mine that processed OpenStreetMap data; the ear-clipping triangulation (`Geo/Triangulator.cs`) is my own implementation from another earlier project. The connectivity analysis in `Grid` is based on an earlier project where I built my own navigation grid. Adapted for this project. New here: the grid layers (flags, surface, cost, building id) built for the ECS map blob, overrides, validation, the determinism check and the background bake.
