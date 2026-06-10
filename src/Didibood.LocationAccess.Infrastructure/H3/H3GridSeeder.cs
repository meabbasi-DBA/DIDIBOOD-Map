using System.Text.Json;
using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Didibood.LocationAccess.Infrastructure.H3;

internal static class H3GridSeeder
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static async Task SeedTehranGridAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.H3CoverageCells.AnyAsync(ct))
            return;

        var boundsJson = await db.SystemConfigurations
            .Where(c => c.ConfigKey == "tehran.bounds")
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(boundsJson))
            return;

        var bounds = JsonSerializer.Deserialize<TehranBounds>(boundsJson, JsonOpts);
        if (bounds is null)
            return;

        const short resolution = 8;
        var indexes = H3CellGeometry.PolyfillBounds(
            bounds.MinLat, bounds.MaxLat, bounds.MinLng, bounds.MaxLng, resolution);

        var now = DateTimeOffset.UtcNow;
        var cells = new List<H3CoverageCell>(indexes.Count);

        foreach (var h3Index in indexes)
        {
            var (lat, lng) = H3CellGeometry.GetCentroid(h3Index);
            var cell = new H3CoverageCell
            {
                H3Index = h3Index,
                Resolution = resolution,
                Status = "pending",
                LastCrawlAt = null,
                LastSuccessAt = null,
                PoiCount = 0,
                FailureCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            cells.Add(cell);
            db.H3CoverageCells.Add(cell);
            db.Entry(cell).Property("Centroid").CurrentValue =
                new Point(lng, lat) { SRID = 4326 };
        }

        await db.SaveChangesAsync(ct);
    }

    private sealed class TehranBounds
    {
        public double MinLat { get; set; }
        public double MaxLat { get; set; }
        public double MinLng { get; set; }
        public double MaxLng { get; set; }
    }
}
