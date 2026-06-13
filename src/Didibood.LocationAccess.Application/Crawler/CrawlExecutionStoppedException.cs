using Didibood.LocationAccess.Application.Crawler;

namespace Didibood.LocationAccess.Application.Crawler;

public sealed class CrawlExecutionStoppedException(string status)
    : OperationCanceledException($"Crawl execution stopped with status '{status}'.")
{
    public string ExecutionStatus { get; } = status;
}
