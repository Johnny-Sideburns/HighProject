using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml;

// Based on my own earlier project that worked with OpenStreetMap (OSM) data; adapted here to feed the grid and the ECS map blob.
namespace HighProject.MapCore
{
    // Streaming reader for OSM XML (the format of the Overpass API and openstreetmap.org exports).
    // No Unity types are used, so it is safe to call from a worker thread.
    public static class OsmReader
    {
        const int ElementsPerProgressUpdate = 4096;

        public static OsmDocument ReadFile(string path, IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                return Read(stream, progress, cancellationToken);
            }
        }

        public static OsmDocument ReadString(string xml, CancellationToken cancellationToken = default)
        {
            using (var reader = new StringReader(xml))
            {
                return Read(reader, null, 0, cancellationToken);
            }
        }

        public static OsmDocument Read(Stream stream, IProgress<float> progress = null, CancellationToken cancellationToken = default)
        {
            long length = stream.CanSeek ? stream.Length : 0;
            var settings = CreateSettings();

            using (XmlReader xml = XmlReader.Create(stream, settings))
            {
                return ReadDocument(xml, stream, length, progress, cancellationToken);
            }
        }

        static OsmDocument Read(TextReader text, IProgress<float> progress, long length, CancellationToken cancellationToken)
        {
            var settings = CreateSettings();
            using (XmlReader xml = XmlReader.Create(text, settings))
            {
                return ReadDocument(xml, null, length, progress, cancellationToken);
            }
        }

