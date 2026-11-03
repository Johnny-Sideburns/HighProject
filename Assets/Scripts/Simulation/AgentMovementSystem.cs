using HighProject.Map;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace HighProject.Simulation
{
    // The map as the movement kernel sees it: walkable = inside the grid and not blocked.
    public readonly struct BlobWalkability : IWalkability
    {
        readonly BlobAssetReference<MapBlob> blob;

        public BlobWalkability(BlobAssetReference<MapBlob> blob)
        {
            this.blob = blob;
        }

        public float CellSize => blob.Value.CellSize;

        public bool IsWalkable(float2 position)
        {
            return blob.Value.IsWalkable(position);
        }
    }

    // Moves every agent one step per frame, in parallel.
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct AgentMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationConfig>();
            state.RequireForUpdate<MapSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SimulationConfig config = SystemAPI.GetSingleton<SimulationConfig>();
            new MoveJob
            {
                Map = new BlobWalkability(SystemAPI.GetSingleton<MapSingleton>().Blob),
                DeltaTime = math.min(SystemAPI.Time.DeltaTime, config.MaxTimeStep),
                HumanSpeed = config.HumanSpeed,
                ZombieSpeed = config.ZombieSpeed,
                WanderRules = config.Wander
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct MoveJob : IJobEntity
        {
            public BlobWalkability Map;
            public float DeltaTime;
            public float HumanSpeed;
            public float ZombieSpeed;
            public WanderParameters WanderRules;

            void Execute(ref AgentPosition position, in AgentState state, ref Wander wander)
            {
                float speed = state.Kind == AgentKind.Zombie ? ZombieSpeed : HumanSpeed;
                AgentMotion.Step(ref position.Value, ref wander.Heading, ref wander.SecondsUntilTurn, ref wander.Random,
                    speed, DeltaTime, in WanderRules, in Map);
            }
        }
    }
}
