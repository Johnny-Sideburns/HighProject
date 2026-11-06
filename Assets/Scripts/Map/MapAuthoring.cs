using Unity.Entities;
using UnityEngine;

namespace HighProject.Map
{
    // Put this on a GameObject inside a SubScene and assign the baked MapAsset. Baking turns the asset into a
    // MapSingleton entity in every world that loads the SubScene (server and clients alike).
    [DisallowMultipleComponent]
    public sealed class MapAuthoring : MonoBehaviour
    {
        [Tooltip("The baked map, created with the Map Bake window (Save Map Asset).")]
        [SerializeField] MapAsset mapAsset = null;

        public MapAsset MapAsset => mapAsset;

        sealed class MapBaker : Baker<MapAuthoring>
        {
            public override void Bake(MapAuthoring authoring)
            {
                if (authoring.mapAsset == null)
                {
                    Debug.LogError("MapAuthoring has no MapAsset assigned: bake the map in the Map Bake window, save the asset and assign it here.", authoring);
                    return;
                }

                // Bake again whenever the asset changes.
                DependsOn(authoring.mapAsset);

                BlobAssetReference<MapBlob> blob = MapBlobBuilder.Build(authoring.mapAsset, Unity.Collections.Allocator.Persistent);
                AddBlobAsset(ref blob, out _);

                Entity entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new MapSingleton { Blob = blob });
            }
        }
    }
}
