namespace Didibood.LocationAccess.Infrastructure.H3;

public sealed class TehranGridPlan
{
    public required short StorageResolution { get; init; }
    public required IReadOnlyList<long> CellIndexes { get; init; }
    public IReadOnlyList<H3SearchCenter> SearchCenters { get; init; } = [];
    public int BaseCellCount { get; init; }
    public int VirtualCenterCount { get; init; }
    public int SearchRadiusMeters { get; init; }
    public int EstimatedRequestsPerCrawl { get; init; }
    public string BoundaryMode { get; init; } = "municipality";
    public CoverageSpatialMetrics? SpatialMetrics { get; init; }
    public IReadOnlyList<GridScenarioComparison> ScenarioComparison { get; init; } = [];
    public H3RefinementPlanResult? Refinement { get; init; }
}

/// <summary>
/// Plans H3 crawl grids for Tehran using municipality polygon polyfill (Phase 1 res-7)
/// with optional budget targeting and overlap validation at 2000 m search radius.
/// </summary>
public static class H3GridPlanner
{
    public const int DefaultSearchTermsPerCrawl = 27;
    public const int MinCrawlRequests = 10_000;
    public const int MaxCrawlRequests = 15_000;
    public const int DefaultSearchRadiusMeters = 2000;

    public const double DefaultMinLat = 35.50;
    public const double DefaultMaxLat = 35.88;
    public const double DefaultMinLng = 51.10;
    public const double DefaultMaxLng = 51.62;

    public static readonly int[] AutoResolutions = [7, 6, 8];
    public const int DefaultMinRadiusMeters = 1500;
    public const int DefaultMaxRadiusMeters = 4500;
    public const int DefaultRadiusStepMeters = 50;

    /// <summary>
    /// Phase 1: municipality polygon polyfill at resolution 7 (or fixed resolution).
    /// </summary>
    public static TehranGridPlan PlanTehranMunicipalityGrid(
        TehranMunicipalityBoundary boundary,
        int searchRadiusMeters = DefaultSearchRadiusMeters,
        int? fixedResolution = null,
        int searchTermsPerCrawl = DefaultSearchTermsPerCrawl,
        int maxCrawlRequests = MaxCrawlRequests,
        bool enableBoundaryRefinement = true)
    {
        var resolution = (short)(fixedResolution ?? 7);
        var cells = H3CellGeometry.PolyfillMunicipality(boundary, resolution);

        if (cells.Count == 0)
            throw new InvalidOperationException($"Tehran municipality produced zero H3 cells at resolution {resolution}.");

        var spacing = H3CoverageMetrics.ComputeCenterSpacing(cells.Select(H3CellGeometry.GetCentroid).ToList());
        var recommendedRadius = H3CoverageMetrics.RecommendSearchRadius(spacing.Median, searchRadiusMeters);
        if (ValidateOverlapInsideBoundary(cells, boundary, recommendedRadius))
            searchRadiusMeters = recommendedRadius;

        var searchCenters = H3BoundaryRefinementPlanner.PlanSearchCentersWithDiagnostics(
            cells, boundary, searchRadiusMeters, searchTermsPerCrawl, maxCrawlRequests, enableBoundaryRefinement);

        var centerCoords = searchCenters.SearchCenters.Select(c => (c.Lat, c.Lng)).ToList();
        if (!ValidateOverlapWithCenters(centerCoords, boundary, searchRadiusMeters))
        {
            var minRadius = FindMinimumOverlapRadiusWithCenters(centerCoords, boundary)
                            ?? searchRadiusMeters;
            searchRadiusMeters = minRadius;
        }

        var requests = searchCenters.SearchCenters.Count * searchTermsPerCrawl;
        if (requests > maxCrawlRequests)
        {
            throw new InvalidOperationException(
                $"Municipality grid at res {resolution} requires {requests} requests (max {maxCrawlRequests}). " +
                "Reduce categories/terms or disable boundary refinement.");
        }

        var spatialMetrics = H3CoverageMetrics.Analyze(boundary, centerCoords, searchRadiusMeters);
        var scenarioComparison = H3CoverageMetrics.CompareScenarios(
            boundary, searchTermsPerCrawl, searchRadiusMeters, maxCrawlRequests);

        return new TehranGridPlan
        {
            StorageResolution = resolution,
            CellIndexes = cells,
            SearchCenters = searchCenters.SearchCenters,
            BaseCellCount = cells.Count,
            VirtualCenterCount = searchCenters.SelectedRefinedCount,
            SearchRadiusMeters = searchRadiusMeters,
            EstimatedRequestsPerCrawl = requests,
            BoundaryMode = "municipality",
            SpatialMetrics = spatialMetrics,
            ScenarioComparison = scenarioComparison,
            Refinement = searchCenters
        };
    }

