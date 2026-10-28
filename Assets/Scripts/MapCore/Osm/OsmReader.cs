using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml;

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
            var strings = new StringPool();

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
                        ReadNode(reader, document, strings);
                        break;
                    case "way":
                        ReadWay(reader, document, strings);
                        break;
                    case "relation":
                        ReadRelation(reader, document, strings);
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

        static void ReadNode(XmlReader reader, OsmDocument document, StringPool strings)
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
                        AddTag(ref tags, reader, strings);
                    }

                    reader.Skip();
                }

                reader.ReadEndElement();
            }

            document.Nodes[id] = new OsmNode(lat, lon);
            if (tags != null)
            {
                document.TaggedNodes.Add(new OsmTaggedNode(id, lat, lon, tags));
            }
        }

        static void ReadWay(XmlReader reader, OsmDocument document, StringPool strings)
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
                            AddTag(ref tags, reader, strings);
                        }
                    }

                    reader.Skip();
                }

                reader.ReadEndElement();
            }

            document.Ways.Add(new OsmWay(id, nodeIds.ToArray(), (IReadOnlyDictionary<string, string>)tags ?? OsmTags.Empty));
        }

        static void ReadRelation(XmlReader reader, OsmDocument document, StringPool strings)
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
                                strings.Intern(reader.GetAttribute("role") ?? string.Empty)));
                        }
                        else if (reader.Name == "tag")
                        {
                            AddTag(ref tags, reader, strings);
                        }
                    }

                    reader.Skip();
                }

                reader.ReadEndElement();
            }

            document.Relations.Add(new OsmRelation(id, members.ToArray(), (IReadOnlyDictionary<string, string>)tags ?? OsmTags.Empty));
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
