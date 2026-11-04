using Unity.Entities;
using UnityEngine;

public class NPCBaker : MonoBehaviour
{
    [SerializeField] private float _speed;
    class Baker : Baker<NPCBaker>
    {
        public override void Bake(NPCBaker authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new NPC
            {
                speed = authoring._speed
            });
        }
    }
}

public struct NPC : IComponentData
{
    public float speed;
}
