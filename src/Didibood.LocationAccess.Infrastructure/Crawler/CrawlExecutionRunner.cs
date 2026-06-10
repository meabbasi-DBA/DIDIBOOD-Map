using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Crawler;
using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Didibood.LocationAccess.Infrastructure.Crawler;

public sealed class CrawlExecutionRunner(
    ICrawlPlanner planner,
    ICrawlExecutor executor,
    IH3CoverageRepository h3Coverage,
    ISystemConfigurationStore configStore,
    ILogger<CrawlExecutionRunner> logger) : ICrawlExecutionRunner
{
    public async Task<CrawlExecutionSummary> RunJobAsync(CrawlJob job, CancellationToken ct = default)
    {
        var staleOnly = job.Name.Contains("stale", StringComparison.OrdinalIgnoreCase);
        var plan = new CrawlPlanRequest(
            Resolution: job.H3Resolution,
            CategoryIds: job.TargetCategoryId.HasValue ? [job.TargetCategoryId.Value] : null,
            StaleOnly: staleOnly);

        var parallelism = await configStore.GetAsync("crawl.parallelism", 2, ct);
        return await ExecutePlanAsync(plan, Math.Min(parallelism, job.MaxParallelCells), job.Id, ct);
    }

    public async Task<CrawlExecutionSummary> RunManualExecutionAsync(
        CrawlJobExecution execution,
        CrawlJob job,
        CancellationToken ct = default)
    {
        var plan = ParseManualPlan(execution.TriggeredBy, job);
        var parallelism = await configStore.GetAsync("crawl.parallelism", 2, ct);
        return await ExecutePlanAsync(plan, Math.Min(parallelism, job.MaxParallelCells), job.Id, ct);
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
        CancellationToken ct)
    {
        var cells = await planner.PlanAsync(plan, ct);
        var summary = new CrawlExecutionSummary();
        var effectiveParallelism = Math.Max(1, maxParallelism);

        logger.LogInformation(
            "Crawl plan for job {JobId}: {TaskCount} tasks, parallelism={Parallelism}",
            jobId,
            cells.Count,
            effectiveParallelism);

        if (cells.Count == 0)
        {
            logger.LogWarning("Crawl plan for job {JobId} produced zero tasks", jobId);
            return summary;
        }

        using var semaphore = new SemaphoreSlim(effectiveParallelism, effectiveParallelism);

        var tasks = cells.Select(async cell =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var result = await executor.ExecuteAsync(cell, ct);

                await h3Coverage.RecordCrawlOutcomeAsync(
                    cell.H3Index,
                    result.NewRecords,
                    result.UpdatedRecords,
                    result.FailedRecords,
                    result.Error is null,
                    result.Error,
                    ct);

                lock (summary)
                {
                    summary.TotalRequests += result.RequestCount;
                    summary.NewRecords += result.NewRecords;
                    summary.UpdatedRecords += result.UpdatedRecords;
                    summary.FailedRecords += result.FailedRecords;

                    if (result.Error is null)
                        summary.CellsProcessed++;
                    else
                        summary.CellsFailed++;
                }
            }
            catch (NeshanQuotaExceededException)
            {
                lock (summary) { summary.CellsFailed++; }
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cell H3={H3Index} failed during job {JobId}", cell.H3Index, jobId);
                lock (summary) { summary.CellsFailed++; }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return summary;
    }
}
