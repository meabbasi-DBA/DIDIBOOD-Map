using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Infrastructure.Persistence;

public sealed class H3CoverageRepository(AppDbContext db) : IH3CoverageRepository
{
    public async Task RecordCrawlOutcomeAsync(
        long h3Index,
        int newRecords,
        int updatedRecords,
        int failedRecords,
        bool apiSucceeded,
        string? error,
        CancellationToken ct = default)
    {
        var cell = await db.H3CoverageCells
            .FirstOrDefaultAsync(c => c.H3Index == h3Index, ct);

        if (cell is null)
            return;

        var now = DateTimeOffset.UtcNow;
        cell.LastCrawlAt = now;
        cell.UpdatedAt = now;
        cell.PoiCount += newRecords + updatedRecords;

        if (apiSucceeded)
        {
            cell.LastSuccessAt = now;
            cell.Status = "success";
        }
        else if (cell.Status != "success")
        {
            cell.FailureCount++;
            cell.FailureReason = error;
            cell.Status = "failed";
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<int> MarkStaleCellsAsync(int staleThresholdDays, CancellationToken ct = default)
    {
        if (staleThresholdDays <= 0)
            return 0;

        var cutoff = DateTimeOffset.UtcNow.AddDays(-staleThresholdDays);
        var staleCells = await db.H3CoverageCells
            .Where(c => c.Status == "success" && c.LastSuccessAt != null && c.LastSuccessAt < cutoff)
            .ToListAsync(ct);

        if (staleCells.Count == 0)
            return 0;

        var now = DateTimeOffset.UtcNow;
        foreach (var cell in staleCells)
        {
            cell.Status = "stale";
            cell.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return staleCells.Count;
    }
}
