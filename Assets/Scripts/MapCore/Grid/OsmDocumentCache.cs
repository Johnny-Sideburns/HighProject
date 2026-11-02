using System;
using System.IO;

namespace HighProject.MapCore
{
    // Remembers the last parsed OSM file so that re-baking after a settings change does not parse it again.
    // The cached file counts as unchanged while its path, size and last write time are the same.
    // Only one bake should use the cache at a time.
    public sealed class OsmDocumentCache
    {
        string path;
        long length;
        DateTime lastWriteUtc;
        OsmDocument document;

        // Returns the cached document if it still matches the file on disk, otherwise null.
        public OsmDocument TryGet(string osmPath)
        {
            if (document == null || path != Path.GetFullPath(osmPath) || !File.Exists(osmPath))
            {
                return null;
            }

            var info = new FileInfo(osmPath);
            return info.Length == length && info.LastWriteTimeUtc == lastWriteUtc ? document : null;
        }

        public void Store(string osmPath, OsmDocument parsed)
        {
            var info = new FileInfo(osmPath);
            path = Path.GetFullPath(osmPath);
            length = info.Length;
            lastWriteUtc = info.LastWriteTimeUtc;
            document = parsed;
        }

        public void Clear()
        {
            document = null;
            path = null;
        }
    }
}
