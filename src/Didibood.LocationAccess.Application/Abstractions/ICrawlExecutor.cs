using Didibood.LocationAccess.Application.Crawler;

namespace Didibood.LocationAccess.Application.Abstractions;

public interface ICrawlExecutor
{
    Task<CrawlExecutionResult> ExecuteAsync(CrawlCell cell, CancellationToken ct = default);
}
