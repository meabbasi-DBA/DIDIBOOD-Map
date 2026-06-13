using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Crawler;
using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Didibood.LocationAccess.Infrastructure.Crawler;

public sealed class CrawlExecutionRunner(
    ICrawlPlanner planner,
    IServiceScopeFactory scopeFactory,
    ISystemConfigurationStore configStore,
    ICrawlLiveTelemetry liveTelemetry,
    ILogger<CrawlExecutionRunner> logger) : ICrawlExecutionRunner
{
    private const int ProgressFlushInterval = 1;

    public async Task<CrawlExecutionSummary> RunJobAsync(
        CrawlJob job,
        Guid executionId,
        CancellationToken ct = default)
    {
        var staleOnly = job.Name.Contains("stale", StringComparison.OrdinalIgnoreCase);
        var plan = new CrawlPlanRequest(
            Resolution: job.H3Resolution,
            CategoryIds: job.TargetCategoryId.HasValue ? [job.TargetCategoryId.Value] : null,
            StaleOnly: staleOnly);

        var parallelism = await configStore.GetAsync("crawl.parallelism", 2, ct);
        return await ExecutePlanAsync(
            plan,
            Math.Min(parallelism, job.MaxParallelCells),
            job.Id,
            executionId,
            ct);
    }

    public async Task<CrawlExecutionSummary> RunManualExecutionAsync(
        CrawlJobExecution execution,
        CrawlJob job,
        CancellationToken ct = default)
    {
        var plan = ParseManualPlan(execution.TriggeredBy, job);
        var parallelism = await configStore.GetAsync("crawl.parallelism", 2, ct);
        return await ExecutePlanAsync(
            plan,
            Math.Min(parallelism, job.MaxParallelCells),
            job.Id,
            execution.Id,
            ct);
    }

    public static CrawlPlanRequest ParseManualPlan(string triggeredBy, CrawlJob job)
    {
        var parts = triggeredBy.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var mode = parts.Length > 1 ? parts[1] : "full";

        return mode switch
        {
            "failed" => new CrawlPlanRequest(job.H3Resolution, null, StaleOnly: true),
            "categories" when parts.Length > 2 => new CrawlPlanRequest(
                job.H3Resolution,
                parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(short.Parse)
                    .ToArray(),
                StaleOnly: false),
            _ => new CrawlPlanRequest(job.H3Resolution, null, StaleOnly: false)
        };
    }

    private async Task<CrawlExecutionSummary> ExecutePlanAsync(
        CrawlPlanRequest plan,
        int maxParallelism,
        Guid jobId,
        Guid executionId,
        CancellationToken ct)
    {
        var cells = await planner.PlanAsync(plan, ct);
        var summary = new CrawlExecutionSummary();
        var effectiveParallelism = Math.Max(1, maxParallelism);
        var totalTasks = cells.Count;
        var tasksCompleted = 0;

        logger.LogInformation(
            "Crawl plan for job {JobId} execution {ExecutionId}: {TaskCount} tasks, parallelism={Parallelism}",
            jobId,
            executionId,
            cells.Count,
            effectiveParallelism);

        if (cells.Count == 0)
        {
            logger.LogWarning("Crawl plan for job {JobId} produced zero tasks", jobId);
            await FlushProgressAsync(executionId, summary, totalTasks, ct);
            return summary;
        }

        await FlushProgressAsync(executionId, summary, totalTasks, ct);

        using var semaphore = new SemaphoreSlim(effectiveParallelism, effectiveParallelism);

        var tasks = cells.Select(async cell =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                await EnsureRunningAsync(executionId, ct);

                using var scope = scopeFactory.CreateScope();
                var executor = scope.ServiceProvider.GetRequiredService<ICrawlExecutor>();
                var h3Coverage = scope.ServiceProvider.GetRequiredService<IH3CoverageRepository>();

                var result = await executor.ExecuteAsync(cell, ct);

                await h3Coverage.RecordCrawlOutcomeAsync(
                    cell.H3Index,
                    result.NewRecords,
                    result.UpdatedRecords,
                    result.FailedRecords,
                    result.Error is null,
                    result.Error,
                    ct);

                var shouldFlush = false;
                lock (summary)
                {
                    summary.TotalRequests += result.RequestCount;
                    summary.NewRecords += result.NewRecords;
                    summary.UpdatedRecords += result.UpdatedRecords;
                    summary.FailedRecords += result.FailedRecords;

                    if (result.Error is null)
                        summary.CellsProcessed++;
                    else
                    {
                        summary.CellsFailed++;
                        summary.LastLiveError = result.Error;
                    }

                    tasksCompleted++;
                    shouldFlush = tasksCompleted % ProgressFlushInterval == 0;
                }

                if (shouldFlush)
                    await FlushProgressAsync(executionId, summary, totalTasks, ct);
            }
            catch (CrawlExecutionStoppedException)
            {
                throw;
            }
            catch (NeshanQuotaExceededException ex)
            {
                var shouldFlush = false;
                lock (summary)
                {
                    summary.CellsFailed++;
                    summary.LastLiveError = ex.Message;
                    tasksCompleted++;
                    shouldFlush = true;
                }

                if (shouldFlush)
                    await FlushProgressAsync(executionId, summary, totalTasks, ct);
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cell H3={H3Index} failed during job {JobId}", cell.H3Index, jobId);
                var shouldFlush = false;
                lock (summary)
                {
                    summary.CellsFailed++;
                    summary.TotalRequests++;
                    summary.LastLiveError = ex.Message;
                    tasksCompleted++;
                    shouldFlush = true;
                }

                if (shouldFlush)
                    await FlushProgressAsync(executionId, summary, totalTasks, ct);
            }
            finally
            {
                semaphore.Release();
            }
        });

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (CrawlExecutionStoppedException)
        {
            await FlushProgressAsync(executionId, summary, totalTasks, CancellationToken.None);
            throw;
        }

        liveTelemetry.Clear(executionId);
        return summary;
    }

    private async Task EnsureRunningAsync(Guid executionId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<ICrawlJobRepository>();
        var status = await jobRepo.GetExecutionStatusAsync(executionId, ct);

        if (status is "cancelled" or "paused")
            throw new CrawlExecutionStoppedException(status);
    }

    private async Task FlushProgressAsync(
        Guid executionId,
        CrawlExecutionSummary summary,
        int totalTasksPlanned,
        CancellationToken ct)
    {
        CrawlExecutionSummary snapshot;
        string? liveError;
        lock (summary)
        {
            snapshot = new CrawlExecutionSummary
            {
                TotalRequests = summary.TotalRequests,
                NewRecords = summary.NewRecords,
                UpdatedRecords = summary.UpdatedRecords,
                FailedRecords = summary.FailedRecords,
                CellsProcessed = summary.CellsProcessed,
                CellsFailed = summary.CellsFailed,
                LastLiveError = summary.LastLiveError
            };
            liveError = summary.LastLiveError;
        }

        if (!string.IsNullOrWhiteSpace(liveError))
            liveTelemetry.SetLiveError(executionId, liveError);

        using var scope = scopeFactory.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<ICrawlJobRepository>();
        await jobRepo.UpdateProgressAsync(executionId, snapshot, totalTasksPlanned, ct);
    }
}
