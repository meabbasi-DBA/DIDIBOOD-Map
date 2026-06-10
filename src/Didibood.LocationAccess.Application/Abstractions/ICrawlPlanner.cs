using Didibood.LocationAccess.Application.Crawler;

namespace Didibood.LocationAccess.Application.Abstractions;

public interface ICrawlPlanner
{
    Task<IReadOnlyList<CrawlCell>> PlanAsync(CrawlPlanRequest request, CancellationToken ct = default);
}
