using System;
using System.Collections.Generic;

namespace HighProject.MapCore
{
    public readonly struct OsmBounds
    {
        public readonly double MinLat;
        public readonly double MinLon;
        public readonly double MaxLat;
        public readonly double MaxLon;

        public OsmBounds(double minLat, double minLon, double maxLat, double maxLon)
        {
            MinLat = minLat;
            MinLon = minLon;
            MaxLat = maxLat;
            MaxLon = maxLon;
        }

        public double CenterLat => (MinLat + MaxLat) * 0.5;
        public double CenterLon => (MinLon + MaxLon) * 0.5;

        public bool Contains(double lat, double lon)
        {
            return lat >= MinLat && lat <= MaxLat && lon >= MinLon && lon <= MaxLon;
        }
    }

    public readonly struct OsmNode
    {
        public readonly double Lat;
        public readonly double Lon;

        public OsmNode(double lat, double lon)
        {
            Lat = lat;
            Lon = lon;
        }
    }

    // A node that carries at least one relevant tag (entrances, barriers, ...).
    // Untagged nodes only live in OsmDocument.Nodes.
    public sealed class OsmTaggedNode
    {
        public long Id { get; }
        public double Lat { get; }
        public double Lon { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }

        public OsmTaggedNode(long id, double lat, double lon, IReadOnlyDictionary<string, string> tags)
        {
            Id = id;
            Lat = lat;
            Lon = lon;
            Tags = tags;
        }
    }

    public sealed class OsmWay
    {
        public long Id { get; }
        public long[] NodeIds { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }

        public bool IsClosed => NodeIds.Length >= 4 && NodeIds[0] == NodeIds[NodeIds.Length - 1];

        public OsmWay(long id, long[] nodeIds, IReadOnlyDictionary<string, string> tags)
        {
            Id = id;
            NodeIds = nodeIds;
            Tags = tags;
        }
    }

    public enum OsmMemberType
    {
        Node,
        Way,
        Relation
    }

    public readonly struct OsmMember
    {
        public readonly OsmMemberType Type;
        public readonly long Ref;
        public readonly string Role;

        public OsmMember(OsmMemberType type, long reference, string role)
        {
            Type = type;
            Ref = reference;
            Role = role;
        }
    }

    public sealed class OsmRelation
    {
        public long Id { get; }
        public OsmMember[] Members { get; }
        public IReadOnlyDictionary<string, string> Tags { get; }

        public OsmRelation(long id, OsmMember[] members, IReadOnlyDictionary<string, string> tags)
        {
            Id = id;
            Members = members;
            Tags = tags;
        }
    }

    // Everything parsed from one .osm file. Ways, relations and tagged nodes keep only the tags listed in OsmTagFilter.
    public sealed class OsmDocument
    {
        public OsmBounds? Bounds { get; set; }
        public Dictionary<long, OsmNode> Nodes { get; } = new Dictionary<long, OsmNode>();
        public List<OsmTaggedNode> TaggedNodes { get; } = new List<OsmTaggedNode>();
        public List<OsmWay> Ways { get; } = new List<OsmWay>();
        public List<OsmRelation> Relations { get; } = new List<OsmRelation>();

        // The <bounds> element if the file has one, otherwise the extent of all nodes.
        public OsmBounds GetBoundsOrComputed()
        {
            if (Bounds.HasValue)
            {
                return Bounds.Value;
            }

            if (Nodes.Count == 0)
            {
                throw new InvalidOperationException("The OSM document has no bounds and no nodes.");
            }

            double minLat = double.MaxValue;
            double minLon = double.MaxValue;
            double maxLat = double.MinValue;
            double maxLon = double.MinValue;

            foreach (OsmNode node in Nodes.Values)
            {
                minLat = Math.Min(minLat, node.Lat);
                maxLat = Math.Max(maxLat, node.Lat);
                minLon = Math.Min(minLon, node.Lon);
                maxLon = Math.Max(maxLon, node.Lon);
            }

            return new OsmBounds(minLat, minLon, maxLat, maxLon);
        }
    }
}