        static XmlReaderSettings CreateSettings()
        {
            return new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true
            };
        }

        static OsmDocument ReadDocument(XmlReader reader, Stream positionSource, long length, IProgress<float> progress, CancellationToken cancellationToken)
        {
            var document = new OsmDocument();
            var context = new ReadContext(document);

            reader.MoveToContent();
            if (reader.NodeType != XmlNodeType.Element || reader.Name != "osm")
            {
                throw new InvalidDataException("Not an OSM XML file: the root element must be <osm>.");
            }

            if (reader.IsEmptyElement)
            {
                return document;
            }

            reader.Read();
            int elementCount = 0;

            while (!reader.EOF && reader.NodeType != XmlNodeType.EndElement)
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Skip();
                    continue;
                }

                switch (reader.Name)
                {
                    case "bounds":
                        ReadBounds(reader, document);
                        break;
                    case "node":
                        ReadNode(reader, context);
                        break;
                    case "way":
                        ReadWay(reader, context);
                        break;
                    case "relation":
                        ReadRelation(reader, context);
                        break;
                    default:
                        reader.Skip();
                        break;
                }

                elementCount++;
                if (elementCount % ElementsPerProgressUpdate == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (progress != null && positionSource != null && length > 0)
                    {
                        progress.Report((float)((double)positionSource.Position / length));
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            document.PrepareIndexes();
            progress?.Report(1f);
            return document;
        }

        static void ReadBounds(XmlReader reader, OsmDocument document)
        {
            document.Bounds = new OsmBounds(
                ParseDouble(reader.GetAttribute("minlat")),
                ParseDouble(reader.GetAttribute("minlon")),
                ParseDouble(reader.GetAttribute("maxlat")),
                ParseDouble(reader.GetAttribute("maxlon")));
            reader.Skip();
        }

        static void ReadNode(XmlReader reader, ReadContext context)
        {
            long id = ParseLong(reader.GetAttribute("id"));
            double lat = ParseDouble(reader.GetAttribute("lat"));
            double lon = ParseDouble(reader.GetAttribute("lon"));
            Dictionary<string, string> tags = null;

            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.Read();
                while (!reader.EOF && reader.NodeType != XmlNodeType.EndElement)
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "tag")
                    {
                        AddTag(ref tags, reader, context.Strings);
                    }

                    reader.Skip();
                }

                reader.ReadEndElement();
            }

            context.Document.Nodes[id] = new OsmNode(lat, lon);
            if (tags != null && context.TaggedNodeIds.Add(id))
            {
                context.Document.TaggedNodes.Add(new OsmTaggedNode(id, lat, lon, tags));
            }
        }

        static void ReadWay(XmlReader reader, ReadContext context)
        {
            long id = ParseLong(reader.GetAttribute("id"));
            var nodeIds = new List<long>();
            Dictionary<string, string> tags = null;

            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.Read();
                while (!reader.EOF && reader.NodeType != XmlNodeType.EndElement)
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "nd")
                        {
                            nodeIds.Add(ParseLong(reader.GetAttribute("ref")));
                        }
                        else if (reader.Name == "tag")
                        {
                            AddTag(ref tags, reader, context.Strings);
                        }
                    }

                    reader.Skip();
                }

                reader.ReadEndElement();
            }

            var way = new OsmWay(id, nodeIds.ToArray(), (IReadOnlyDictionary<string, string>)tags ?? OsmTags.Empty);
            AddOrMerge(context.Document.Ways, context.WayIndex, id, way, way.Tags.Count > 0);
        }

        static void ReadRelation(XmlReader reader, ReadContext context)
        {
            long id = ParseLong(reader.GetAttribute("id"));
            var members = new List<OsmMember>();
            Dictionary<string, string> tags = null;

            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.Read();
                while (!reader.EOF && reader.NodeType != XmlNodeType.EndElement)
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Name == "member")
                        {
                            members.Add(new OsmMember(
                                ParseMemberType(reader.GetAttribute("type")),
                                ParseLong(reader.GetAttribute("ref")),
                                context.Strings.Intern(reader.GetAttribute("role") ?? string.Empty)));
                        }
                        else if (reader.Name == "tag")
                        {
                            AddTag(ref tags, reader, context.Strings);
                        }
                    }

                    reader.Skip();
                }

                reader.ReadEndElement();
            }

            var relation = new OsmRelation(id, members.ToArray(), (IReadOnlyDictionary<string, string>)tags ?? OsmTags.Empty);
            AddOrMerge(context.Document.Relations, context.RelationIndex, id, relation, relation.Tags.Count > 0);
        }

        // Overpass queries ending in "out body; >; out skel qt;" can list an element twice: once complete and once
        // as an untagged skeleton copy. Keep one element per id and prefer the one that carries tags.
        static void AddOrMerge<T>(List<T> list, Dictionary<long, int> indexById, long id, T element, bool hasTags)
        {
            if (!indexById.TryGetValue(id, out int existing))
            {
                indexById[id] = list.Count;
                list.Add(element);
            }
            else if (hasTags)
            {
                list[existing] = element;
            }
        }

        static void AddTag(ref Dictionary<string, string> tags, XmlReader reader, StringPool strings)
        {
            string key = reader.GetAttribute("k");
            if (key == null || !OsmTagFilter.IsRelevantKey(key))
            {
                return;
            }

            if (tags == null)
            {
                tags = new Dictionary<string, string>();
            }

            tags[strings.Intern(key)] = strings.Intern(reader.GetAttribute("v") ?? string.Empty);
        }

        static OsmMemberType ParseMemberType(string value)
        {
            switch (value)
            {
                case "node": return OsmMemberType.Node;
                case "way": return OsmMemberType.Way;
                case "relation": return OsmMemberType.Relation;
                default: throw new InvalidDataException($"Unknown OSM member type '{value}'.");
            }
        }

        static long ParseLong(string value)
        {
            if (value == null || !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result))
            {
                throw new InvalidDataException($"Expected an integer id but found '{value}'.");
            }

            return result;
        }

        static double ParseDouble(string value)
        {
            if (value == null || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            {
                throw new InvalidDataException($"Expected a number but found '{value}'.");
            }

            return result;
        }

        sealed class ReadContext
        {
            public OsmDocument Document { get; }
            public StringPool Strings { get; } = new StringPool();
            public Dictionary<long, int> WayIndex { get; } = new Dictionary<long, int>();
            public Dictionary<long, int> RelationIndex { get; } = new Dictionary<long, int>();
            public HashSet<long> TaggedNodeIds { get; } = new HashSet<long>();

            public ReadContext(OsmDocument document)
            {
                Document = document;
            }
        }

        // Tag keys and values repeat thousands of times ("yes", "footway", ...); share one instance of each.
        sealed class StringPool
        {
            readonly Dictionary<string, string> pool = new Dictionary<string, string>();

            public string Intern(string value)
            {
                if (pool.TryGetValue(value, out string existing))
                {
                    return existing;
                }

                pool[value] = value;
                return value;
            }
        }
    }
}
