using System;
using System.Collections.Generic;
using System.Globalization;

namespace HighProject.Simulation
{
    // The contents of simulation.json (Assets/StreamingAssets). Edit the file to change the size and the rules of
    // the simulation without touching code. Fields that are missing from the file keep the values below.
    // Plain data: the fields are public because Unity's JSON reader needs that.
    [Serializable]
    public sealed class SimulationConfigData
    {
        public const int MaxAgents = 5000000;
        public const float MaxSpeed = 20f;

        // How many agents are created at the start (the scale of the simulation).
        public int agentCount = 10000;

        // How many of them start as zombies (the rest are humans).
        public int initialZombies = 100;

        // Same seed, same starting positions.
        public int randomSeed = 12345;

        // Metres per second. A relaxed walk is about 1.4.
        public float humanSpeed = 1.4f;
        public float zombieSpeed = 2.0f;

        // How often a wandering agent picks a slightly new direction, in seconds.
        public float minSecondsBetweenTurns = 2f;
        public float maxSecondsBetweenTurns = 6f;

        // The largest change of direction at such a turn, in degrees.
        public float maxTurnDegrees = 70f;

        // Longest time step the movement uses, in seconds; slow frames are cut to this so agents do not jump.
        public float maxTimeStepSeconds = 0.1f;

        // Fixes values that cannot work and says what it changed. Never throws.
        public List<string> Sanitize()
        {
            var notes = new List<string>();

            agentCount = ClampInt(agentCount, 0, MaxAgents, nameof(agentCount), notes);
            initialZombies = ClampInt(initialZombies, 0, agentCount, nameof(initialZombies), notes);
            humanSpeed = ClampFloat(humanSpeed, 0.05f, MaxSpeed, nameof(humanSpeed), notes);
            zombieSpeed = ClampFloat(zombieSpeed, 0.05f, MaxSpeed, nameof(zombieSpeed), notes);
            minSecondsBetweenTurns = ClampFloat(minSecondsBetweenTurns, 0.05f, 3600f, nameof(minSecondsBetweenTurns), notes);
            maxSecondsBetweenTurns = ClampFloat(maxSecondsBetweenTurns, minSecondsBetweenTurns, 3600f, nameof(maxSecondsBetweenTurns), notes);
            maxTurnDegrees = ClampFloat(maxTurnDegrees, 0f, 180f, nameof(maxTurnDegrees), notes);
            maxTimeStepSeconds = ClampFloat(maxTimeStepSeconds, 0.001f, 1f, nameof(maxTimeStepSeconds), notes);
            return notes;
        }

        static int ClampInt(int value, int min, int max, string name, List<string> notes)
        {
            int clamped = Math.Max(min, Math.Min(max, value));
            if (clamped != value)
            {
                notes.Add(string.Format(CultureInfo.InvariantCulture, "{0} was {1}, changed to {2} (allowed {3} to {4}).", name, value, clamped, min, max));
            }

            return clamped;
        }

        static float ClampFloat(float value, float min, float max, string name, List<string> notes)
        {
            float clamped = float.IsNaN(value) ? min : Math.Max(min, Math.Min(max, value));
            if (clamped != value)
            {
                notes.Add(string.Format(CultureInfo.InvariantCulture, "{0} was {1}, changed to {2} (allowed {3} to {4}).", name, value, clamped, min, max));
            }

            return clamped;
        }
    }
}
