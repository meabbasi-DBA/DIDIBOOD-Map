namespace Didibood.LocationAccess.Application.Abstractions;

/// <summary>
/// Ephemeral crawl error messages shared across Worker and Admin (file-backed, not in DB).
/// Cleared when the execution finishes.
/// </summary>
public interface ICrawlLiveTelemetry
{
    void SetLiveError(Guid executionId, string? error);
    string? GetLiveError(Guid executionId);
    void SetQueuedCells(Guid executionId, IReadOnlyList<long> h3Indexes);
    void SetCurrentCell(Guid executionId, long h3Index, short categoryId, string searchTerm);
    void RecordCellResult(Guid executionId, long h3Index, bool succeeded, string? error);
    CrawlLiveSnapshot GetSnapshot(Guid executionId);
    void Clear(Guid executionId);
}

public sealed record CrawlLiveCell(long H3Index, DateTimeOffset UpdatedAt, short? CategoryId = null, string? SearchTerm = null, string? Error = null);

public sealed record CrawlLiveSnapshot(
    string? Error,
    CrawlLiveCell? CurrentCell,
    IReadOnlyList<long> QueuedCells,
    IReadOnlyList<CrawlLiveCell> RecentCells,
    IReadOnlyList<CrawlLiveCell> FailedCells);
