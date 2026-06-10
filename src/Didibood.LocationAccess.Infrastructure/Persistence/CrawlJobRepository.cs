using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Crawler;
using Didibood.LocationAccess.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Infrastructure.Persistence;

public sealed class CrawlJobRepository(AppDbContext db) : ICrawlJobRepository
{
    public async Task<IReadOnlyList<CrawlJob>> GetEnabledJobsAsync(CancellationToken ct)
    {
        return await db.CrawlJobs
            .Where(j => j.IsEnabled)
            .Include(j => j.TargetCategory)
            .ToListAsync(ct);
    }

    public async Task<CrawlJobExecution> StartExecutionAsync(Guid jobId, string triggeredBy, CancellationToken ct)
    {
        var execution = new CrawlJobExecution
        {
            Id = Guid.NewGuid(),
            CrawlJobId = jobId,
            Status = "running",
            StartedAt = DateTimeOffset.UtcNow,
            TriggeredBy = triggeredBy
        };
        db.CrawlJobExecutions.Add(execution);
        await db.SaveChangesAsync(ct);
        return execution;
    }

    public async Task CompleteExecutionAsync(Guid executionId, CrawlExecutionSummary summary, CancellationToken ct)
    {
        var execution = await db.CrawlJobExecutions.FindAsync([executionId], ct)
            ?? throw new InvalidOperationException($"Execution {executionId} not found.");

        var endedAt = DateTimeOffset.UtcNow;
        execution.Status = "completed";
        execution.EndedAt = endedAt;
        execution.DurationMs = (long)(endedAt - execution.StartedAt).TotalMilliseconds;
        execution.RequestCount = summary.TotalRequests;
        execution.NewRecords = summary.NewRecords;
        execution.UpdatedRecords = summary.UpdatedRecords;
        execution.FailedRecords = summary.FailedRecords;
        execution.CellsProcessed = summary.CellsProcessed;
        execution.CellsFailed = summary.CellsFailed;

        await db.SaveChangesAsync(ct);
    }

    public async Task FailExecutionAsync(Guid executionId, string error, CancellationToken ct)
    {
        var execution = await db.CrawlJobExecutions.FindAsync([executionId], ct)
            ?? throw new InvalidOperationException($"Execution {executionId} not found.");

        var endedAt = DateTimeOffset.UtcNow;
        execution.Status = "failed";
        execution.EndedAt = endedAt;
        execution.DurationMs = (long)(endedAt - execution.StartedAt).TotalMilliseconds;
        execution.ErrorSummary = error.Length > 2000 ? error[..2000] : error;

        await db.SaveChangesAsync(ct);
    }
}
