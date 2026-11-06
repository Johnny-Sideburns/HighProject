# Simulation

The agents (humans, later infected and zombies) and how they move on the map. Runs in the server world (or a plain local world).

| File | Purpose |
|---|---|
| `SimulationConfigData` | Model of `Assets/StreamingAssets/simulation.json` (agent count, zombies, seed, speeds, turn timing). `Sanitize()` fixes impossible values and reports what it changed. |
| `SimulationConfigLoadSystem` | Reads the file once at world creation and publishes the `SimulationConfig` singleton. Missing or broken file = defaults and a warning. |
| `AgentComponents` | The data: `AgentPosition` (metres, x east, y north), `AgentState` (Human, Infected, Zombie), `Wander` (heading, turn timer, own random generator), `SimulationConfig`. |
| `AgentSpawnSystem` | Creates all agents once on random spawnable cells. The first N are zombies. Same seed gives the same start. |
| `AgentMotion` | The walking itself, no Unity types: keep a heading, turn a little now and then, slide along walls, turn away from them. Moves are split into steps of half a cell at most, so nobody jumps through a wall. Asks the map only "is this position walkable?". |
| `AgentMovementSystem` | Runs `AgentMotion` for every agent each frame in a parallel Burst job. Zombies use the faster speed. |
| `AgentRenderer` | Draws the agents with GPU instancing (green humans, yellow infected, red zombies). Presentation only. Add it to a GameObject in the scene. |

Assembly: `HighProject.Simulation`. Depends on `Map`.

To run: map SubScene with `MapAuthoring`, an object with `Agent Renderer`, press Play. Change the scale in `simulation.json`.

## Origin
The agent simulation (components, config, spawning, movement systems) is new for this project. `AgentRenderer`'s instanced drawing is a small part taken from an earlier project of mine.