    /// <summary>Legacy bbox planner kept for probes/tests.</summary>
    public static TehranGridPlan PlanTehranGrid(
        double minLat, double maxLat, double minLng, double maxLng,
        int searchRadiusMeters = DefaultSearchRadiusMeters,
        int? fixedResolution = null,
        int searchTermsPerCrawl = DefaultSearchTermsPerCrawl,
        int minCrawlRequests = MinCrawlRequests,
        int maxCrawlRequests = MaxCrawlRequests)
    {
        if (fixedResolution is int explicitResolution)
        {
            var cells = H3CellGeometry.PolyfillBounds(minLat, maxLat, minLng, maxLng, explicitResolution);
            var radius = FindMinimumOverlapRadius(cells, minLat, maxLat, minLng, maxLng) ?? searchRadiusMeters;
            return BuildPlan(cells, (short)explicitResolution, radius, searchTermsPerCrawl, "bbox");
        }

        var candidates = new List<GridCandidate>();
        foreach (var resolution in AutoResolutions)
        {
            var cells = H3CellGeometry.PolyfillBounds(minLat, maxLat, minLng, maxLng, resolution);
            if (cells.Count == 0) continue;

            var minRadius = FindMinimumOverlapRadius(cells, minLat, maxLat, minLng, maxLng);
            if (minRadius is null) continue;

            candidates.Add(new GridCandidate((short)resolution, cells, minRadius.Value, cells.Count * searchTermsPerCrawl));
        }

        if (candidates.Count == 0)
        {
            var fallbackCells = H3CellGeometry.PolyfillBounds(minLat, maxLat, minLng, maxLng, 8);
            var fallbackRadius = FindMinimumOverlapRadius(fallbackCells, minLat, maxLat, minLng, maxLng) ?? searchRadiusMeters;
            return BuildPlan(fallbackCells, 8, fallbackRadius, searchTermsPerCrawl, "bbox");
        }

        const int targetMid = (MinCrawlRequests + MaxCrawlRequests) / 2;
        var best = candidates
            .Where(c => c.Requests >= minCrawlRequests && c.Requests <= maxCrawlRequests)
            .OrderBy(c => Math.Abs(c.Requests - targetMid))
            .ThenBy(c => c.Radius)
            .ThenBy(c => Math.Abs(c.Resolution - 7))
            .FirstOrDefault()
            ?? candidates
                .OrderBy(c => DistanceFromBudget(c.Requests, minCrawlRequests, maxCrawlRequests))
                .ThenBy(c => c.Radius)
                .ThenBy(c => Math.Abs(c.Resolution - 7))
                .First();

        return BuildPlan(best.Cells, best.Resolution, best.Radius, searchTermsPerCrawl, "bbox");
    }

    public static int? FindMinimumOverlapRadius(
        IReadOnlyList<long> cellIndexes,
        double minLat, double maxLat, double minLng, double maxLng,
        int minRadiusMeters = DefaultMinRadiusMeters,
        int maxRadiusMeters = DefaultMaxRadiusMeters,
        int stepMeters = DefaultRadiusStepMeters,
        int samplePoints = 500)
    {
        for (var radius = minRadiusMeters; radius <= maxRadiusMeters; radius += stepMeters)
        {
            if (ValidateOverlap(cellIndexes, radius, minLat, maxLat, minLng, maxLng, samplePoints))
                return radius;
        }

        return null;
    }

    public static int? FindMinimumOverlapRadiusInsideBoundary(
        IReadOnlyList<long> cellIndexes,
        TehranMunicipalityBoundary boundary,
        int minRadiusMeters = DefaultMinRadiusMeters,
        int maxRadiusMeters = DefaultMaxRadiusMeters,
        int stepMeters = DefaultRadiusStepMeters,
        int samplePoints = 300)
    {
        var centroids = cellIndexes.Select(H3CellGeometry.GetCentroid).ToList();
        return FindMinimumOverlapRadiusWithCenters(centroids, boundary, minRadiusMeters, maxRadiusMeters, stepMeters, samplePoints);
    }

    public static int? FindMinimumOverlapRadiusWithCenters(
        IReadOnlyList<(double Lat, double Lng)> centers,
        TehranMunicipalityBoundary boundary,
        int minRadiusMeters = DefaultMinRadiusMeters,
        int maxRadiusMeters = DefaultMaxRadiusMeters,
        int stepMeters = DefaultRadiusStepMeters,
        int samplePoints = 300)
    {
        for (var radius = minRadiusMeters; radius <= maxRadiusMeters; radius += stepMeters)
        {
            if (ValidateOverlapWithCenters(centers, boundary, radius, samplePoints))
                return radius;
        }

        return null;
    }

