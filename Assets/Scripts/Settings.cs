using Unity.Entities;
using UnityEngine;

public class Settings : MonoBehaviour
{
    [SerializeField] private int _nPCCount;
    [SerializeField] private GameObject _nPCPrefab;
    
    class Baker : Baker<Settings>
    {
        public override void Bake(Settings authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new NPCSpawner
            {
                npcCount = authoring._nPCCount,
                npcPrefab = GetEntity(authoring._nPCPrefab, TransformUsageFlags.Dynamic)
            });
        }
    }
    
}

public struct NPCSpawner : IComponentData
{
    public int npcCount;
    public Entity npcPrefab;
}