using Didibood.LocationAccess.Application.Abstractions;

using Didibood.LocationAccess.Application.Crawler;

using Didibood.LocationAccess.Domain;

using Didibood.LocationAccess.Domain.Entities;

using Microsoft.EntityFrameworkCore;



namespace Didibood.LocationAccess.Infrastructure.Persistence;



public sealed class CrawlJobRepository(AppDbContext db, ICrawlLiveTelemetry liveTelemetry) : ICrawlJobRepository

{

    public async Task<IReadOnlyList<CrawlJob>> GetEnabledJobsAsync(CancellationToken ct)

    {

        return await db.CrawlJobs

            .Where(j => j.IsEnabled && j.JobType != CrawlJobKinds.Manual)

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



    public async Task<string?> GetExecutionStatusAsync(Guid executionId, CancellationToken ct)

    {

        return await db.CrawlJobExecutions

            .AsNoTracking()

            .Where(e => e.Id == executionId)

            .Select(e => e.Status)

            .FirstOrDefaultAsync(ct);

    }



    public async Task UpdateProgressAsync(

        Guid executionId,

        CrawlExecutionSummary summary,

        int totalTasksPlanned,

        CancellationToken ct)

    {

        var execution = await db.CrawlJobExecutions.FindAsync([executionId], ct);

        if (execution is null || execution.Status is not ("running" or "queued"))

            return;



        execution.RequestCount = summary.TotalRequests;

        execution.NewRecords = summary.NewRecords;

        execution.UpdatedRecords = summary.UpdatedRecords;

        execution.FailedRecords = summary.FailedRecords;

        execution.CellsProcessed = summary.CellsProcessed;

        execution.CellsFailed = summary.CellsFailed;

        if (totalTasksPlanned > 0)

            execution.TotalTasksPlanned = totalTasksPlanned;



        await db.SaveChangesAsync(ct);

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

        execution.ErrorSummary = null;



        await db.SaveChangesAsync(ct);

        liveTelemetry.Clear(executionId);

    }



    public async Task FailExecutionAsync(Guid executionId, string error, CancellationToken ct)

    {

        var execution = await db.CrawlJobExecutions.FindAsync([executionId], ct)

            ?? throw new InvalidOperationException($"Execution {executionId} not found.");



        var endedAt = DateTimeOffset.UtcNow;

        execution.Status = "failed";

        execution.EndedAt = endedAt;

        execution.DurationMs = (long)(endedAt - execution.StartedAt).TotalMilliseconds;

        execution.ErrorSummary = null;



        await db.SaveChangesAsync(ct);

        liveTelemetry.Clear(executionId);

    }



    public async Task StopExecutionAsync(

        Guid executionId,

        string status,

        CrawlExecutionSummary summary,

        CancellationToken ct)

    {

        var execution = await db.CrawlJobExecutions.FindAsync([executionId], ct)

            ?? throw new InvalidOperationException($"Execution {executionId} not found.");



        var endedAt = DateTimeOffset.UtcNow;

        execution.Status = status;

        execution.EndedAt = status == "paused" ? null : endedAt;

        execution.DurationMs = status == "paused"

            ? null

            : (long)(endedAt - execution.StartedAt).TotalMilliseconds;

        execution.RequestCount = summary.TotalRequests;

        execution.NewRecords = summary.NewRecords;

        execution.UpdatedRecords = summary.UpdatedRecords;

        execution.FailedRecords = summary.FailedRecords;

        execution.CellsProcessed = summary.CellsProcessed;

        execution.CellsFailed = summary.CellsFailed;

        execution.ErrorSummary = null;



        await db.SaveChangesAsync(ct);



        if (status is "cancelled" or "completed")

            liveTelemetry.Clear(executionId);

    }

}