    public static bool ValidateOverlapWithCenters(
        IReadOnlyList<(double Lat, double Lng)> centers,
        TehranMunicipalityBoundary boundary,
        int searchRadiusMeters,
        int samplePoints = 300)
    {
        if (centers.Count == 0)
            return false;

        var env = boundary.Envelope;
        var rng = new Random(42);
        var found = 0;

        for (var attempt = 0; attempt < samplePoints * 20 && found < samplePoints; attempt++)
        {
            var lat = env.MinY + rng.NextDouble() * (env.MaxY - env.MinY);
            var lng = env.MinX + rng.NextDouble() * (env.MaxX - env.MinX);
            if (!boundary.ContainsPoint(lat, lng))
                continue;

            found++;
            if (!centers.Any(c => HaversineMeters(lat, lng, c.Lat, c.Lng) <= searchRadiusMeters))
                return false;
        }

        return found >= Math.Min(samplePoints, 50);
    }

    public static bool ValidateOverlapInsideBoundary(
        IReadOnlyList<long> cellIndexes,
        TehranMunicipalityBoundary boundary,
        int searchRadiusMeters,
        int samplePoints = 300)
    {
        var centroids = cellIndexes.Select(H3CellGeometry.GetCentroid).ToList();
        return ValidateOverlapWithCenters(centroids, boundary, searchRadiusMeters, samplePoints);
    }

    public static bool ValidateOverlap(
        IReadOnlyList<long> cellIndexes,
        int searchRadiusMeters,
        double minLat = DefaultMinLat,
        double maxLat = DefaultMaxLat,
        double minLng = DefaultMinLng,
        double maxLng = DefaultMaxLng,
        int samplePoints = 500)
    {
        if (cellIndexes.Count == 0)
            return false;

        var centroids = cellIndexes.Select(H3CellGeometry.GetCentroid).ToList();
        var fixedSamples = new (double Lat, double Lng)[]
        {
            (minLat, minLng), (minLat, maxLng), (maxLat, minLng), (maxLat, maxLng),
            (minLat, (minLng + maxLng) / 2), (maxLat, (minLng + maxLng) / 2),
            ((minLat + maxLat) / 2, minLng), ((minLat + maxLat) / 2, maxLng)
        };

        foreach (var (lat, lng) in fixedSamples)
        {
            if (!centroids.Any(c => HaversineMeters(lat, lng, c.Lat, c.Lng) <= searchRadiusMeters))
                return false;
        }

        var rng = new Random(42);
        for (var i = 0; i < samplePoints; i++)
        {
            var lat = minLat + rng.NextDouble() * (maxLat - minLat);
            var lng = minLng + rng.NextDouble() * (maxLng - minLng);
            if (!centroids.Any(c => HaversineMeters(lat, lng, c.Lat, c.Lng) <= searchRadiusMeters))
                return false;
        }

        return true;
    }

    private static TehranGridPlan BuildPlan(
        IReadOnlyList<long> cells, short resolution, int searchRadiusMeters,
        int searchTermsPerCrawl, string boundaryMode)
    {
        if (cells.Count == 0)
            throw new InvalidOperationException($"Tehran bounds produced zero H3 cells at resolution {resolution}.");

        var centroids = cells.Select(h3 =>
        {
            var (lat, lng) = H3CellGeometry.GetCentroid(h3);
            return new H3SearchCenter
            {
                H3Index = h3,
                Lat = lat,
                Lng = lng,
                IsRefined = false,
                Resolution = resolution
            };
        }).ToList();

        return new TehranGridPlan
        {
            StorageResolution = resolution,
            CellIndexes = cells,
            SearchCenters = centroids,
            BaseCellCount = cells.Count,
            VirtualCenterCount = 0,
            SearchRadiusMeters = searchRadiusMeters,
            EstimatedRequestsPerCrawl = cells.Count * searchTermsPerCrawl,
            BoundaryMode = boundaryMode
        };
    }

    private static int DistanceFromBudget(int requests, int minRequests, int maxRequests)
    {
        if (requests < minRequests) return minRequests - requests;
        if (requests > maxRequests) return requests - maxRequests;
        return 0;
    }

    private sealed record GridCandidate(short Resolution, IReadOnlyList<long> Cells, int Radius, int Requests);

    private static double HaversineMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusM = 6_371_000;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLng = DegreesToRadians(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
                * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return earthRadiusM * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
