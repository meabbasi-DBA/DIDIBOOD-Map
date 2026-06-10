using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Crawler;
using Didibood.LocationAccess.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Didibood.LocationAccess.Infrastructure.Persistence;

public sealed class PoiRepository(AppDbContext db) : IPoiRepository
{
    public async Task<bool> ExistsByFingerprintAsync(string fingerprint, CancellationToken ct = default)
    {
        return await db.Pois
            .AnyAsync(p => p.PoiFingerprint == fingerprint, ct);
    }

    public async Task UpsertAsync(NormalizedPoi poi, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var existing = await db.Pois
            .FirstOrDefaultAsync(p => p.PoiFingerprint == poi.Fingerprint, ct);

        if (existing is not null)
        {
            existing.LastSeenAt = now;
            existing.SourcePayloadJson = poi.SourcePayloadJson;
            existing.UpdatedAt = now;
        }
        else
        {
            var entity = new Poi
            {
                PoiFingerprint = poi.Fingerprint,
                Latitude = poi.Latitude,
                Longitude = poi.Longitude,
                Title = poi.Title,
                Address = poi.Address,
                CategoryId = poi.CategoryId,
                NeshanType = poi.NeshanType,
                NeshanCategory = poi.NeshanCategory,
                SourcePayloadJson = poi.SourcePayloadJson,
                FirstSeenAt = now,
                LastSeenAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            };

            db.Pois.Add(entity);

            // Set the PostGIS geography shadow property (Latitude/Longitude are ignored by EF Core).
            db.Entry(entity).Property("Location").CurrentValue =
                new Point(poi.Longitude, poi.Latitude) { SRID = 4326 };
        }

        await db.SaveChangesAsync(ct);
    }
}
