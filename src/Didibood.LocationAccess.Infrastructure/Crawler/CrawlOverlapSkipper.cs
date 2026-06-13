using Didibood.LocationAccess.Application.Crawler;
using Didibood.LocationAccess.Infrastructure.H3;

namespace Didibood.LocationAccess.Infrastructure.Crawler;

/// <summary>
/// Skips crawl tasks when neighboring successful cells already cover the area (2000 m overlap).
/// </summary>
public static class CrawlOverlapSkipper
{
    public const int SearchRadiusMeters = 2000;
    public const double OverlapSkipThreshold = 0.85;

    public static OverlapSkipDecision Evaluate(
        long targetH3Index,
        IReadOnlyList<(long H3Index, double Lat, double Lng, int PoiCount, string Status)> neighbors)
    {
        var (lat, lng) = H3CellGeometry.GetCentroid(targetH3Index);
        var successfulNeighbors = neighbors
            .Where(n => n.Status == "success" && n.H3Index != targetH3Index)
            .Where(n => HaversineMeters(lat, lng, n.Lat, n.Lng) <= SearchRadiusMeters)
            .ToList();

        if (successfulNeighbors.Count == 0)
            return new OverlapSkipDecision { ShouldSkip = false };

        var overlapScore = Math.Min(1.0, successfulNeighbors.Count / 3.0);
        if (overlapScore < OverlapSkipThreshold)
            return new OverlapSkipDecision { ShouldSkip = false };

        return new OverlapSkipDecision
        {
            ShouldSkip = true,
            Reason = $"Covered by {successfulNeighbors.Count} successful neighbor(s) within {SearchRadiusMeters}m",
            ReusedPoiCount = successfulNeighbors.Sum(n => n.PoiCount)
        };
    }

    private static double HaversineMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double r = 6_371_000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
