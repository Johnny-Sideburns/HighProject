using System;
using System.IO;
using System.IO.Compression;

namespace HighProject.MapCore
{
    // The four per-cell layers of a baked grid.
    public sealed class GridLayers
    {
        public byte[] Flags { get; }
        public byte[] Surface { get; }
        public byte[] Cost { get; }

        // Four bytes per cell (a little endian int), -1 = no building.
        public byte[] BuildingIdBytes { get; }

        public GridLayers(byte[] flags, byte[] surface, byte[] cost, byte[] buildingIdBytes)
        {
            Flags = flags;
            Surface = surface;
            Cost = cost;
            BuildingIdBytes = buildingIdBytes;
        }
    }

    // Packs the layers of a grid into one small byte array and back. A map has about a million cells and its layers
    // are extremely repetitive (long runs of the same value), so the packed form is a few percent of the raw size.
    // This matters in the Unity editor: a serialized array of millions of bytes makes the Inspector and the preview
    // generation allocate gigabytes, so the baked asset stores the packed form instead.
    public static class GridLayerCodec
    {
        const int BytesPerCell = 1 + 1 + 1 + sizeof(int);

        // Layout before compression: Flags, Surface, Cost, BuildingIdBytes, one after the other.
        public static byte[] Pack(GridLayers layers, int cellCount)
        {
            Validate(layers, cellCount);

            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
                {
                    deflate.Write(layers.Flags, 0, cellCount);
                    deflate.Write(layers.Surface, 0, cellCount);
                    deflate.Write(layers.Cost, 0, cellCount);
                    deflate.Write(layers.BuildingIdBytes, 0, cellCount * sizeof(int));
                }

                return output.ToArray();
            }
        }

        // Throws InvalidDataException if the data is damaged or was packed for a different number of cells.
        public static GridLayers Unpack(byte[] packed, int cellCount)
        {
            if (packed == null)
            {
                throw new ArgumentNullException(nameof(packed));
            }

            if (cellCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellCount), "The grid needs at least one cell.");
            }

            var flags = new byte[cellCount];
            var surface = new byte[cellCount];
            var cost = new byte[cellCount];
            var ids = new byte[cellCount * sizeof(int)];

            using (var input = new MemoryStream(packed))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            {
                ReadExactly(deflate, flags);
                ReadExactly(deflate, surface);
                ReadExactly(deflate, cost);
                ReadExactly(deflate, ids);

                if (deflate.ReadByte() != -1)
                {
                    throw new InvalidDataException("The packed grid layers are longer than expected for this grid size.");
                }
            }

            return new GridLayers(flags, surface, cost, ids);
        }

        static void Validate(GridLayers layers, int cellCount)
        {
            if (layers == null)
            {
                throw new ArgumentNullException(nameof(layers));
            }

            if (cellCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellCount), "The grid needs at least one cell.");
            }

            if (layers.Flags == null || layers.Flags.Length != cellCount
                || layers.Surface == null || layers.Surface.Length != cellCount
                || layers.Cost == null || layers.Cost.Length != cellCount
                || layers.BuildingIdBytes == null || layers.BuildingIdBytes.Length != cellCount * sizeof(int))
            {
                throw new ArgumentException($"Every layer must have exactly one entry per cell ({cellCount}; {BytesPerCell} bytes per cell in total).", nameof(layers));
            }
        }

        static void ReadExactly(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int read = stream.Read(buffer, total, buffer.Length - total);
                if (read <= 0)
                {
                    throw new InvalidDataException("The packed grid layers are shorter than expected for this grid size.");
                }

                total += read;
            }
        }
    }
}
