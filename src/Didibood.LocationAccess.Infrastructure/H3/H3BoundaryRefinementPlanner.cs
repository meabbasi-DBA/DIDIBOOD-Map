using NetTopologySuite.Geometries;

namespace Didibood.LocationAccess.Infrastructure.H3;

/// <summary>
/// Adds virtual sub-centroids inside boundary H3 cells to improve edge coverage
/// without a full resolution-8 polyfill.
/// </summary>
public static class H3BoundaryRefinementPlanner
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    public const int MaxSubCentroidsPerCell = 3;
    public const double MaxRequestIncreaseRatio = 0.30;
    public const double MinSeparationMeters = 350;

    public const string RefinementConfigKey = "grid.boundary.refinement.enabled";

    public static H3RefinementPlanResult PlanSearchCentersWithDiagnostics(
        IReadOnlyList<long> baseCells,
        TehranMunicipalityBoundary boundary,
        int searchRadiusMeters,
        int searchTermsPerCrawl,
        int maxCrawlRequests = H3GridPlanner.MaxCrawlRequests,
        bool enableRefinement = true)
    {
        var centers = new List<H3SearchCenter>();
        var allCoords = new List<(double Lat, double Lng)>();

        foreach (var h3 in baseCells)
        {
            var (lat, lng) = H3CellGeometry.GetCentroid(h3);
            centers.Add(new H3SearchCenter
            {
                H3Index = h3,
                ParentH3Index = null,
                Lat = lat,
                Lng = lng,
                IsRefined = false,
                Resolution = 7
            });
            allCoords.Add((lat, lng));
        }

        if (!enableRefinement || baseCells.Count == 0)
        {
            return new H3RefinementPlanResult
            {
                SearchCenters = centers,
                BaseCellCount = baseCells.Count,
                RefinementEnabled = false,
                DisabledReason = !enableRefinement ? "config_disabled" : "no_base_cells"
            };
        }

        var maxTotalCenters = Math.Min(
            maxCrawlRequests / Math.Max(1, searchTermsPerCrawl),
            (int)Math.Floor(baseCells.Count * (1 + MaxRequestIncreaseRatio)));

        var maxVirtual = maxTotalCenters - baseCells.Count;
        if (maxVirtual <= 0)
        {
            return new H3RefinementPlanResult
            {
                SearchCenters = centers,
                BaseCellCount = baseCells.Count,
                RefinementEnabled = false,
                DisabledReason = "budget_exhausted",
                MaxVirtualBudget = 0
            };
        }

        // Score gaps at the planning radius, but also use clip ratio so high search radii
        // do not suppress boundary refinement entirely.
        var scoringRadius = Math.Min(searchRadiusMeters, H3GridPlanner.DefaultSearchRadiusMeters);
        var (candidates, skippedByThreshold) = ScoreBoundaryCells(
            baseCells, boundary, scoringRadius, allCoords);

        var virtualAdded = 0;
        var rejectedBySeparation = 0;
        var skippedByBudget = 0;

        foreach (var candidate in candidates)
        {
            if (virtualAdded >= maxVirtual)
            {
                skippedByBudget += candidate.SuggestedSubCentroids;
                continue;
            }

            var subs = GenerateSubCentroids(candidate.H3Index, boundary, candidate.SuggestedSubCentroids);
            var slot = 0;
            foreach (var (lat, lng) in subs)
            {
                if (virtualAdded >= maxVirtual)
                {
                    skippedByBudget++;
                    continue;
                }

                if (allCoords.Any(c => H3CoverageMetrics.HaversineMeters(lat, lng, c.Lat, c.Lng) < MinSeparationMeters))
                {
                    rejectedBySeparation++;
                    continue;
                }

                centers.Add(new H3SearchCenter
                {
                    H3Index = H3VirtualCenterId.Create(candidate.H3Index, slot),
                    ParentH3Index = candidate.H3Index,
                    Lat = lat,
                    Lng = lng,
                    IsRefined = true,
                    Resolution = 7
                });
                allCoords.Add((lat, lng));
                virtualAdded++;
                slot++;
            }
        }

        return new H3RefinementPlanResult
        {
            SearchCenters = centers,
            BaseCellCount = baseCells.Count,
            CandidateCellCount = candidates.Count,
            SelectedRefinedCount = virtualAdded,
            RejectedBySeparationCount = rejectedBySeparation,
            SkippedByBudgetCount = skippedByBudget,
            SkippedByThresholdCount = skippedByThreshold,
            RefinementEnabled = true,
            MaxVirtualBudget = maxVirtual
        };
    }

    public static IReadOnlyList<H3SearchCenter> PlanSearchCenters(
        IReadOnlyList<long> baseCells,
        TehranMunicipalityBoundary boundary,
        int searchRadiusMeters,
        int searchTermsPerCrawl,
        int maxCrawlRequests = H3GridPlanner.MaxCrawlRequests,
        bool enableRefinement = true) =>
        PlanSearchCentersWithDiagnostics(
            baseCells, boundary, searchRadiusMeters, searchTermsPerCrawl, maxCrawlRequests, enableRefinement)
            .SearchCenters;

    public static bool IsBoundaryCell(long h3Index, TehranMunicipalityBoundary boundary)
    {
        var cellPoly = ToCellPolygon(h3Index);
        return !boundary.Union.Contains(cellPoly);
    }

    public static double ComputeLocalGapScore(
        long h3Index,
        TehranMunicipalityBoundary boundary,
        int searchRadiusMeters,
        IReadOnlyList<(double Lat, double Lng)> allCenters)
    {
        var cellPoly = ToCellPolygon(h3Index);
        var env = cellPoly.EnvelopeInternal;
        var rng = new Random((int)(h3Index % int.MaxValue));
        var uncovered = 0;
        var found = 0;

        for (var attempt = 0; attempt < 120 && found < 40; attempt++)
        {
            var lat = env.MinY + rng.NextDouble() * (env.MaxY - env.MinY);
            var lng = env.MinX + rng.NextDouble() * (env.MaxX - env.MinX);
            var point = Factory.CreatePoint(new Coordinate(lng, lat));
            if (!cellPoly.Contains(point) || !boundary.ContainsPoint(lat, lng))
                continue;

            found++;
            var covered = allCenters.Any(c =>
                H3CoverageMetrics.HaversineMeters(lat, lng, c.Lat, c.Lng) <= searchRadiusMeters);
            if (!covered)
                uncovered++;
        }

        return found == 0 ? 0 : uncovered / (double)found;
    }

    public static IReadOnlyList<(double Lat, double Lng)> GenerateSubCentroids(
        long h3Index,
        TehranMunicipalityBoundary boundary,
        int maxCount)
    {
        if (maxCount <= 0)
            return [];

        var centroid = H3CellGeometry.GetCentroid(h3Index);
        var ring = H3CellGeometry.GetBoundary(h3Index);
        var interiorVertices = ring
            .Select(v => (Lat: v[1], Lng: v[0]))
            .Where(v => boundary.ContainsPoint(v.Lat, v.Lng))
            .Distinct()
            .OrderByDescending(v => H3CoverageMetrics.HaversineMeters(centroid.Lat, centroid.Lng, v.Lat, v.Lng))
            .Take(maxCount)
            .ToList();

        if (interiorVertices.Count == 0)
            return [];

        var results = new List<(double Lat, double Lng)>();
        var fractions = new[] { 0.50, 0.62, 0.72 };

        for (var i = 0; i < interiorVertices.Count && results.Count < maxCount; i++)
        {
            var v = interiorVertices[i];
            var frac = fractions[Math.Min(i, fractions.Length - 1)];
            var lat = centroid.Lat + (v.Lat - centroid.Lat) * frac;
            var lng = centroid.Lng + (v.Lng - centroid.Lng) * frac;

            if (!boundary.ContainsPoint(lat, lng))
                continue;

            results.Add((lat, lng));
        }

        return results;
    }

    public static double ComputeBoundaryClipScore(long h3Index, TehranMunicipalityBoundary boundary)
    {
        var cellPoly = ToCellPolygon(h3Index);
        var env = cellPoly.EnvelopeInternal;
        var rng = new Random((int)(h3Index % int.MaxValue));
        var outside = 0;
        var inside = 0;

        for (var i = 0; i < 80; i++)
        {
            var lat = env.MinY + rng.NextDouble() * (env.MaxY - env.MinY);
            var lng = env.MinX + rng.NextDouble() * (env.MaxX - env.MinX);
            var point = Factory.CreatePoint(new Coordinate(lng, lat));
            if (!cellPoly.Contains(point))
                continue;

            if (boundary.ContainsPoint(lat, lng))
                inside++;
            else
                outside++;
        }

        var total = inside + outside;
        return total == 0 ? 0 : outside / (double)total;
    }

    private static (List<BoundaryCandidate> Candidates, int SkippedByThreshold) ScoreBoundaryCells(
        IReadOnlyList<long> baseCells,
        TehranMunicipalityBoundary boundary,
        int searchRadiusMeters,
        IReadOnlyList<(double Lat, double Lng)> allCenters)
    {
        var candidates = new List<BoundaryCandidate>();
        var skippedByThreshold = 0;

        foreach (var h3 in baseCells)
        {
            var isBoundary = IsBoundaryCell(h3, boundary);
            var clip = isBoundary ? ComputeBoundaryClipScore(h3, boundary) : 0;
            var gap = ComputeLocalGapScore(h3, boundary, searchRadiusMeters, allCenters);
            var combined = gap + clip * 0.65;

            if (isBoundary && clip >= 0.05)
            {
                var suggested = clip >= 0.22 || combined >= 0.35 ? 3
                    : clip >= 0.12 || combined >= 0.18 ? 2
                    : 1;
                candidates.Add(new BoundaryCandidate(h3, combined, suggested));
                continue;
            }

            if (!isBoundary && gap >= 0.10)
            {
                var suggested = gap >= 0.28 ? 3 : gap >= 0.16 ? 2 : 1;
                candidates.Add(new BoundaryCandidate(h3, combined, suggested));
                continue;
            }

            skippedByThreshold++;
        }

        return (candidates.OrderByDescending(c => c.GapScore).ToList(), skippedByThreshold);
    }

    private static Polygon ToCellPolygon(long h3Index)
    {
        var ring = H3CellGeometry.GetBoundary(h3Index);
        var coords = ring.Select(p => new Coordinate(p[0], p[1])).ToArray();
        if (coords.Length > 1 && coords[0].Equals2D(coords[^1]))
            coords = coords[..^1];

        return Factory.CreatePolygon(coords);
    }

    private sealed record BoundaryCandidate(long H3Index, double GapScore, int SuggestedSubCentroids);
}
