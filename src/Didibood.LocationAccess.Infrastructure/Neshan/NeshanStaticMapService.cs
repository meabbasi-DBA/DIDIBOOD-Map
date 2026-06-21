using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Configuration;
using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Didibood.LocationAccess.Infrastructure.Neshan;

public sealed class NeshanStaticMapService(
    HttpClient httpClient,
    IOptions<NeshanOptions> options,
    ISystemConfigurationStore configStore,
    Persistence.AppDbContext db,
    IMemoryCache cache,
    ILogger<NeshanStaticMapService> logger) : IStaticMapProvider
{
    private static readonly TimeSpan CacheSliding = TimeSpan.FromMinutes(5);

    public async Task<StaticMapResult> GetMapAsync(StaticMapRequest request, CancellationToken ct)
    {
        var cacheKey = ComputeCacheKey(request);

        // L1: memory cache
        if (cache.TryGetValue(cacheKey, out byte[]? cached) && cached is { Length: > 0 })
        {
            logger.LogDebug("Static map cache hit (L1) for key {Key}", cacheKey);
            return new StaticMapResult { ImageData = cached, CacheKey = cacheKey, FromCache = true };
        }

        // L2: database cache
        var snapshot = await db.StaticMapSnapshots
            .FirstOrDefaultAsync(s => s.CacheKey == cacheKey, ct);

        if (snapshot?.ImageData is { Length: > 0 })
        {
            logger.LogDebug("Static map cache hit (L2/DB) for key {Key}", cacheKey);
            cache.Set(cacheKey, snapshot.ImageData, new MemoryCacheEntryOptions { SlidingExpiration = CacheSliding });
            return new StaticMapResult { ImageData = snapshot.ImageData, CacheKey = cacheKey, FromCache = true };
        }

        // Fetch from Neshan API
        logger.LogInformation("Fetching static map from Neshan for key {Key}", cacheKey);
        var imageData = await FetchFromNeshanAsync(request, ct);

        // Persist to DB
        if (snapshot is null)
        {
            db.StaticMapSnapshots.Add(new StaticMapSnapshot
            {
                Id = Guid.NewGuid(),
                Latitude = (decimal)request.Latitude,
                Longitude = (decimal)request.Longitude,
                Zoom = (short)request.Zoom,
                Width = request.Width,
                Height = request.Height,
                Style = request.Style,
                Marker = request.Marker,
                CacheKey = cacheKey,
                ImageData = imageData,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            snapshot.ImageData = imageData;
        }

        await db.SaveChangesAsync(ct);

        // Store in L1
        cache.Set(cacheKey, imageData, new MemoryCacheEntryOptions { SlidingExpiration = CacheSliding });

        return new StaticMapResult { ImageData = imageData, CacheKey = cacheKey, FromCache = false };
    }

    public async Task<IReadOnlyList<StaticMapSnapshot>> GetCachedSnapshotsAsync(CancellationToken ct)
    {
        return await db.StaticMapSnapshots
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new StaticMapSnapshot
            {
                Id = s.Id,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                Zoom = s.Zoom,
                Width = s.Width,
                Height = s.Height,
                Style = s.Style,
                Marker = s.Marker,
                CacheKey = s.CacheKey,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task DeleteCachedSnapshotAsync(Guid id, CancellationToken ct)
    {
        var snapshot = await db.StaticMapSnapshots.FindAsync([id], ct);
        if (snapshot is null)
            return;

        cache.Remove(snapshot.CacheKey);
        db.StaticMapSnapshots.Remove(snapshot);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Deleted static map snapshot {Id} (key {Key})", id, snapshot.CacheKey);
    }

    private async Task<byte[]> FetchFromNeshanAsync(StaticMapRequest request, CancellationToken ct)
    {
        var apiKey = await configStore.GetAsync("neshan.LocationApiKey", options.Value.GetLocationApiKey(), ct);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new NeshanAuthenticationException("Neshan Location API key is not configured.", 480);

        var url = BuildUrl(request, apiKey);

        using var response = await httpClient.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var error = NeshanExceptionMapper.TryParseError(body);
            var code = error?.Code ?? (int)response.StatusCode;
            throw NeshanExceptionMapper.Map(code, error);
        }

        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private const string StaticMapBaseUrl = "https://api.neshan.org/v5/static";

    private static string BuildUrl(StaticMapRequest request, string apiKey)
    {
        var style = string.IsNullOrWhiteSpace(request.Style) ? "light" : request.Style;
        var sb = new StringBuilder(StaticMapBaseUrl);
        sb.Append('?');
        sb.Append("key=").Append(Uri.EscapeDataString(apiKey));
        sb.Append("&style=").Append(Uri.EscapeDataString(style));
        sb.Append("&zoom=").Append(request.Zoom);
        sb.Append("&latitude=").Append(request.Latitude.ToString(CultureInfo.InvariantCulture));
        sb.Append("&longitude=").Append(request.Longitude.ToString(CultureInfo.InvariantCulture));
        sb.Append("&width=").Append(request.Width);
        sb.Append("&height=").Append(request.Height);
        if (!string.IsNullOrWhiteSpace(request.Marker))
            sb.Append("&marker=").Append(Uri.EscapeDataString(request.Marker));
        return sb.ToString();
    }

    private static string ComputeCacheKey(StaticMapRequest req)
    {
        var raw = $"{req.Latitude:F6}|{req.Longitude:F6}|z{req.Zoom}|{req.Width}x{req.Height}|{req.Style ?? ""}|{req.Marker ?? ""}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
