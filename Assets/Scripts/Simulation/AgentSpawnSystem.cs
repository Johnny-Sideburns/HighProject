using HighProject.Map;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace HighProject.Simulation
{
    // Creates all agents once, as soon as both the config and the map exist: random positions on spawnable cells,
    // random headings, the first InitialZombies of them as zombies. Same seed, same start.
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(AgentMovementSystem))]
    public partial struct AgentSpawnSystem : ISystem
    {
        const int MaxCellTries = 1000;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationConfig>();
            state.RequireForUpdate<MapSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            SimulationConfig config = SystemAPI.GetSingleton<SimulationConfig>();
            BlobAssetReference<MapBlob> map = SystemAPI.GetSingleton<MapSingleton>().Blob;

            EntityArchetype archetype = state.EntityManager.CreateArchetype(
                typeof(AgentPosition), typeof(AgentState), typeof(Wander));
            state.EntityManager.CreateEntity(archetype, config.AgentCount, Allocator.Temp).Dispose();

            new SpawnJob { Map = map, Config = config }.ScheduleParallel();

            Debug.Log($"[Simulation] world '{state.WorldUnmanaged.Name}': spawned {config.AgentCount} agents, {config.InitialZombies} of them zombies.");
            state.Enabled = false;
        }

        [BurstCompile]
        partial struct SpawnJob : IJobEntity
        {
            [ReadOnly] public BlobAssetReference<MapBlob> Map;
            public SimulationConfig Config;

            void Execute([EntityIndexInQuery] int index, ref AgentPosition position, ref AgentState state, ref Wander wander)
            {
                ref MapBlob map = ref Map.Value;
                Random random = Random.CreateFromIndex((uint)index + (uint)Config.RandomSeed * 0x9E3779B9u);

                // Rejection sampling: pick random cells until one is spawnable (a large share of the map is).
                int x = 0;
                int z = 0;
                for (int attempt = 0; attempt < MaxCellTries; attempt++)
                {
                    x = random.NextInt(0, map.Width);
                    z = random.NextInt(0, map.Height);
                    if (map.IsSpawnable(map.Index(x, z)))
                    {
                        break;
                    }
                }

                position.Value = map.CellCenter(x, z) + (random.NextFloat2() - 0.5f) * map.CellSize;
                state.Kind = index < Config.InitialZombies ? AgentKind.Zombie : AgentKind.Human;
                wander.Heading = random.NextFloat(-math.PI, math.PI);
                wander.SecondsUntilTurn = random.NextFloat(Config.Wander.MinSecondsBetweenTurns, Config.Wander.MaxSecondsBetweenTurns);
                wander.Random = random;
            }
        }
    }
}
