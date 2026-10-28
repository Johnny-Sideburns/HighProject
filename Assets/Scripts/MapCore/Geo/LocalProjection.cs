using System;

namespace HighProject.MapCore
{
    // Equirectangular projection onto a local tangent plane around a centre point.
    // x = east, z = north, in metres, origin at the centre. Error over a few km is far below one grid cell.
    public readonly struct LocalProjection
    {
        public const double EarthRadiusMeters = 6371008.8;
        const double DegreesToRadians = Math.PI / 180.0;

        public readonly double CenterLat;
        public readonly double CenterLon;

        readonly double metersPerDegreeLat;
        readonly double metersPerDegreeLon;

        public LocalProjection(double centerLat, double centerLon)
        {
            CenterLat = centerLat;
            CenterLon = centerLon;
            metersPerDegreeLat = EarthRadiusMeters * DegreesToRadians;
            metersPerDegreeLon = EarthRadiusMeters * DegreesToRadians * Math.Cos(centerLat * DegreesToRadians);
        }

        public Point2d ToLocal(double lat, double lon)
        {
            return new Point2d((lon - CenterLon) * metersPerDegreeLon, (lat - CenterLat) * metersPerDegreeLat);
        }

        public void ToGeo(double x, double z, out double lat, out double lon)
        {
            lat = CenterLat + z / metersPerDegreeLat;
            lon = CenterLon + x / metersPerDegreeLon;
        }
    }
}
