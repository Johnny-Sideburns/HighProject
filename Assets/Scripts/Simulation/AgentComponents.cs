using Unity.Entities;
using Unity.Mathematics;

namespace HighProject.Simulation
{
    // Where the agent is on the map, in metres (x east, y north), the same coordinates as the MapBlob.
    public struct AgentPosition : IComponentData
    {
        public float2 Value;
    }

    public enum AgentKind : byte
    {
        Human = 0,
        Infected = 1,
        Zombie = 2
    }

    public struct AgentState : IComponentData
    {
        public AgentKind Kind;
    }

    // State of the random wandering: direction in radians (0 = east), time until the next small turn,
    // and the agent's own random generator so the walk is repeatable.
    public struct Wander : IComponentData
    {
        public float Heading;
        public float SecondsUntilTurn;
        public Random Random;
    }

    // The loaded simulation.json as a singleton, so systems (and later the network) can read it.
    public struct SimulationConfig : IComponentData
    {
        public int AgentCount;
        public int InitialZombies;
        public int RandomSeed;
        public float HumanSpeed;
        public float ZombieSpeed;
        public float MaxTimeStep;
        public WanderParameters Wander;
    }
}
