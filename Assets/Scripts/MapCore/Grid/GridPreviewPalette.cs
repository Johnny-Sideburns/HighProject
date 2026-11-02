namespace HighProject.MapCore
{
    // Colours for drawing a grid as an image. Pure C#, so the editor preview and the command-line checks
    // draw exactly the same picture.
    public static class GridPreviewPalette
    {
        static readonly (byte, byte, byte) BuildingColor = (40, 50, 80);
        static readonly (byte, byte, byte) WaterColor = (90, 150, 215);
        static readonly (byte, byte, byte) RailColor = (160, 60, 160);
        static readonly (byte, byte, byte) BarrierColor = (190, 90, 40);

        // Walkable cells that are not part of the spawn area (sealed courtyards, military grounds, ...).
        static readonly (byte, byte, byte) UnreachableColor = (235, 90, 90);

        static readonly (byte, byte, byte) GroundColor = (215, 205, 195);
        static readonly (byte, byte, byte) GrassColor = (150, 200, 120);
        static readonly (byte, byte, byte) WoodlandColor = (70, 140, 80);
        static readonly (byte, byte, byte) PlazaColor = (235, 215, 170);
        static readonly (byte, byte, byte) FootpathColor = (250, 250, 250);
        static readonly (byte, byte, byte) StepsColor = (240, 140, 60);
        static readonly (byte, byte, byte) MinorRoadColor = (170, 170, 170);
        static readonly (byte, byte, byte) MajorRoadColor = (120, 120, 120);

        // Name and colour of every kind of cell, for drawing a legend next to the picture.
        public static readonly (string Name, byte R, byte G, byte B)[] Legend =
        {
            ("Building", BuildingColor.Item1, BuildingColor.Item2, BuildingColor.Item3),
            ("Water", WaterColor.Item1, WaterColor.Item2, WaterColor.Item3),
            ("Wall, fence, hedge", BarrierColor.Item1, BarrierColor.Item2, BarrierColor.Item3),
            ("Rail, ventilation shaft", RailColor.Item1, RailColor.Item2, RailColor.Item3),
            ("Cut off from the main area", UnreachableColor.Item1, UnreachableColor.Item2, UnreachableColor.Item3),
            ("Open ground", GroundColor.Item1, GroundColor.Item2, GroundColor.Item3),
            ("Grass", GrassColor.Item1, GrassColor.Item2, GrassColor.Item3),
            ("Woodland", WoodlandColor.Item1, WoodlandColor.Item2, WoodlandColor.Item3),
            ("Plaza", PlazaColor.Item1, PlazaColor.Item2, PlazaColor.Item3),
            ("Footpath", FootpathColor.Item1, FootpathColor.Item2, FootpathColor.Item3),
            ("Steps", StepsColor.Item1, StepsColor.Item2, StepsColor.Item3),
            ("Minor road", MinorRoadColor.Item1, MinorRoadColor.Item2, MinorRoadColor.Item3),
            ("Major road", MajorRoadColor.Item1, MajorRoadColor.Item2, MajorRoadColor.Item3)
        };

        public static (byte R, byte G, byte B) ColorOf(MapGrid grid, int index)
        {
            byte flags = grid.Flags[index];

            if ((flags & (byte)CellFlags.Blocked) != 0)
            {
                if ((flags & (byte)CellFlags.Water) != 0) return WaterColor;
                if ((flags & (byte)CellFlags.Rail) != 0) return RailColor;
                if ((flags & (byte)CellFlags.Barrier) != 0 && (flags & (byte)CellFlags.Building) == 0) return BarrierColor;
                return BuildingColor;
            }

            if ((flags & (byte)CellFlags.Spawnable) == 0)
            {
                return UnreachableColor;
            }

            switch ((SurfaceKind)grid.Surface[index])
            {
                case SurfaceKind.Grass: return GrassColor;
                case SurfaceKind.Woodland: return WoodlandColor;
                case SurfaceKind.Plaza: return PlazaColor;
                case SurfaceKind.Footpath: return FootpathColor;
                case SurfaceKind.Steps: return StepsColor;
                case SurfaceKind.MinorRoad: return MinorRoadColor;
                case SurfaceKind.MajorRoad: return MajorRoadColor;
                default: return GroundColor;
            }
        }

        // Colour of the ground itself, for the game map: buildings are drawn as plain ground (the 3D building covers
        // them) and cut-off areas look like any other ground, unlike the preview picture which highlights both.
        public static (byte R, byte G, byte B) GroundColorOf(MapGrid grid, int index)
        {
            byte flags = grid.Flags[index];

            if ((flags & (byte)CellFlags.Water) != 0) return WaterColor;
            if ((flags & (byte)CellFlags.Rail) != 0) return RailColor;
            if ((flags & (byte)CellFlags.Barrier) != 0) return BarrierColor;

            switch ((SurfaceKind)grid.Surface[index])
            {
                case SurfaceKind.Grass: return GrassColor;
                case SurfaceKind.Woodland: return WoodlandColor;
                case SurfaceKind.Plaza: return PlazaColor;
                case SurfaceKind.Footpath: return FootpathColor;
                case SurfaceKind.Steps: return StepsColor;
                case SurfaceKind.MinorRoad: return MinorRoadColor;
                case SurfaceKind.MajorRoad: return MajorRoadColor;
                default: return GroundColor;
            }
        }

        // Ground texture pixels (RGB24) for Unity, southern row first, see GroundColorOf.
        public static byte[] RenderGroundRgb(MapGrid grid)
        {
            var pixels = new byte[grid.CellCount * 3];
            int offset = 0;
            for (int z = 0; z < grid.Height; z++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    (byte r, byte g, byte b) = GroundColorOf(grid, grid.Index(x, z));
                    pixels[offset++] = r;
                    pixels[offset++] = g;
                    pixels[offset++] = b;
                }
            }

            return pixels;
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
