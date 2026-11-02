namespace HighProject.MapCore
{
    // Colours for drawing a grid as an image. Pure C#, so the editor preview and the command-line checks
    // draw exactly the same picture.
    public static class GridPreviewPalette
    {
        // Walkable cells that are not part of the spawn area (sealed courtyards, military grounds, ...).
        static readonly (byte, byte, byte) Unreachable = (235, 90, 90);

        public static (byte R, byte G, byte B) ColorOf(MapGrid grid, int index)
        {
            byte flags = grid.Flags[index];

            if ((flags & (byte)CellFlags.Blocked) != 0)
            {
                if ((flags & (byte)CellFlags.Water) != 0) return (90, 150, 215);
                if ((flags & (byte)CellFlags.Rail) != 0) return (160, 60, 160);
                if ((flags & (byte)CellFlags.Barrier) != 0 && (flags & (byte)CellFlags.Building) == 0) return (190, 90, 40);
                return (40, 50, 80);
            }

            if ((flags & (byte)CellFlags.Spawnable) == 0)
            {
                return Unreachable;
            }

            switch ((SurfaceKind)grid.Surface[index])
            {
                case SurfaceKind.Grass: return (150, 200, 120);
                case SurfaceKind.Woodland: return (70, 140, 80);
                case SurfaceKind.Plaza: return (235, 215, 170);
                case SurfaceKind.Footpath: return (250, 250, 250);
                case SurfaceKind.Steps: return (240, 140, 60);
                case SurfaceKind.MinorRoad: return (170, 170, 170);
                case SurfaceKind.MajorRoad: return (120, 120, 120);
                default: return (215, 205, 195);
            }
        }

        // RGB24 pixels, three bytes per cell, row by row. With topRowFirst the northern row (largest z) comes first,
        // which is what image files expect; Unity textures expect the southern row first, so pass false there.
        public static byte[] RenderRgb(MapGrid grid, bool topRowFirst)
        {
            var pixels = new byte[grid.CellCount * 3];
            for (int row = 0; row < grid.Height; row++)
            {
                int z = topRowFirst ? grid.Height - 1 - row : row;
                int offset = row * grid.Width * 3;
                for (int x = 0; x < grid.Width; x++)
                {
                    (byte r, byte g, byte b) = ColorOf(grid, grid.Index(x, z));
                    pixels[offset++] = r;
                    pixels[offset++] = g;
                    pixels[offset++] = b;
                }
            }

            return pixels;
        }
    }
}
