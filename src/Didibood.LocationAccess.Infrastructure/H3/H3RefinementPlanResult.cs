namespace Didibood.LocationAccess.Infrastructure.H3;

public sealed class H3RefinementPlanResult
{
    public IReadOnlyList<H3SearchCenter> SearchCenters { get; init; } = [];
    public int BaseCellCount { get; init; }
    public int CandidateCellCount { get; init; }
    public int SelectedRefinedCount { get; init; }
    public int RejectedBySeparationCount { get; init; }
    public int SkippedByBudgetCount { get; init; }
    public int SkippedByThresholdCount { get; init; }
    public bool RefinementEnabled { get; init; }
    public string? DisabledReason { get; init; }
    public int MaxVirtualBudget { get; init; }
}
