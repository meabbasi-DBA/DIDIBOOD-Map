using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.DataQuality;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Infrastructure.DataQuality;

public sealed class DataQualityService(
    AppDbContext db,
    INeshanSearchClient searchClient,
    IPoiFingerprintService fingerprintService) : IDataQualityService
{
    public async Task<DataQualityCompareResult> CompareAsync(
        DataQualityCompareRequest request,
        CancellationToken ct = default)
    {
        var liveResponse = await searchClient.SearchAsync(
            request.SearchTerm,
            request.Latitude,
            request.Longitude,
            ct);

        var liveRows = liveResponse.Items
            .Select(item =>
            {
                var fp = fingerprintService.ComputeFingerprint(
                    item.Title,
                    item.Category,
                    item.Location.Y,
                    item.Location.X,
                    item.Address);

                return new DataQualityPoiRowDto
                {
                    Title = item.Title,
                    Address = item.Address,
                    Latitude = item.Location.Y,
                    Longitude = item.Location.X,
                    Fingerprint = fp,
                    Source = "live"
                };
            })
            .ToList();

        const string sql = """
            SELECT
                p.id            AS "Id",
                p.title         AS "Title",
                p.address       AS "Address",
                ST_Y(p.location::geometry) AS "Latitude",
                ST_X(p.location::geometry) AS "Longitude",
                p.poi_fingerprint AS "Fingerprint"
            FROM pois p
            WHERE p.superseded_at IS NULL
              AND ST_DWithin(
                    p.location,
                    ST_SetSRID(ST_MakePoint(@p_lng, @p_lat), 4326)::geography,
                    @p_radius
              )
            ORDER BY p.title
            """;

        var dbRows = await db.Database
            .SqlQueryRaw<DbPoiRow>(
                sql,
                new Npgsql.NpgsqlParameter("p_lat", request.Latitude),
                new Npgsql.NpgsqlParameter("p_lng", request.Longitude),
                new Npgsql.NpgsqlParameter("p_radius", request.RadiusMeters))
            .ToListAsync(ct);

        var dbDtos = dbRows.Select(r => new DataQualityPoiRowDto
        {
            Id = r.Id,
            Title = r.Title,
            Address = r.Address,
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            Fingerprint = r.Fingerprint,
            Source = "database"
        }).ToList();

        var liveFps = liveRows.Where(r => r.Fingerprint is not null).Select(r => r.Fingerprint!).ToHashSet();
        var dbFps = dbDtos.Where(r => r.Fingerprint is not null).Select(r => r.Fingerprint!).ToHashSet();

        var matched = liveFps.Intersect(dbFps).Count();
        var liveOnly = liveRows.Where(r => r.Fingerprint is not null && !dbFps.Contains(r.Fingerprint)).ToList();
        var dbOnly = dbDtos.Where(r => r.Fingerprint is not null && !liveFps.Contains(r.Fingerprint)).ToList();

        var mismatches = new List<DataQualityMismatchDto>();
        mismatches.AddRange(liveOnly.Select(r => new DataQualityMismatchDto
        {
            Kind = "live-only",
            Title = r.Title,
            Address = r.Address,
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            Fingerprint = r.Fingerprint
        }));
        mismatches.AddRange(dbOnly.Select(r => new DataQualityMismatchDto
        {
            Kind = "db-only",
            Title = r.Title,
            Address = r.Address,
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            Fingerprint = r.Fingerprint
        }));

        return new DataQualityCompareResult
        {
            LiveCount = liveRows.Count,
            DbCount = dbDtos.Count,
            MatchedCount = matched,
            LiveOnlyCount = liveOnly.Count,
            DbOnlyCount = dbOnly.Count,
            LiveResults = liveRows,
            DbResults = dbDtos,
            Mismatches = mismatches
        };
    }

    public async Task<DataQualityPoiDetailDto?> GetPoiDetailAsync(Guid poiId, CancellationToken ct = default)
    {
        var poi = await db.Pois
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == poiId, ct);

        if (poi is null)
            return null;

        return new DataQualityPoiDetailDto
        {
            Id = poi.Id,
            Title = poi.Title,
            Address = poi.Address,
            Latitude = poi.Latitude,
            Longitude = poi.Longitude,
            Fingerprint = poi.PoiFingerprint,
            CategoryCode = poi.Category.Code,
            SourcePayloadJson = poi.SourcePayloadJson,
            FirstSeenAt = poi.FirstSeenAt,
            LastSeenAt = poi.LastSeenAt
        };
    }

    private sealed class DbPoiRow
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Address { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public string Fingerprint { get; init; } = string.Empty;
    }
}
