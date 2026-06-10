using Didibood.LocationAccess.Application.Crawler;
using Didibood.LocationAccess.Domain.Entities;

namespace Didibood.LocationAccess.Application.Abstractions;

public interface ICrawlJobRepository
{
    Task<IReadOnlyList<CrawlJob>> GetEnabledJobsAsync(CancellationToken ct);
    Task<CrawlJobExecution> StartExecutionAsync(Guid jobId, string triggeredBy, CancellationToken ct);
    Task CompleteExecutionAsync(Guid executionId, CrawlExecutionSummary summary, CancellationToken ct);
    Task FailExecutionAsync(Guid executionId, string error, CancellationToken ct);
}
