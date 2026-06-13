using System.Text.Json;
using Didibood.LocationAccess.Application.Coverage;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;

namespace Didibood.LocationAccess.Infrastructure.H3;

/// <summary>
/// Tehran municipality boundary from 22 municipal districts (OSM-derived GeoJSON).
/// Source: data/tehran-areas-22.geojson
/// </summary>
public sealed class TehranMunicipalityBoundary
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    public IReadOnlyList<Polygon> Districts { get; }
    public Geometry Union { get; }
    public Envelope Envelope { get; }

    private TehranMunicipalityBoundary(IReadOnlyList<Polygon> districts, Geometry union)
    {
        Districts = districts;
        Union = union;
        Envelope = union.EnvelopeInternal;
    }

    public static TehranMunicipalityBoundary LoadFromGeoJson(string json)
    {
        var districts = new List<Polygon>();
        using var doc = JsonDocument.Parse(json);
        foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
        {
            var geometry = feature.GetProperty("geometry");
            var type = geometry.GetProperty("type").GetString();
            switch (type)
            {
                case "Polygon":
                    districts.Add(ParsePolygon(geometry.GetProperty("coordinates")));
                    break;
                case "MultiPolygon":
                    foreach (var poly in geometry.GetProperty("coordinates").EnumerateArray())
                        districts.Add(ParsePolygon(poly));
                    break;
            }
        }

        if (districts.Count == 0)
            throw new InvalidOperationException("Tehran municipality GeoJSON contains no polygon features.");

        var union = CascadedPolygonUnion.Union(districts.Cast<Geometry>().ToArray());
        return new TehranMunicipalityBoundary(districts, union);
    }

    public static TehranMunicipalityBoundary LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Tehran municipality boundary not found: {path}", path);

        return LoadFromGeoJson(File.ReadAllText(path));
    }

    public static string ResolveDefaultPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "data", "tehran-areas-22.geojson"),
            Path.Combine(Directory.GetCurrentDirectory(), "data", "tehran-areas-22.geojson"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "tehran-areas-22.geojson"))
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return candidates[0];
    }

    public bool ContainsCentroid(long h3Index)
    {
        var (lat, lng) = H3CellGeometry.GetCentroid(h3Index);
        return ContainsPoint(lat, lng);
    }

    public bool ContainsPoint(double lat, double lng)
    {
        var point = Factory.CreatePoint(new Coordinate(lng, lat));
        return Union.Contains(point);
    }

    public double ApproximateAreaSqKm()
    {
        // Shoelace on union envelope mid-latitude — adequate for debug metrics.
        var env = Envelope;
        var latMid = (env.MinY + env.MaxY) / 2 * Math.PI / 180;
        const double kmPerDegLat = 111.32;
        var kmPerDegLng = 111.32 * Math.Cos(latMid);
        return (env.MaxY - env.MinY) * kmPerDegLat * (env.MaxX - env.MinX) * kmPerDegLng;
    }

    public CoverageBoundaryGeoJsonDto ToGeoJson()
    {
        var features = Districts.Select((poly, i) => new CoverageBoundaryFeatureDto
        {
            Properties = new Dictionary<string, object> { ["districtIndex"] = i + 1 },
            Geometry = new CoveragePolygonGeometryDto
            {
                Coordinates = [PolygonToRing(poly)]
            }
        }).ToList();

        return new CoverageBoundaryGeoJsonDto { Features = features };
    }

    private static Polygon ParsePolygon(JsonElement coordinatesElement)
    {
        var ring = coordinatesElement[0];
        var coords = new Coordinate[ring.GetArrayLength()];
        var i = 0;
        foreach (var pair in ring.EnumerateArray())
        {
            var arr = pair.EnumerateArray().ToArray();
            coords[i++] = new Coordinate(arr[0].GetDouble(), arr[1].GetDouble());
        }

        return Factory.CreatePolygon(coords);
    }

    private static IReadOnlyList<double[]> PolygonToRing(Polygon poly)
    {
        var ring = poly.ExteriorRing.Coordinates
            .Select(c => new[] { c.X, c.Y })
            .ToList();
        if (ring.Count > 0)
            ring.Add(ring[0]);
        return ring;
    }
}
