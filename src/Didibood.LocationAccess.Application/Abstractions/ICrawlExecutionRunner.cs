using Didibood.LocationAccess.Application.Crawler;
using Didibood.LocationAccess.Domain.Entities;

namespace Didibood.LocationAccess.Application.Abstractions;

public interface ICrawlExecutionRunner
{
    Task<CrawlExecutionSummary> RunJobAsync(CrawlJob job, CancellationToken ct = default);

    Task<CrawlExecutionSummary> RunManualExecutionAsync(
        CrawlJobExecution execution,
        CrawlJob job,
        CancellationToken ct = default);
}
