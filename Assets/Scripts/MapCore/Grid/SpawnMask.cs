namespace HighProject.MapCore
{
    public sealed class SpawnMaskOptions
    {
        // Components with at least this many cells are spawnable. 0 (the default) means only the largest component,
        // so agents never start on an island or in a sealed courtyard they could not leave.
        public int MinComponentCells { get; set; }
    }

    public static class SpawnMask
    {
        // Sets CellFlags.Spawnable on the walkable cells of the accepted components and clears it everywhere else.
        // Returns the number of spawnable cells.
        public static int Apply(MapGrid grid, ConnectivityResult connectivity, SpawnMaskOptions options)
        {
            bool[] accepted = new bool[connectivity.Components.Count];
            if (options.MinComponentCells <= 0)
            {
                if (connectivity.LargestComponentId >= 0)
                {
                    accepted[connectivity.LargestComponentId] = true;
                }
            }
            else
            {
                foreach (ComponentInfo component in connectivity.Components)
                {
                    accepted[component.Id] = component.CellCount >= options.MinComponentCells;
                }
            }

            int spawnable = 0;
            for (int i = 0; i < grid.CellCount; i++)
            {
                int component = connectivity.ComponentIdByCell[i];
                if (component >= 0 && accepted[component])
                {
                    grid.SetFlag(i, CellFlags.Spawnable);
                    spawnable++;
                }
                else
                {
                    grid.ClearFlag(i, CellFlags.Spawnable);
                }
            }

            return spawnable;
        }
    }
}
