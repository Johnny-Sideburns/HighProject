using Unity.Entities;
using UnityEngine;

namespace HighProject.Map
{
    // Logs the map once per world as soon as it is available. This is a check, not game logic: with the Netcode
    // bootstrap the log appears once for the server world and once for every client world, which shows that all
    // of them loaded the same map (same checksum).
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial struct MapInfoLogSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MapSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            ref MapBlob map = ref SystemAPI.GetSingleton<MapSingleton>().Blob.Value;

            int blocked = 0;
            int spawnable = 0;
            for (int i = 0; i < map.CellCount(); i++)
            {
                if (map.IsBlocked(i))
                {
                    blocked++;
                }

                if (map.IsSpawnable(i))
                {
                    spawnable++;
                }
            }

            Debug.Log($"[Map] world '{state.WorldUnmanaged.Name}': {map.Width} x {map.Height} cells of {map.CellSize} m, " +
                      $"{map.Buildings.Length} buildings, {map.Entrances.Length} entrances, {blocked} blocked and {spawnable} spawnable cells, " +
                      $"checksum {map.Checksum:x16}");

            // Once is enough.
            state.Enabled = false;
        }
    }
}
