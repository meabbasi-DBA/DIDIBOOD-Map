using System.Collections.Concurrent;
using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Crawler;
using Didibood.LocationAccess.Domain.Exceptions;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Worker;

public sealed class ManualCrawlHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ManualCrawlHostedService> logger) : BackgroundService
{
    private static readonly ConcurrentDictionary<Guid, byte> Active = new();
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ManualCrawlHostedService started (poll every {Seconds}s)", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollManualExecutionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error polling manual crawl executions");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task PollManualExecutionsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendingIds = await db.CrawlJobExecutions
            .AsNoTracking()
            .Where(e => (e.Status == "queued" || e.Status == "running")
                        && e.TriggeredBy.StartsWith("admin_manual"))
            .Select(e => e.Id)
            .ToListAsync(ct);

        foreach (var executionId in pendingIds)
        {
            if (!Active.TryAdd(executionId, 0))
                continue;

            _ = RunExecutionAsync(executionId);
        }
    }

    private async Task RunExecutionAsync(Guid executionId)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var runner = scope.ServiceProvider.GetRequiredService<ICrawlExecutionRunner>();
            var jobRepo = scope.ServiceProvider.GetRequiredService<ICrawlJobRepository>();

            var execution = await db.CrawlJobExecutions
                .FirstOrDefaultAsync(e => e.Id == executionId);

            if (execution is null)
                return;

            if (execution.Status == "queued")
            {
                execution.Status = "running";
                await db.SaveChangesAsync(CancellationToken.None);
            }
            else if (execution.Status != "running")
            {
                return;
            }

            var job = await db.CrawlJobs.FirstOrDefaultAsync(j => j.Id == execution.CrawlJobId);
            if (job is null)
            {
                await jobRepo.FailExecutionAsync(executionId, "Crawl job definition not found.", CancellationToken.None);
                return;
            }

            logger.LogInformation(
                "Starting manual crawl execution {ExecutionId} ({Trigger}) using job {JobName}",
                executionId,
                execution.TriggeredBy,
                job.Name);

            var summary = await runner.RunManualExecutionAsync(execution, job, CancellationToken.None);
            await jobRepo.CompleteExecutionAsync(executionId, summary, CancellationToken.None);

            logger.LogInformation(
                "Manual crawl {ExecutionId} completed: new={New}, updated={Updated}, requests={Requests}",
                executionId,
                summary.NewRecords,
                summary.UpdatedRecords,
                summary.TotalRequests);
        }
        catch (CrawlExecutionStoppedException ex)
        {
            logger.LogInformation("Manual crawl {ExecutionId} stopped ({Status})", executionId, ex.ExecutionStatus);
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var jobRepo = scope.ServiceProvider.GetRequiredService<ICrawlJobRepository>();
                var execution = await db.CrawlJobExecutions.AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Id == executionId);

                if (execution is null)
                    return;

                var summary = new CrawlExecutionSummary
                {
                    TotalRequests = execution.RequestCount,
                    NewRecords = execution.NewRecords,
                    UpdatedRecords = execution.UpdatedRecords,
                    FailedRecords = execution.FailedRecords,
                    CellsProcessed = execution.CellsProcessed,
                    CellsFailed = execution.CellsFailed
                };

                await jobRepo.StopExecutionAsync(executionId, ex.ExecutionStatus, summary, CancellationToken.None);
            }
            catch (Exception inner)
            {
                logger.LogError(inner, "Failed to record stop for execution {ExecutionId}", executionId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Manual crawl execution {ExecutionId} failed", executionId);
            try
            {
                using var scope = scopeFactory.CreateScope();
                var jobRepo = scope.ServiceProvider.GetRequiredService<ICrawlJobRepository>();
                await jobRepo.FailExecutionAsync(executionId, ex.Message, CancellationToken.None);
            }
            catch (Exception inner)
            {
                logger.LogError(inner, "Failed to record failure for execution {ExecutionId}", executionId);
            }
        }
        finally
        {
            Active.TryRemove(executionId, out _);
        }
    }
}
