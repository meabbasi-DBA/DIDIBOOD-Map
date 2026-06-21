using Didibood.LocationAccess.Application.Crawler;

namespace Didibood.LocationAccess.Application.Abstractions;

public interface ICrawlPolicyGate
{
    Task<CrawlGateLease> TryAcquireAsync(
        Guid executionId,
        string source,
        CrawlCell cell,
        CancellationToken ct = default);

    Task RecordResultAsync(
        CrawlGateLease lease,
        CrawlExecutionResult result,
        long durationMs,
        CancellationToken ct = default);
}

public sealed record CrawlGateLease(
    bool Accepted,
    string Reason,
    Guid ExecutionId,
    string Source,
    long H3Index,
    int? GridNumber,
    short CategoryId,
    string SearchTerm,
    DateTimeOffset Timestamp);
