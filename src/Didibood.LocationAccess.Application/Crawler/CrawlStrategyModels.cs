namespace Didibood.LocationAccess.Application.Crawler;

public sealed class CrawlBudgetState
{
    public int MaxRequests { get; init; } = 15_000;
    public int UsedRequests { get; set; }
    public int ReservedForRefinement { get; init; } = 2_000;
    public int Remaining => Math.Max(0, MaxRequests - UsedRequests);
    public bool IsExhausted => UsedRequests >= MaxRequests;
}

public sealed class CellDensitySignal
{
    public long H3Index { get; init; }
    public int ApiResultCount { get; init; }
    public int PoiCountAfterNormalize { get; init; }
    public int CategoriesSaturated { get; init; }
    public bool IsDense { get; init; }
}

public sealed class OverlapSkipDecision
{
    public bool ShouldSkip { get; init; }
    public string Reason { get; init; } = "";
    public int ReusedPoiCount { get; init; }
}

public sealed class SpatialDedupMatch
{
    public Guid ExistingPoiId { get; init; }
    public double DistanceMeters { get; init; }
    public string NormalizedTitle { get; init; } = "";
}
