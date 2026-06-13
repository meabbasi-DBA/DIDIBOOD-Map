using System.Text.Json;
using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace Didibood.LocationAccess.Infrastructure.H3;

internal static class H3GridSeeder
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static async Task SeedTehranGridAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.H3CoverageCells.AnyAsync(ct))
            return;

        await ReseedTehranGridAsync(db, ct: ct);
    }

    /// <summary>
    /// Rebuilds the Tehran H3 grid from <c>tehran.bounds</c>, <c>search.radius.default_meters</c>,
    /// and optional <c>crawl.h3_resolution</c> (auto = coarsest overlap-safe resolution).
    /// </summary>
    public static async Task ReseedTehranGridAsync(
        AppDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        var bounds = await LoadBoundsAsync(db, ct);
        if (bounds is null)
            return;

        var plan = await PlanGridAsync(db, bounds, ct);
        var refinement = plan.Refinement;

        if (plan.BoundaryMode == "municipality")
        {
            var boundary = TehranMunicipalityBoundary.LoadFromFile(TehranMunicipalityBoundary.ResolveDefaultPath());
            if (!H3GridPlanner.ValidateOverlapInsideBoundary(plan.CellIndexes, boundary, plan.SearchRadiusMeters))
            {
                logger?.LogWarning(
                    "Planned municipality grid ({CellCount} cells at res {Resolution}) failed interior overlap at {Radius}m",
                    plan.CellIndexes.Count, plan.StorageResolution, plan.SearchRadiusMeters);
            }
        }
        else if (!H3GridPlanner.ValidateOverlap(
                     plan.CellIndexes, plan.SearchRadiusMeters,
                     bounds.MinLat, bounds.MaxLat, bounds.MinLng, bounds.MaxLng))
        {
            logger?.LogWarning(
                "Planned H3 grid ({CellCount} cells at res {Resolution}) failed overlap validation for radius {Radius}m",
                plan.CellIndexes.Count, plan.StorageResolution, plan.SearchRadiusMeters);
        }

        var configuredRadius = await GetConfigIntAsync(db, "search.radius.default_meters", 2000, ct);
        if (plan.SearchRadiusMeters != configuredRadius)
            await UpdateConfigIntAsync(db, "search.radius.default_meters", plan.SearchRadiusMeters, ct);

        await db.H3CoverageCells.ExecuteDeleteAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var centers = plan.SearchCenters.Count > 0
            ? plan.SearchCenters
            : plan.CellIndexes.Select(h3 =>
            {
                var (lat, lng) = H3CellGeometry.GetCentroid(h3);
                return new H3SearchCenter
                {
                    H3Index = h3,
                    Lat = lat,
                    Lng = lng,
                    IsRefined = false,
                    Resolution = plan.StorageResolution
                };
            }).ToList();

        var refinedInserted = 0;
        foreach (var center in centers)
        {
            var cell = new H3CoverageCell
            {
                H3Index = center.H3Index,
                Resolution = center.Resolution,
                ParentH3Index = center.ParentH3Index,
                IsRefined = center.IsRefined,
                MunicipalityMode = plan.BoundaryMode == "municipality",
                Status = "pending",
                LastCrawlAt = null,
                LastSuccessAt = null,
                PoiCount = 0,
                RequestCount = 0,
                FailureCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.H3CoverageCells.Add(cell);
            db.Entry(cell).Property("Centroid").CurrentValue =
                new Point(center.Lng, center.Lat) { SRID = 4326 };

            if (center.IsRefined)
                refinedInserted++;
        }

        await db.SaveChangesAsync(ct);

        if (refinement?.RefinementEnabled == true && refinedInserted == 0)
        {
            logger?.LogError(
                "Boundary refinement enabled but zero refined cells were persisted. Candidates={Candidates}, DisabledReason={Reason}",
                refinement.CandidateCellCount,
                refinement.DisabledReason ?? "none");
        }

        await db.CrawlJobs
            .ExecuteUpdateAsync(
                s => s.SetProperty(j => j.H3Resolution, plan.StorageResolution),
                ct);

        await UpdateConfigStringAsync(db, "tehran.boundary.mode", plan.BoundaryMode, ct);
        await UpdateConfigStringAsync(db, "grid.boundary.source", plan.BoundaryMode, ct);

        logger?.LogInformation(
            "Tehran H3 grid reseeded ({BoundaryMode}): base cells generated={BaseCells}, refinement candidates={Candidates}, " +
            "refined cells inserted={RefinedInserted}, rejected={Rejected}, skipped={Skipped}, resolution={Resolution}, " +
            "radius={Radius}m, ~{Requests} requests/crawl, est. coverage={Coverage}%",
            plan.BoundaryMode,
            plan.BaseCellCount > 0 ? plan.BaseCellCount : plan.CellIndexes.Count,
            refinement?.CandidateCellCount ?? 0,
            refinedInserted,
            refinement?.RejectedBySeparationCount ?? 0,
            (refinement?.SkippedByBudgetCount ?? 0) + (refinement?.SkippedByThresholdCount ?? 0),
            plan.StorageResolution,
            plan.SearchRadiusMeters,
            plan.EstimatedRequestsPerCrawl,
            plan.SpatialMetrics?.EstimatedCoveragePercent ?? 0);
    }

    public static async Task EnsureTargetGridAsync(
        AppDbContext db,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        var bounds = await LoadBoundsAsync(db, ct);
        if (bounds is null)
            return;

        var configuredRadius = await GetConfigIntAsync(db, "search.radius.default_meters", 2000, ct);
        var fixedResolution = await GetOptionalResolutionAsync(db, ct);
        var searchTermsPerCrawl = await CountSearchTermsPerCrawlAsync(db, ct);
        var expected = await PlanGridAsync(db, bounds, ct);

        var currentCount = await db.H3CoverageCells.CountAsync(ct);
        if (currentCount == 0)
        {
            await ReseedTehranGridAsync(db, logger, ct);
            return;
        }

        var currentResolution = await db.H3CoverageCells
            .Select(c => c.Resolution)
            .Distinct()
            .ToListAsync(ct);

        var resolutionMatches = currentResolution.Count == 1
                                && currentResolution[0] == expected.StorageResolution;
        var expectedCenterCount = expected.SearchCenters.Count > 0
            ? expected.SearchCenters.Count
            : expected.CellIndexes.Count;
        var countMatches = currentCount == expectedCenterCount;

        var municipalityMode = await GetConfigStringAsync(db, "tehran.boundary.mode", "municipality", ct);
        var municipalityCells = await db.H3CoverageCells.CountAsync(c => c.MunicipalityMode, ct);
        var refinementEnabled = await GetConfigBoolAsync(db, H3BoundaryRefinementPlanner.RefinementConfigKey, true, ct);
        var refinedCells = await db.H3CoverageCells.CountAsync(c => c.IsRefined, ct);
        var refinementMissing = refinementEnabled
                                && refinedCells == 0
                                && expected.VirtualCenterCount > 0;

        var boundaryMismatch = municipalityMode.Equals("municipality", StringComparison.OrdinalIgnoreCase)
                               && (municipalityCells != expectedCenterCount
                                   || refinedCells != expected.VirtualCenterCount
                                   || refinementMissing);

        if (resolutionMatches && countMatches && !boundaryMismatch)
            return;

        var legacyTarget = await GetLegacyTargetCellCountAsync(db, ct);
        var autoMode = await IsAutoResolutionAsync(db, ct);
        var shouldReseed = await GetConfigBoolAsync(db, "crawl.h3_reseed_on_startup", false, ct)
                           || legacyTarget.HasValue
                           || boundaryMismatch
                           || refinementMissing
                           || (autoMode && (!resolutionMatches || !countMatches));

        if (!shouldReseed)
        {
            logger?.LogWarning(
                "H3 grid mismatch: DB has {Current} cells ({Refined} refined, res {CurrentRes}), expected {Expected} cells ({ExpectedRefined} refined, res {ExpectedRes}). " +
                "RefinementEnabled={RefinementEnabled}. Set crawl.h3_reseed_on_startup=true to rebuild.",
                currentCount,
                refinedCells,
                currentResolution.Count == 1 ? currentResolution[0].ToString() : "mixed",
                expectedCenterCount,
                expected.VirtualCenterCount,
                expected.StorageResolution,
                refinementEnabled);
            return;
        }

        if (legacyTarget.HasValue)
        {
            logger?.LogInformation(
                "Migrating legacy crawl.h3_target_cell_count={Legacy} to standard H3 polyfill ({Expected} cells at res {Resolution})",
                legacyTarget.Value, expectedCenterCount, expected.StorageResolution);
        }
        else
        {
            logger?.LogInformation(
                "Reseeding H3 grid: current {Current} cells, expected {Expected} at resolution {Resolution}",
                currentCount, expectedCenterCount, expected.StorageResolution);
        }

        await ReseedTehranGridAsync(db, logger, ct);
        await RemoveLegacyTargetCellCountConfigAsync(db, ct);
    }

    private static async Task<int?> GetLegacyTargetCellCountAsync(AppDbContext db, CancellationToken ct)
    {
        var value = await db.SystemConfigurations
            .Where(c => c.ConfigKey == "crawl.h3_target_cell_count")
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static async Task RemoveLegacyTargetCellCountConfigAsync(AppDbContext db, CancellationToken ct)
    {
        await db.SystemConfigurations
            .Where(c => c.ConfigKey == "crawl.h3_target_cell_count")
            .ExecuteDeleteAsync(ct);
    }

    private static async Task<TehranBounds?> LoadBoundsAsync(AppDbContext db, CancellationToken ct)
    {
        var boundsJson = await db.SystemConfigurations
            .Where(c => c.ConfigKey == "tehran.bounds")
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(boundsJson))
            return null;

        return JsonSerializer.Deserialize<TehranBounds>(boundsJson, JsonOpts);
    }

    /// <summary>
    /// Returns null for auto-selection, or an explicit resolution from config.
    /// </summary>
    private static async Task<int?> GetOptionalResolutionAsync(AppDbContext db, CancellationToken ct)
    {
        var value = await db.SystemConfigurations
            .Where(c => c.ConfigKey == "crawl.h3_resolution")
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(value) || value.Equals("auto", StringComparison.OrdinalIgnoreCase))
            return null;

        return int.TryParse(value, out var parsed) ? parsed : null;
    }

    private static async Task<bool> IsAutoResolutionAsync(AppDbContext db, CancellationToken ct)
    {
        var value = await db.SystemConfigurations
            .Where(c => c.ConfigKey == "crawl.h3_resolution")
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(value)
               || value.Equals("auto", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<TehranGridPlan> PlanGridAsync(
        AppDbContext db, TehranBounds bounds, CancellationToken ct)
    {
        var mode = await GetConfigStringAsync(db, "tehran.boundary.mode", "municipality", ct);
        var configuredRadius = await GetConfigIntAsync(db, "search.radius.default_meters", 2000, ct);
        var fixedResolution = await GetOptionalResolutionAsync(db, ct);
        var searchTermsPerCrawl = await CountSearchTermsPerCrawlAsync(db, ct);
        var refinementEnabled = await GetConfigBoolAsync(db, H3BoundaryRefinementPlanner.RefinementConfigKey, true, ct);

        if (mode.Equals("municipality", StringComparison.OrdinalIgnoreCase))
        {
            var path = TehranMunicipalityBoundary.ResolveDefaultPath();
            var boundary = TehranMunicipalityBoundary.LoadFromFile(path);
            return H3GridPlanner.PlanTehranMunicipalityGrid(
                boundary,
                configuredRadius,
                fixedResolution ?? 7,
                searchTermsPerCrawl,
                enableBoundaryRefinement: refinementEnabled);
        }

        return H3GridPlanner.PlanTehranGrid(
            bounds.MinLat, bounds.MaxLat, bounds.MinLng, bounds.MaxLng,
            configuredRadius, fixedResolution, searchTermsPerCrawl);
    }

    private static async Task<string> GetConfigStringAsync(
        AppDbContext db, string key, string defaultValue, CancellationToken ct)
    {
        var value = await db.SystemConfigurations
            .Where(c => c.ConfigKey == key)
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    private static async Task<int> CountSearchTermsPerCrawlAsync(AppDbContext db, CancellationToken ct)
    {
        var jsonList = await db.PoiCategories
            .Where(c => c.IsEnabled)
            .Select(c => c.SearchTermsJson)
            .ToListAsync(ct);

        var total = 0;
        foreach (var json in jsonList)
        {
            var terms = JsonSerializer.Deserialize<string[]>(json, JsonOpts) ?? [];
            total += terms.Length;
        }

        return total > 0 ? total : H3GridPlanner.DefaultSearchTermsPerCrawl;
    }

    private static async Task<int> GetConfigIntAsync(
        AppDbContext db, string key, int defaultValue, CancellationToken ct)
    {
        var value = await db.SystemConfigurations
            .Where(c => c.ConfigKey == key)
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static async Task UpdateConfigIntAsync(
        AppDbContext db, string key, int value, CancellationToken ct)
    {
        var updated = await db.SystemConfigurations
            .Where(c => c.ConfigKey == key)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(c => c.ConfigValue, value.ToString())
                    .SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow)
                    .SetProperty(c => c.UpdatedBy, "h3-grid-planner"),
                ct);

        if (updated == 0)
        {
            db.SystemConfigurations.Add(new SystemConfiguration
            {
                ConfigKey = key,
                ConfigValue = value.ToString(),
                ValueType = "int",
                Description = "Optimized by H3 grid planner",
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = "h3-grid-planner"
            });
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task UpdateConfigStringAsync(
        AppDbContext db, string key, string value, CancellationToken ct)
    {
        var updated = await db.SystemConfigurations
            .Where(c => c.ConfigKey == key)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(c => c.ConfigValue, value)
                    .SetProperty(c => c.UpdatedAt, DateTimeOffset.UtcNow)
                    .SetProperty(c => c.UpdatedBy, "h3-grid-planner"),
                ct);

        if (updated == 0)
        {
            db.SystemConfigurations.Add(new SystemConfiguration
            {
                ConfigKey = key,
                ConfigValue = value,
                ValueType = "string",
                Description = "Set by H3 grid planner",
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = "h3-grid-planner"
            });
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task<bool> GetConfigBoolAsync(
        AppDbContext db, string key, bool defaultValue, CancellationToken ct)
    {
        var value = await db.SystemConfigurations
            .Where(c => c.ConfigKey == key)
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
               || value == "1";
    }

    private sealed class TehranBounds
    {
        public double MinLat { get; set; }
        public double MaxLat { get; set; }
        public double MinLng { get; set; }
        public double MaxLng { get; set; }
    }
}
