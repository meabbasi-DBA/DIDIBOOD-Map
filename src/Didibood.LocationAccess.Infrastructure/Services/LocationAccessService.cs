using System.Security.Cryptography;
using System.Text;
using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Configuration;
using Didibood.LocationAccess.Application.LocationAccess;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Didibood.LocationAccess.Infrastructure.Services;

public sealed class LocationAccessService(
    AppDbContext db,
    IOptions<NeshanOptions> options,
    ISystemConfigurationStore configStore,
    IMemoryCache cache) : ILocationAccessService
{
    private static readonly TimeSpan CacheSliding = TimeSpan.FromMinutes(5);

    public async Task<LocationAccessResponse> GetNearbyAsync(
        LocationAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = ComputeCacheKey(request);

        if (cache.TryGetValue(cacheKey, out LocationAccessResponse? cached) && cached is not null)
            return cached;

        var maxPerCategory = await configStore.GetAsync(
            "search.max_results_per_category",
            options.Value.MaxResultsPerCategory,
            cancellationToken);

        const string sql = """
            SELECT
                p.id                                                        AS "Id",
                p.title                                                     AS "Title",
                p.address                                                   AS "Address",
                ST_Y(p.location::geometry)                                  AS "Latitude",
                ST_X(p.location::geometry)                                  AS "Longitude",
                c.code                                                      AS "CategoryCode",
                ST_Distance(
                    p.location,
                    ST_SetSRID(ST_MakePoint(@p_lng, @p_lat), 4326)::geography
                )                                                           AS "DistanceMeters"
            FROM pois p
            INNER JOIN poi_categories c ON c.id = p.category_id
            WHERE p.superseded_at IS NULL
              AND c.is_enabled = TRUE
              AND ST_DWithin(
                    p.location,
                    ST_SetSRID(ST_MakePoint(@p_lng, @p_lat), 4326)::geography,
                    @p_radius
              )
            ORDER BY c.code, "DistanceMeters"
            """;

        var rows = await db.Database
            .SqlQueryRaw<NearbyPoiRow>(
                sql,
                new Npgsql.NpgsqlParameter("p_lat", request.Latitude),
                new Npgsql.NpgsqlParameter("p_lng", request.Longitude),
                new Npgsql.NpgsqlParameter("p_radius", request.Radius))
            .ToListAsync(cancellationToken);

        var grouped = rows
            .GroupBy(r => r.CategoryCode)
            .ToDictionary(
                g => ToCamelCase(g.Key),
                g => g.Take(maxPerCategory).Select(r => new PoiResultDto
                {
                    Id = r.Id,
                    Title = r.Title,
                    Address = r.Address,
                    Latitude = r.Latitude,
                    Longitude = r.Longitude,
                    DistanceMeters = r.DistanceMeters
                }).ToList());

        var response = new LocationAccessResponse { Categories = grouped };

        cache.Set(cacheKey, response, new MemoryCacheEntryOptions { SlidingExpiration = CacheSliding });

        return response;
    }

    private static string ComputeCacheKey(LocationAccessRequest req)
    {
        var raw = $"{req.Latitude:F4}|{req.Longitude:F4}|{req.Radius}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return $"loc:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string ToCamelCase(string code) =>
        string.IsNullOrEmpty(code) ? code : char.ToLowerInvariant(code[0]) + code[1..];

    private sealed class NearbyPoiRow
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string? Address { get; init; }
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public string CategoryCode { get; init; } = string.Empty;
        public double DistanceMeters { get; init; }
    }
}
