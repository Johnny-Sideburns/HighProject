using Unity.Mathematics;

namespace HighProject.Simulation
{
    // What the movement needs to know about the map. Implemented by the baked map blob in the game and by plain
    // grids in tests, so the movement code itself needs neither Entities nor Unity.
    public interface IWalkability
    {
        float CellSize { get; }

        // True if an agent may stand at this position (inside the map and not blocked).
        bool IsWalkable(float2 position);
    }

    public struct WanderParameters
    {
        public float MinSecondsBetweenTurns;
        public float MaxSecondsBetweenTurns;
        public float MaxTurnRadians;
    }

    // How a wandering agent moves. A plain static function (no state of its own), written to run inside Burst jobs.
    // Positions are metres: x east, y north. Heading is an angle in radians, 0 = east, pi/2 = north.
    public static class AgentMotion
    {
        // A move is split into steps no longer than this share of a cell, so fast agents and long time steps can
        // never jump across a one-cell wall.
        const float MaxStepShareOfCell = 0.5f;

        // Moves the agent for deltaTime seconds at the given speed: it keeps its heading, turns a little now and then,
        // slides along walls it runs into and turns away from them. Never ends up on a blocked cell.
        public static void Step<TMap>(
            ref float2 position,
            ref float heading,
            ref float secondsUntilTurn,
            ref Random random,
            float speed,
            float deltaTime,
            in WanderParameters wander,
            in TMap map) where TMap : struct, IWalkability
        {
            secondsUntilTurn -= deltaTime;
            if (secondsUntilTurn <= 0f)
            {
                heading += random.NextFloat(-wander.MaxTurnRadians, wander.MaxTurnRadians);
                secondsUntilTurn = random.NextFloat(wander.MinSecondsBetweenTurns, wander.MaxSecondsBetweenTurns);
            }

            if (!map.IsWalkable(position))
            {
                // Should not happen (agents start on walkable cells and never leave them); stand still rather than guess.
                return;
            }

            float distance = speed * deltaTime;
            int steps = math.max(1, (int)math.ceil(distance / (map.CellSize * MaxStepShareOfCell)));
            float stepLength = distance / steps;

            for (int i = 0; i < steps; i++)
            {
                float2 step = new float2(math.cos(heading), math.sin(heading)) * stepLength;

                if (map.IsWalkable(position + step))
                {
                    position += step;
                    continue;
                }

                // Blocked: slide along the wall if one direction is free, then turn around and stop for this tick.
                if (map.IsWalkable(position + new float2(step.x, 0f)))
                {
                    position.x += step.x;
                }
                else if (map.IsWalkable(position + new float2(0f, step.y)))
                {
                    position.y += step.y;
                }

                heading += math.PI + random.NextFloat(-math.PI * 0.5f, math.PI * 0.5f);
                secondsUntilTurn = random.NextFloat(wander.MinSecondsBetweenTurns, wander.MaxSecondsBetweenTurns);
                break;
            }

            heading = Wrap(heading);
        }

        // Brings an angle into the range -pi to pi.
        public static float Wrap(float angle)
        {
            const float twoPi = 2f * math.PI;
            return angle - twoPi * math.floor((angle + math.PI) / twoPi);
        }
    }
}
