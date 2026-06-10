using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Coverage;
using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Infrastructure.H3;
using Didibood.LocationAccess.Infrastructure.Persistence;
using H3;
using H3.Extensions;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Didibood.LocationAccess.Infrastructure.Coverage;

public sealed class CoverageService(AppDbContext db) : ICoverageService
{
    public async Task<CoverageSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var counts = await db.H3CoverageCells
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Pending = g.Count(c => c.Status == "pending"),
                Success = g.Count(c => c.Status == "success"),
                Failed = g.Count(c => c.Status == "failed"),
                Stale = g.Count(c => c.Status == "stale")
            })
            .FirstOrDefaultAsync(ct);

        var total = counts?.Total ?? 0;
        var success = counts?.Success ?? 0;

        return new CoverageSummaryDto
        {
            TotalCells = total,
            PendingCells = counts?.Pending ?? 0,
            SuccessCells = success,
            FailedCells = counts?.Failed ?? 0,
            StaleCells = counts?.Stale ?? 0,
            CoveragePercent = total == 0 ? 0 : Math.Round(success * 100.0 / total, 1)
        };
    }

    public async Task<CoverageGeoJsonDto> GetCellsAsync(CoverageCellsQuery query, CancellationToken ct = default)
    {
        var cellQuery = ApplyCellFilters(db.H3CoverageCells.AsQueryable(), query);
        var limit = Math.Clamp(query.Limit, 1, 2000);

        var cells = await cellQuery
            .OrderBy(c => c.H3Index)
            .Take(limit)
            .ToListAsync(ct);

        var features = cells.Select(ToFeature).ToList();

        return new CoverageGeoJsonDto { Features = features };
    }

    public async Task<CoverageCellDetailDto?> GetCellDetailAsync(long h3Index, CancellationToken ct = default)
    {
        var cell = await db.H3CoverageCells
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.H3Index == h3Index, ct);

        if (cell is null)
            return null;

        var (lat, lng) = H3CellGeometry.GetCentroid(cell.H3Index);

        return new CoverageCellDetailDto
        {
            H3Index = cell.H3Index,
            Resolution = cell.Resolution,
            Status = cell.Status,
            PoiCount = cell.PoiCount,
            FailureCount = cell.FailureCount,
            FailureReason = cell.FailureReason,
            LastCrawlAt = cell.LastCrawlAt,
            LastSuccessAt = cell.LastSuccessAt,
            CentroidLat = lat,
            CentroidLng = lng
        };
    }

    public async Task<IReadOnlyList<HeatmapPointDto>> GetHeatmapAsync(
        CoverageHeatmapQuery query,
        CancellationToken ct = default)
    {
        if (query.CategoryId.HasValue)
            return await GetCategoryHeatmapAsync(query, ct);

        var resolution = query.Resolution ?? 8;
        var cellQuery = db.H3CoverageCells.AsQueryable()
            .Where(c => c.Resolution == resolution);

        if (!string.IsNullOrWhiteSpace(query.Status))
            cellQuery = cellQuery.Where(c => c.Status == query.Status);

        if (query.MinPoiCount is > 0)
            cellQuery = cellQuery.Where(c => c.PoiCount >= query.MinPoiCount);

        if (query.MaxAgeDays is > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-query.MaxAgeDays.Value);
            cellQuery = cellQuery.Where(c => c.LastSuccessAt == null || c.LastSuccessAt < cutoff);
        }

        var cells = await cellQuery
            .OrderByDescending(c => c.PoiCount)
            .Take(1500)
            .ToListAsync(ct);

        return cells.Select(cell =>
        {
            var (lat, lng) = H3CellGeometry.GetCentroid(cell.H3Index);
            return new HeatmapPointDto
            {
                Lat = lat,
                Lng = lng,
                Weight = Math.Max(1, cell.PoiCount),
                H3Index = cell.H3Index,
                Status = cell.Status
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<HeatmapPointDto>> GetCategoryHeatmapAsync(
        CoverageHeatmapQuery query,
        CancellationToken ct)
    {
        var resolution = query.Resolution ?? 8;
        var categoryId = query.CategoryId!.Value;

        var pois = await db.Pois
            .AsNoTracking()
            .Where(p => p.CategoryId == categoryId && p.SupersededAt == null)
            .Select(p => new { p.Latitude, p.Longitude })
            .ToListAsync(ct);

        var grouped = new Dictionary<long, int>();

        foreach (var poi in pois)
        {
            var coordinate = new Coordinate(poi.Longitude, poi.Latitude);
            var index = coordinate.ToH3Index(resolution);
            var key = (long)(ulong)index;
            grouped[key] = grouped.GetValueOrDefault(key) + 1;
        }

        return grouped
            .OrderByDescending(kv => kv.Value)
            .Take(1500)
            .Select(kv =>
            {
                var (lat, lng) = H3CellGeometry.GetCentroid(kv.Key);
                return new HeatmapPointDto
                {
                    Lat = lat,
                    Lng = lng,
                    Weight = kv.Value,
                    H3Index = kv.Key
                };
            })
            .ToList();
    }

    private static IQueryable<H3CoverageCell> ApplyCellFilters(
        IQueryable<H3CoverageCell> query,
        CoverageCellsQuery filter)
    {
        if (filter.Resolution is > 0)
            query = query.Where(c => c.Resolution == filter.Resolution);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(c => c.Status == filter.Status);

        if (filter.MaxAgeDays is > 0)
        {
            var cutoff = DateTimeOffset.UtcNow.AddDays(-filter.MaxAgeDays.Value);
            query = query.Where(c => c.LastSuccessAt == null || c.LastSuccessAt < cutoff);
        }

        if (filter.MinLat is not null && filter.MaxLat is not null &&
            filter.MinLng is not null && filter.MaxLng is not null)
        {
            var minLat = filter.MinLat.Value;
            var maxLat = filter.MaxLat.Value;
            var minLng = filter.MinLng.Value;
            var maxLng = filter.MaxLng.Value;

            var indices = H3CellGeometry.PolyfillBounds(minLat, maxLat, minLng, maxLng, filter.Resolution ?? 8);
            query = query.Where(c => indices.Contains(c.H3Index));
        }

        return query;
    }

    private static CoverageFeatureDto ToFeature(H3CoverageCell cell)
    {
        var (lat, lng) = H3CellGeometry.GetCentroid(cell.H3Index);
        var ring = H3CellGeometry.GetBoundary(cell.H3Index);

        return new CoverageFeatureDto
        {
            Properties = new CoverageFeaturePropertiesDto
            {
                H3Index = cell.H3Index,
                Status = cell.Status,
                PoiCount = cell.PoiCount,
                LastCrawlAt = cell.LastCrawlAt,
                LastSuccessAt = cell.LastSuccessAt,
                CentroidLat = lat,
                CentroidLng = lng
            },
            Geometry = new CoveragePolygonGeometryDto
            {
                Coordinates = [ring]
            }
        };
    }
}
