using UnityEngine;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

partial struct NPCSpawnSystem : ISystem
{
    private Unity.Mathematics.Random _random;
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _random = new Unity.Mathematics.Random(1234);
        state.RequireForUpdate<NPCSpawner>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var npcSpawner = SystemAPI.GetSingletonRW<NPCSpawner>();
        for (int n = 0; n < npcSpawner.ValueRO.npcCount; n++)
        {
            float randomX = _random.NextFloat(-50f, 50f);
            float randomZ = _random.NextFloat(-50f, 50f);
            Entity npc = state.EntityManager.Instantiate(npcSpawner.ValueRO.npcPrefab);
            state.EntityManager.SetComponentData(npc, LocalTransform.FromPosition(new float3(randomX, 1f, randomZ)));
        }
    }

}
