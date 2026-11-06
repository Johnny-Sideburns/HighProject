using System.IO;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HighProject.Simulation
{
    // Reads StreamingAssets/simulation.json once when the world is created and publishes it as the SimulationConfig
    // singleton. A missing or broken file is not fatal: the defaults are used and a warning says why.
    // Not game logic and runs once, so it is a managed system (it uses Unity's file path and JSON reader).
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SimulationConfigLoadSystem : SystemBase
    {
        public const string FileName = "simulation.json";

        protected override void OnCreate()
        {
            var data = new SimulationConfigData();
            string path = Path.Combine(Application.streamingAssetsPath, FileName);

            if (!File.Exists(path))
            {
                Debug.LogWarning($"[Simulation] {path} not found, using the default settings.");
            }
            else
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(path), data);
                }
                catch (System.Exception exception)
                {
                    data = new SimulationConfigData();
                    Debug.LogWarning($"[Simulation] {path} could not be read ({exception.Message}), using the default settings.");
                }
            }

            foreach (string note in data.Sanitize())
            {
                Debug.LogWarning($"[Simulation] {FileName}: {note}");
            }

            EntityManager.CreateSingleton(new SimulationConfig
            {
                AgentCount = data.agentCount,
                InitialZombies = data.initialZombies,
                RandomSeed = data.randomSeed,
                HumanSpeed = data.humanSpeed,
                ZombieSpeed = data.zombieSpeed,
                MaxTimeStep = data.maxTimeStepSeconds,
                Wander = new WanderParameters
                {
                    MinSecondsBetweenTurns = data.minSecondsBetweenTurns,
                    MaxSecondsBetweenTurns = data.maxSecondsBetweenTurns,
                    MaxTurnRadians = math.radians(data.maxTurnDegrees)
                }
            });

            Enabled = false;
        }

        protected override void OnUpdate()
        {
        }
    }
}
