namespace Didibood.LocationAccess.Infrastructure.H3;

public sealed class CoverageSpatialMetrics
{
    public double EstimatedCoveragePercent { get; init; }
    public double AverageCenterSpacingMeters { get; init; }
    public double UncoveredAreaKm2 { get; init; }
    public double OverlapRatio { get; init; }
    public int RecommendedSearchRadiusMeters { get; init; }
    public double MedianNeighborSpacingMeters { get; init; }
}

public sealed class GridScenarioComparison
{
    public required string Scenario { get; init; }
    public int CellCount { get; init; }
    public int SearchCenterCount { get; init; }
    public int EstimatedRequests { get; init; }
    public double EstimatedCoveragePercent { get; init; }
    public double OverlapPercent { get; init; }
    public int SearchRadiusMeters { get; init; }
}

/// <summary>
/// Monte Carlo spatial coverage analysis and radius recommendations for Tehran municipality grids.
/// </summary>
public static class H3CoverageMetrics
{
    private const double H3Res7EdgeKm = 1.220629;
    private static readonly double H3Res7TheoreticalSpacingM = H3Res7EdgeKm * Math.Sqrt(3) * 1000;

    public static CoverageSpatialMetrics Analyze(
        TehranMunicipalityBoundary boundary,
        IReadOnlyList<(double Lat, double Lng)> centers,
        int searchRadiusMeters,
        int samplePoints = 1500)
    {
        var (estimatedCoverage, overlapRatio, uncoveredKm2) =
            EstimateCoverage(boundary, centers, searchRadiusMeters, samplePoints);

        var spacing = ComputeCenterSpacing(centers);

        return new CoverageSpatialMetrics
        {
            EstimatedCoveragePercent = Math.Round(estimatedCoverage, 1),
            AverageCenterSpacingMeters = Math.Round(spacing.Average, 0),
            MedianNeighborSpacingMeters = Math.Round(spacing.Median, 0),
            UncoveredAreaKm2 = Math.Round(uncoveredKm2, 2),
            OverlapRatio = Math.Round(overlapRatio, 3),
            RecommendedSearchRadiusMeters = RecommendSearchRadius(spacing.Median, searchRadiusMeters)
        };
    }

    public static IReadOnlyList<GridScenarioComparison> CompareScenarios(
        TehranMunicipalityBoundary boundary,
        int searchTermsPerCrawl,
        int searchRadiusMeters = H3GridPlanner.DefaultSearchRadiusMeters,
        int maxCrawlRequests = H3GridPlanner.MaxCrawlRequests)
    {
        var baseCells = H3CellGeometry.PolyfillMunicipality(boundary, 7);
        var baseCenters = baseCells.Select(H3CellGeometry.GetCentroid).ToList();
        var baseMetrics = Analyze(boundary, baseCenters, searchRadiusMeters);

        var refinedCenters = H3BoundaryRefinementPlanner.PlanSearchCenters(
            baseCells, boundary, searchRadiusMeters, searchTermsPerCrawl, maxCrawlRequests);
        var refinedCoords = refinedCenters.Select(c => (c.Lat, c.Lng)).ToList();
        var refinedMetrics = Analyze(boundary, refinedCoords, searchRadiusMeters);

        var res8Cells = H3CellGeometry.PolyfillMunicipality(boundary, 8);
        var res8Centers = res8Cells.Select(H3CellGeometry.GetCentroid).ToList();
        var res8Radius = H3GridPlanner.FindMinimumOverlapRadiusInsideBoundary(res8Cells, boundary)
                         ?? searchRadiusMeters;
        var res8Metrics = Analyze(boundary, res8Centers, res8Radius);

        return
        [
            new GridScenarioComparison
            {
                Scenario = "A_res7_base",
                CellCount = baseCells.Count,
                SearchCenterCount = baseCells.Count,
                EstimatedRequests = baseCells.Count * searchTermsPerCrawl,
                EstimatedCoveragePercent = baseMetrics.EstimatedCoveragePercent,
                OverlapPercent = Math.Round(baseMetrics.OverlapRatio * 100, 1),
                SearchRadiusMeters = searchRadiusMeters
            },
            new GridScenarioComparison
            {
                Scenario = "B_res7_boundary_refinement",
                CellCount = baseCells.Count,
                SearchCenterCount = refinedCenters.Count,
                EstimatedRequests = refinedCenters.Count * searchTermsPerCrawl,
                EstimatedCoveragePercent = refinedMetrics.EstimatedCoveragePercent,
                OverlapPercent = Math.Round(refinedMetrics.OverlapRatio * 100, 1),
                SearchRadiusMeters = searchRadiusMeters
            },
            new GridScenarioComparison
            {
                Scenario = "C_res8_full_polyfill",
                CellCount = res8Cells.Count,
                SearchCenterCount = res8Cells.Count,
                EstimatedRequests = res8Cells.Count * searchTermsPerCrawl,
                EstimatedCoveragePercent = res8Metrics.EstimatedCoveragePercent,
                OverlapPercent = Math.Round(res8Metrics.OverlapRatio * 100, 1),
                SearchRadiusMeters = res8Radius
            }
        ];
    }

