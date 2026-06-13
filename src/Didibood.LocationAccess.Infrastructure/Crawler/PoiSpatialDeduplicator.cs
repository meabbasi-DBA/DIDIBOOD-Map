using System.Text.RegularExpressions;
using Didibood.LocationAccess.Application.Crawler;

namespace Didibood.LocationAccess.Infrastructure.Crawler;

/// <summary>
/// Multi-stage dedup: fingerprint (primary) + spatial+title fallback (&lt;50 m, same normalized name).
/// </summary>
public static partial class PoiSpatialDeduplicator
{
    public const double SpatialDuplicateMeters = 50;

    public static SpatialDedupMatch? FindSpatialDuplicate(
        string title,
        double latitude,
        double longitude,
        IReadOnlyList<(Guid Id, string Title, double Lat, double Lng)> candidates)
    {
        var normalized = NormalizeTitle(title);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        foreach (var candidate in candidates)
        {
            if (!string.Equals(NormalizeTitle(candidate.Title), normalized, StringComparison.Ordinal))
                continue;

            var distance = HaversineMeters(latitude, longitude, candidate.Lat, candidate.Lng);
            if (distance <= SpatialDuplicateMeters)
            {
                return new SpatialDedupMatch
                {
                    ExistingPoiId = candidate.Id,
                    DistanceMeters = distance,
                    NormalizedTitle = normalized
                };
            }
        }

        return null;
    }

    public static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "";

        var t = title.Trim().ToLowerInvariant();
        t = WhitespaceRegex().Replace(t, " ");
        t = PunctuationRegex().Replace(t, "");
        return t;
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

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[.,\-_/\\()]+")]
    private static partial Regex PunctuationRegex();
}
