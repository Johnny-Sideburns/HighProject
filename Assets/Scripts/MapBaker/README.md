# MapBaker

Editor-only tools for creating the map. Nothing here is included in a build.

| File | Purpose |
|---|---|
| `MapBakeWindow` | The main window (Tools > Map). Bake in a background job, Cancel, Check Determinism, Save Map Asset, Export PNG. Shows validation results in plain words and the position of hovered/clicked cells. |
| `MapBakeSettings` | Settings asset for a bake: OSM file, cell size, surface and cost options, manual override areas (Block, Open, Exclude from spawn, Include in spawn). |
| `MapAssetWriter` | Saves the bake result as a `MapAsset`. |
| `MapAssetEditor` | Custom Inspector for `MapAsset` that shows a summary instead of the huge layers. |
| `MapSceneMenu` | Tools > Map > Add Map To Scene: adds the map renderer to the open scene. |
| `GridPreviewMenu`, `GridPreviewExporter` | Build the grid and export a coloured PNG preview. |
| `OsmReportMenu` | Prints a summary of an OSM file. |

Typical use: open the Bake window, bake, check the validation, Save Map Asset, then assign the asset to `MapAuthoring` in a SubScene.

## Origin
Editor tools like these follow what I built in earlier projects; they were adapted here for the map baker.
