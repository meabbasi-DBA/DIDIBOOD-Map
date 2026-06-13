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
        var resolution = query.Resolution ?? await GetActiveGridResolutionAsync(ct);
        var municipalityMode = await IsMunicipalityModeAsync(ct);
        var cellQuery = ApplyCellFilters(db.H3CoverageCells.AsQueryable(), query, resolution, municipalityMode);

        if (municipalityMode)
            cellQuery = cellQuery.Where(c => c.MunicipalityMode);

        var limit = Math.Clamp(query.Limit, 1, 5000);
        var rows = await cellQuery
            .OrderBy(c => c.H3Index)
            .Take(limit)
            .Select(c => new
            {
                Cell = c,
                Centroid = EF.Property<Point>(c, "Centroid")
            })
            .ToListAsync(ct);

        return new CoverageGeoJsonDto
        {
            Features = rows.Select(r => ToFeature(r.Cell, r.Centroid)).ToList()
        };
    }

    public Task<CoverageBoundaryGeoJsonDto> GetBoundaryAsync(CancellationToken ct = default)
    {
        var boundary = LoadMunicipalityBoundary();
        return Task.FromResult(boundary.ToGeoJson());
    }

    public async Task<CoverageDebugDto> GetDebugAsync(CancellationToken ct = default)
    {
        var cells = await db.H3CoverageCells.AsNoTracking().ToListAsync(ct);
        var boundary = LoadMunicipalityBoundary();
        var sourceMode = await GetBoundarySourceModeAsync(ct);
        var bounds = await LoadTehranBoundsAsync(ct);
        var searchRadius = await GetSearchRadiusAsync(ct);
        var searchTerms = await CountSearchTermsAsync(ct);

        var resolution = cells.Select(c => c.Resolution).Distinct().ToList() switch
        {
            [var only] => only,
            _ => (short)7
        };

        var baseCells = cells.Where(c => !c.IsRefined).ToList();
        var virtualCells = cells.Where(c => c.IsRefined).ToList();
        var centerCoords = await LoadSearchCenterCoordsAsync(cells, ct);
        var outside = centerCoords.Count(pt => !boundary.ContainsPoint(pt.Lat, pt.Lng));

        var expectedMunicipality = H3CellGeometry.PolyfillMunicipality(boundary, resolution).Count;
        var legacyBbox = bounds is null
            ? 0
            : H3CellGeometry.PolyfillBounds(bounds.MinLat, bounds.MaxLat, bounds.MinLng, bounds.MaxLng, resolution).Count;

        var municipalityCells = cells.Count(c => c.MunicipalityMode);
        var success = cells.Count(c => c.Status == "success");
        var total = cells.Count;

        double minLat = 0, maxLat = 0, minLng = 0, maxLng = 0;
        if (centerCoords.Count > 0)
        {
            minLat = centerCoords.Min(c => c.Lat);
            maxLat = centerCoords.Max(c => c.Lat);
            minLng = centerCoords.Min(c => c.Lng);
            maxLng = centerCoords.Max(c => c.Lng);
        }

        var spatial = H3CoverageMetrics.Analyze(boundary, centerCoords, searchRadius);
        var scenarios = H3CoverageMetrics.CompareScenarios(boundary, searchTerms, searchRadius)
            .Select(s => new GridScenarioComparisonDto
            {
                Scenario = s.Scenario,
                CellCount = s.CellCount,
                SearchCenterCount = s.SearchCenterCount,
                EstimatedRequests = s.EstimatedRequests,
                EstimatedCoveragePercent = s.EstimatedCoveragePercent,
                OverlapPercent = s.OverlapPercent,
                SearchRadiusMeters = s.SearchRadiusMeters
            })
            .ToList();

        return new CoverageDebugDto
        {
            TotalCells = total,
            BaseCells = baseCells.Count,
            VirtualCenters = virtualCells.Count,
            Resolution = resolution,
            SearchRadiusMeters = searchRadius,
            CityAreaKm2 = Math.Round(boundary.ApproximateAreaSqKm(), 1),
            H3AreaKm2 = Math.Round(baseCells.Count * 5.16, 1),
            CoveragePercent = total == 0 ? 0 : Math.Round(success * 100.0 / total, 1),
            EstimatedCoveragePercent = spatial.EstimatedCoveragePercent,
            AverageCenterSpacingMeters = spatial.AverageCenterSpacingMeters,
            UncoveredAreaKm2 = spatial.UncoveredAreaKm2,
            OverlapRatio = spatial.OverlapRatio,
            RecommendedSearchRadiusMeters = spatial.RecommendedSearchRadiusMeters,
            SourceMode = sourceMode,
            MunicipalityModeCells = municipalityCells,
            LegacyBboxCells = total - municipalityCells,
            CellsOutsideMunicipality = outside,
            ExpectedMunicipalityCells = expectedMunicipality,
            LegacyBboxPolyfillCells = legacyBbox,
            EnvelopeLooksRectangular = baseCells.Count > 0 && baseCells.Count >= legacyBbox * 0.85,
            CentroidBounds = new CoverageCentroidBoundsDto
            {
                MinLat = minLat,
                MaxLat = maxLat,
                MinLng = minLng,
                MaxLng = maxLng
            },
            ScenarioComparison = scenarios
        };
    }

    public async Task<CoverageRefinementDebugDto> GetRefinementDebugAsync(CancellationToken ct = default)
    {
        var persistedRefined = await db.H3CoverageCells.CountAsync(c => c.IsRefined, ct);
        var sourceMode = await GetBoundarySourceModeAsync(ct);
        var searchRadius = await GetSearchRadiusAsync(ct);
        var refinementEnabled = await GetConfigBoolAsync(H3BoundaryRefinementPlanner.RefinementConfigKey, true, ct);
        var searchTerms = await CountSearchTermsAsync(ct);

        var boundary = LoadMunicipalityBoundary();
        var baseCells = H3CellGeometry.PolyfillMunicipality(boundary, 7);
        var refinement = H3BoundaryRefinementPlanner.PlanSearchCentersWithDiagnostics(
            baseCells,
            boundary,
            searchRadius,
            searchTerms,
            enableRefinement: refinementEnabled);

        return new CoverageRefinementDebugDto
        {
            BaseCells = refinement.BaseCellCount,
            CandidateCells = refinement.CandidateCellCount,
            InsertedRefinedCells = refinement.SelectedRefinedCount,
            PersistedRefinedCells = persistedRefined,
            SkippedCells = refinement.SkippedByBudgetCount + refinement.SkippedByThresholdCount,
            RejectedCells = refinement.RejectedBySeparationCount,
            RefinementEnabled = refinement.RefinementEnabled,
            DisabledReason = refinement.DisabledReason,
            MaxVirtualBudget = refinement.MaxVirtualBudget,
            SearchRadiusMeters = searchRadius,
            SourceMode = sourceMode
        };
    }

    public async Task<CoverageCellDetailDto?> GetCellDetailAsync(long h3Index, CancellationToken ct = default)
    {
        var row = await db.H3CoverageCells
            .AsNoTracking()
            .Where(c => c.H3Index == h3Index)
            .Select(c => new
            {
                Cell = c,
                Centroid = EF.Property<Point>(c, "Centroid")
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
            return null;

        var (lat, lng) = ResolveCenterCoords(row.Cell, row.Centroid);

        return new CoverageCellDetailDto
        {
            H3Index = row.Cell.H3Index,
            Resolution = row.Cell.Resolution,
            Status = row.Cell.Status,
            PoiCount = row.Cell.PoiCount,
            FailureCount = row.Cell.FailureCount,
            FailureReason = row.Cell.FailureReason,
            LastCrawlAt = row.Cell.LastCrawlAt,
            LastSuccessAt = row.Cell.LastSuccessAt,
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

        var resolution = query.Resolution ?? await GetActiveGridResolutionAsync(ct);
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
        var resolution = query.Resolution ?? await GetActiveGridResolutionAsync(ct);
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

    private async Task<short> GetActiveGridResolutionAsync(CancellationToken ct)
    {
        var resolutions = await db.H3CoverageCells
            .AsNoTracking()
            .Select(c => c.Resolution)
            .Distinct()
            .ToListAsync(ct);

        if (resolutions.Count == 1)
            return resolutions[0];

        return 7;
    }

    private static TehranMunicipalityBoundary LoadMunicipalityBoundary() =>
        TehranMunicipalityBoundary.LoadFromFile(TehranMunicipalityBoundary.ResolveDefaultPath());

    private async Task<string> GetBoundarySourceModeAsync(CancellationToken ct)
    {
        var mode = await db.SystemConfigurations
            .AsNoTracking()
            .Where(c => c.ConfigKey == "tehran.boundary.mode")
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(mode) ? "municipality (default, not persisted)" : mode;
    }

    private async Task<TehranBoundsDto?> LoadTehranBoundsAsync(CancellationToken ct)
    {
        var json = await db.SystemConfigurations
            .AsNoTracking()
            .Where(c => c.ConfigKey == "tehran.bounds")
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(json))
            return null;

        return System.Text.Json.JsonSerializer.Deserialize<TehranBoundsDto>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private sealed class TehranBoundsDto
    {
        public double MinLat { get; set; }
        public double MaxLat { get; set; }
        public double MinLng { get; set; }
        public double MaxLng { get; set; }
    }

    private async Task<bool> IsMunicipalityModeAsync(CancellationToken ct)
    {
        var mode = await db.SystemConfigurations
            .AsNoTracking()
            .Where(c => c.ConfigKey == "tehran.boundary.mode")
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(mode)
               || mode.Equals("municipality", StringComparison.OrdinalIgnoreCase);
    }

    private static IQueryable<H3CoverageCell> ApplyCellFilters(
        IQueryable<H3CoverageCell> query,
        CoverageCellsQuery filter,
        short resolution,
        bool municipalityMode)
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

        // Legacy bbox filter — disabled for municipality grids (cells already clipped to boundary).
        if (!municipalityMode
            && filter.MinLat is not null && filter.MaxLat is not null
            && filter.MinLng is not null && filter.MaxLng is not null)
        {
            var indices = H3CellGeometry.PolyfillBounds(
                filter.MinLat.Value, filter.MaxLat.Value,
                filter.MinLng.Value, filter.MaxLng.Value,
                resolution);
            query = query.Where(c => indices.Contains(c.H3Index));
        }

        return query;
    }

    private static CoverageFeatureDto ToFeature(H3CoverageCell cell, Point? centroid)
    {
        var (lat, lng) = ResolveCenterCoords(cell, centroid);

        if (cell.IsRefined || H3VirtualCenterId.IsVirtual(cell.H3Index))
        {
            return new CoverageFeatureDto
            {
                Properties = new CoverageFeaturePropertiesDto
                {
                    H3Index = cell.H3Index,
                    ParentH3Index = cell.ParentH3Index,
                    IsRefined = true,
                    Status = cell.Status,
                    PoiCount = cell.PoiCount,
                    LastCrawlAt = cell.LastCrawlAt,
                    LastSuccessAt = cell.LastSuccessAt,
                    CentroidLat = lat,
                    CentroidLng = lng,
                    MunicipalityMode = cell.MunicipalityMode
                },
                Geometry = new CoveragePointGeometryDto
                {
                    Coordinates = [lng, lat]
                }
            };
        }

        var ring = H3CellGeometry.GetBoundary(cell.H3Index);
        return new CoverageFeatureDto
        {
            Properties = new CoverageFeaturePropertiesDto
            {
                H3Index = cell.H3Index,
                ParentH3Index = cell.ParentH3Index,
                IsRefined = false,
                Status = cell.Status,
                PoiCount = cell.PoiCount,
                LastCrawlAt = cell.LastCrawlAt,
                LastSuccessAt = cell.LastSuccessAt,
                CentroidLat = lat,
                CentroidLng = lng,
                MunicipalityMode = cell.MunicipalityMode
            },
            Geometry = new CoveragePolygonGeometryDto
            {
                Coordinates = [ring]
            }
        };
    }

    private static (double Lat, double Lng) ResolveCenterCoords(H3CoverageCell cell, Point? centroid)
    {
        if ((cell.IsRefined || H3VirtualCenterId.IsVirtual(cell.H3Index)) && centroid is not null)
            return (centroid.Y, centroid.X);

        return H3CellGeometry.GetCentroid(cell.H3Index);
    }

    private async Task<List<(double Lat, double Lng)>> LoadSearchCenterCoordsAsync(
        IReadOnlyList<H3CoverageCell> cells,
        CancellationToken ct)
    {
        if (cells.Count == 0)
            return [];

        var ids = cells.Select(c => c.H3Index).ToList();
        var rows = await db.H3CoverageCells
            .AsNoTracking()
            .Where(c => ids.Contains(c.H3Index))
            .Select(c => new
            {
                c.H3Index,
                c.IsRefined,
                Centroid = EF.Property<Point>(c, "Centroid")
            })
            .ToListAsync(ct);

        return rows
            .Select(r => ResolveCenterCoords(
                new H3CoverageCell { H3Index = r.H3Index, IsRefined = r.IsRefined },
                r.Centroid))
            .ToList();
    }

    private async Task<int> GetSearchRadiusAsync(CancellationToken ct)
    {
        var value = await db.SystemConfigurations
            .AsNoTracking()
            .Where(c => c.ConfigKey == "search.radius.default_meters")
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        return int.TryParse(value, out var parsed) ? parsed : H3GridPlanner.DefaultSearchRadiusMeters;
    }

    private async Task<int> CountSearchTermsAsync(CancellationToken ct)
    {
        var jsonList = await db.PoiCategories
            .Where(c => c.IsEnabled)
            .Select(c => c.SearchTermsJson)
            .ToListAsync(ct);

        var total = 0;
        foreach (var json in jsonList)
        {
            var terms = System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? [];
            total += terms.Length;
        }

        return total > 0 ? total : H3GridPlanner.DefaultSearchTermsPerCrawl;
    }

    private async Task<bool> GetConfigBoolAsync(string key, bool defaultValue, CancellationToken ct)
    {
        var value = await db.SystemConfigurations
            .AsNoTracking()
            .Where(c => c.ConfigKey == key)
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }
}
