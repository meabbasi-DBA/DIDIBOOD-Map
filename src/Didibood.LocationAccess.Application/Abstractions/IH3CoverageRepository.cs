namespace Didibood.LocationAccess.Application.Abstractions;

public interface IH3CoverageRepository
{
    Task RecordCrawlOutcomeAsync(
        long h3Index,
        int newRecords,
        int updatedRecords,
        int failedRecords,
        bool apiSucceeded,
        string? error,
        CancellationToken ct = default);

    Task<int> MarkStaleCellsAsync(int staleThresholdDays, CancellationToken ct = default);
}
