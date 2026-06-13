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
        return PolyfillPolygon(polygon, resolution);
    }

    public static IReadOnlyList<long> PolyfillPolygon(Polygon polygon, int resolution)
    {
        return polygon.Fill(resolution)
            .Select(i => (long)(ulong)i)
            .Distinct()
            .OrderBy(i => i)
            .ToList();
    }

    /// <summary>
    /// Polyfills each district polygon at <paramref name="resolution"/> and keeps cells whose centroids
    /// lie inside the municipality union (no rectangular bbox bleed).
    /// </summary>
    public static IReadOnlyList<long> PolyfillMunicipality(TehranMunicipalityBoundary boundary, int resolution)
    {
        var indices = new HashSet<long>();

        foreach (var district in boundary.Districts)
        {
            foreach (var index in district.Fill(resolution).Select(i => (long)(ulong)i))
            {
                if (boundary.ContainsCentroid(index))
                    indices.Add(index);
            }
        }

        return indices.OrderBy(i => i).ToList();
    }
}
