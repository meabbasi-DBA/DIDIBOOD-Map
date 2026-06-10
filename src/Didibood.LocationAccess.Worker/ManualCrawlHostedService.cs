using System.Collections.Concurrent;
using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Worker;

public sealed class ManualCrawlHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ManualCrawlHostedService> logger) : BackgroundService
{
    private static readonly ConcurrentDictionary<Guid, byte> Active = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ManualCrawlHostedService started");

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

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task PollManualExecutionsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendingIds = await db.CrawlJobExecutions
            .AsNoTracking()
            .Where(e => e.Status == "running"
                        && e.TriggeredBy.StartsWith("admin_manual")
                        && e.RequestCount == 0
                        && e.CellsProcessed == 0
                        && e.CellsFailed == 0)
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

            if (execution is null || execution.Status != "running")
                return;

            var job = await db.CrawlJobs.FirstOrDefaultAsync(j => j.Id == execution.CrawlJobId);
            if (job is null)
            {
                await jobRepo.FailExecutionAsync(executionId, "Crawl job definition not found.", CancellationToken.None);
                return;
            }

            logger.LogInformation(
                "Starting manual crawl execution {ExecutionId} ({Trigger})",
                executionId,
                execution.TriggeredBy);

            var summary = await runner.RunManualExecutionAsync(execution, job, CancellationToken.None);
            await jobRepo.CompleteExecutionAsync(executionId, summary, CancellationToken.None);

            logger.LogInformation(
                "Manual crawl {ExecutionId} completed: new={New}, updated={Updated}, requests={Requests}",
                executionId,
                summary.NewRecords,
                summary.UpdatedRecords,
                summary.TotalRequests);
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
