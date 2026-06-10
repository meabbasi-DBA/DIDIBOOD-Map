using H3;
using H3.Algorithms;
using H3.Extensions;
using NetTopologySuite.Geometries;

namespace Didibood.LocationAccess.Infrastructure.H3;

public static class H3CellGeometry
{
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 4326);

    public static (double Lat, double Lng) GetCentroid(long h3Index)
    {
        var index = new H3Index((ulong)h3Index);
        var coordinate = index.ToCoordinate();
        return (coordinate.Y, coordinate.X);
    }

    public static IReadOnlyList<double[]> GetBoundary(long h3Index)
    {
        var index = new H3Index((ulong)h3Index);
        var boundary = index.GetCellBoundary();
        var ring = boundary.ExteriorRing.Coordinates
            .Select(c => new[] { c.X, c.Y })
            .ToList();

        if (ring.Count > 0)
            ring.Add(ring[0]);

        return ring;
    }

    public static IReadOnlyList<long> PolyfillBounds(double minLat, double maxLat, double minLng, double maxLng, int resolution)
    {
        var coords = new[]
        {
            new Coordinate(minLng, minLat),
            new Coordinate(maxLng, minLat),
            new Coordinate(maxLng, maxLat),
            new Coordinate(minLng, maxLat),
            new Coordinate(minLng, minLat)
        };

        var polygon = GeometryFactory.CreatePolygon(coords);
        return polygon.Fill(resolution)
            .Select(i => (long)(ulong)i)
            .Distinct()
            .OrderBy(i => i)
            .ToList();
    }
}