    public static int RecommendSearchRadius(double medianNeighborSpacingMeters, int currentRadiusMeters)
    {
        // H3 res-7 theoretical center spacing ≈ √3 × 1.221 km ≈ 2114 m.
        var fromTheoretical = (int)Math.Round(H3Res7TheoreticalSpacingM * 0.94, MidpointRounding.AwayFromZero);
        var fromObserved = medianNeighborSpacingMeters > 0
            ? (int)Math.Round(Math.Min(medianNeighborSpacingMeters, H3Res7TheoreticalSpacingM * 1.1) * 0.94,
                MidpointRounding.AwayFromZero)
            : fromTheoretical;
        var recommended = Math.Clamp(Math.Min(fromTheoretical, fromObserved), 1850, 2150);

        return Math.Abs(recommended - currentRadiusMeters) <= 50 ? currentRadiusMeters : recommended;
    }

    public static (double CoveragePercent, double OverlapRatio, double UncoveredAreaKm2) EstimateCoverage(
        TehranMunicipalityBoundary boundary,
        IReadOnlyList<(double Lat, double Lng)> centers,
        int searchRadiusMeters,
        int samplePoints = 1500)
    {
        if (centers.Count == 0)
            return (0, 0, boundary.ApproximateAreaSqKm());

        var cityArea = boundary.ApproximateAreaSqKm();
        var env = boundary.Envelope;
        var rng = new Random(42);
        var covered = 0;
        var multiCovered = 0;
        var found = 0;

        for (var attempt = 0; attempt < samplePoints * 40 && found < samplePoints; attempt++)
        {
            var lat = env.MinY + rng.NextDouble() * (env.MaxY - env.MinY);
            var lng = env.MinX + rng.NextDouble() * (env.MaxX - env.MinX);
            if (!boundary.ContainsPoint(lat, lng))
                continue;

            found++;
            var hits = 0;
            foreach (var (cLat, cLng) in centers)
            {
                if (HaversineMeters(lat, lng, cLat, cLng) <= searchRadiusMeters)
                    hits++;
            }

            if (hits >= 1) covered++;
            if (hits >= 2) multiCovered++;
        }

        if (found == 0)
            return (0, 0, cityArea);

        var coveragePct = covered * 100.0 / found;
        var overlapRatio = covered == 0 ? 0 : multiCovered / (double)covered;
        var uncoveredKm2 = cityArea * (1 - coveragePct / 100.0);
        return (coveragePct, overlapRatio, uncoveredKm2);
    }

    public static (double Average, double Median) ComputeCenterSpacing(
        IReadOnlyList<(double Lat, double Lng)> centers)
    {
        if (centers.Count < 2)
            return (H3Res7TheoreticalSpacingM, H3Res7TheoreticalSpacingM);

        var nearest = new List<double>(centers.Count);
        for (var i = 0; i < centers.Count; i++)
        {
            var min = double.MaxValue;
            for (var j = 0; j < centers.Count; j++)
            {
                if (i == j) continue;
                var d = HaversineMeters(centers[i].Lat, centers[i].Lng, centers[j].Lat, centers[j].Lng);
                if (d < min) min = d;
            }

            nearest.Add(min);
        }

        nearest.Sort();
        return (nearest.Average(), nearest[nearest.Count / 2]);
    }

    internal static double HaversineMeters(double lat1, double lng1, double lat2, double lng2)
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
