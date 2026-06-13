using Didibood.LocationAccess.Application.Crawler;

namespace Didibood.LocationAccess.Infrastructure.Crawler;

/// <summary>
/// Tracks Neshan request budget per crawl execution with per-category counters.
/// </summary>
public sealed class CrawlBudgetTracker(int maxRequests = 15_000, int refinementReserve = 2_000)
{
    private readonly Dictionary<short, int> _categoryRequests = new();

    public CrawlBudgetState State { get; } = new()
    {
        MaxRequests = maxRequests,
        ReservedForRefinement = refinementReserve
    };

    public bool TryConsume(short categoryId, int count = 1)
    {
        if (State.UsedRequests + count > State.MaxRequests)
            return false;

        State.UsedRequests += count;
        _categoryRequests[categoryId] = _categoryRequests.GetValueOrDefault(categoryId) + count;
        return true;
    }

    public bool CanRefine(int childRequests) =>
        State.UsedRequests + childRequests <= State.MaxRequests;

    public IReadOnlyDictionary<short, int> RequestsPerCategory => _categoryRequests;
}
