using System.Text.Json;
using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Crawler;
using Didibood.LocationAccess.Application.Neshan;

namespace Didibood.LocationAccess.Infrastructure.Crawler;

public sealed class PoiNormalizer(IPoiFingerprintService fingerprintService) : IPoiNormalizer
{
    private const string PlaceCategory = "place";

    // Maps category code → set of accepted Neshan `type` values (Phase 3 findings).
    private static readonly IReadOnlyDictionary<string, HashSet<string>> TypeWhitelists =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["metro"] = ["subway_station", "metro_entrance", "train_station"],
            ["brt"] = ["bus_station", "transit_station"],
            ["bus"] = ["bus_station"],
            ["school"] = ["formal_school", "school", "tertiary"],
            ["university"] = ["university", "college"],
            ["hospital"] = ["hospital"],
            ["clinic"] = ["clinic"],
            ["pharmacy"] = ["pharmacy"],
            ["shoppingCenter"] = ["shopping_mall", "commercial_complex"],
            ["supermarket"] = ["supermarket"],
            ["park"] = ["park"],
            ["gym"] = ["gym"],
            ["bank"] = ["bank"],
            ["mosque"] = ["mosque"],
            ["governmentOffice"] = ["local_government_office", "e_government"],
        };

    // Stable mapping derived from seeded poi_categories (IDs 1-15 are fixed by migration).
    private static readonly IReadOnlyDictionary<short, string> CategoryIdToCode =
        new Dictionary<short, string>
        {
            [1] = "metro",
            [2] = "brt",
            [3] = "bus",
            [4] = "school",
            [5] = "university",
            [6] = "hospital",
            [7] = "clinic",
            [8] = "pharmacy",
            [9] = "shoppingCenter",
            [10] = "supermarket",
            [11] = "park",
            [12] = "gym",
            [13] = "bank",
            [14] = "mosque",
            [15] = "governmentOffice",
        };

    public NormalizedPoi? Normalize(NeshanSearchItem item, short categoryId)
    {
        // Filter 1: only ingest "place" items.
        if (!string.Equals(item.Category, PlaceCategory, StringComparison.OrdinalIgnoreCase))
            return null;

        // Filter 2: type must be in the whitelist for this category.
        if (!CategoryIdToCode.TryGetValue(categoryId, out var code))
            return null;

        if (!TypeWhitelists.TryGetValue(code, out var allowedTypes))
            return null;

        if (item.Type is null || !allowedTypes.Contains(item.Type))
            return null;

        var lat = item.Location.Y;
        var lng = item.Location.X;

        // Use the Neshan type as the category discriminator for the fingerprint
        // so that the same venue appearing under different types produces distinct fingerprints.
        var fingerprint = fingerprintService.ComputeFingerprint(
            item.Title,
            item.Type,
            lat,
            lng,
            item.Address);

        return new NormalizedPoi
        {
            Fingerprint = fingerprint,
            Latitude = lat,
            Longitude = lng,
            Title = item.Title,
            Address = item.Address,
            CategoryId = categoryId,
            NeshanType = item.Type,
            NeshanCategory = item.Category,
            SourcePayloadJson = JsonSerializer.Serialize(item),
        };
    }
}
