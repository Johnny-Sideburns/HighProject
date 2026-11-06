# Map

Brings the baked map into Unity and DOTS so systems can read it.

| File | Purpose |
|---|---|
| `MapAsset` | The saved bake result (a ScriptableObject). Layers are packed and hidden from the Inspector, because millions of serialized values froze the editor. |
| `MapAuthoring` | Put on a GameObject inside a SubScene and assign the `MapAsset`. Baking turns it into a `MapSingleton` entity in every world that loads the SubScene. |
| `MapBlob`, `MapBlobBuilder` | A read-only data block with the grid layers (flags, surface, cost, building ids), the buildings and entrances. Safe to read from Burst jobs. |
| `MapBlobQueries` | Helper functions on the blob, for example `IsWalkable(position)`, `IsSpawnable(index)`. |
| `MapInfoLogSystem` | Logs the map once per world (size, counts, checksum) so you can see that server and client load the same map. |
| `MapRenderer` | Draws the ground and the buildings with ordinary GameObjects. Presentation only; the simulation never reads it. |

Assembly: `HighProject.Map`. Depends on `MapCore`.

## Origin
New for this project: the whole DOTS side of the map (map blob, authoring/baker, queries, per-world log). It connects the code from earlier projects of mine in `MapCore` to ECS.
