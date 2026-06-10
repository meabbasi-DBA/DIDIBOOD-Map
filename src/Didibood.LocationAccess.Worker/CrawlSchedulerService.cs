using Cronos;
using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Crawler;
using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Domain.Exceptions;

namespace Didibood.LocationAccess.Worker;

public sealed class CrawlSchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<CrawlSchedulerService> logger) : BackgroundService
{
    private readonly Dictionary<Guid, DateTimeOffset> _lastRunTimes = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CrawlSchedulerService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled error in CrawlSchedulerService tick");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("CrawlSchedulerService stopped");
    }

    private async Task ProcessDueJobsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var jobRepo = scope.ServiceProvider.GetRequiredService<ICrawlJobRepository>();
        var configStore = scope.ServiceProvider.GetRequiredService<ISystemConfigurationStore>();
        var planner = scope.ServiceProvider.GetRequiredService<ICrawlPlanner>();
        var executor = scope.ServiceProvider.GetRequiredService<ICrawlExecutor>();
        var h3Coverage = scope.ServiceProvider.GetRequiredService<IH3CoverageRepository>();

        var jobs = await jobRepo.GetEnabledJobsAsync(ct);
        var parallelism = await configStore.GetAsync("crawl.parallelism", 2, ct);
        var staleThresholdDays = await configStore.GetAsync("crawl.stale_threshold_days", 30, ct);
        var markedStale = await h3Coverage.MarkStaleCellsAsync(staleThresholdDays, ct);
        if (markedStale > 0)
            logger.LogInformation("Marked {Count} H3 cells as stale", markedStale);

        var now = DateTimeOffset.UtcNow;

        foreach (var job in jobs)
        {
            if (!IsDue(job.Id, job.CronExpression, now))
                continue;

            logger.LogInformation("Starting crawl job {JobId} ({JobName})", job.Id, job.Name);

            CrawlJobExecution? execution = null;
            try
            {
                execution = await jobRepo.StartExecutionAsync(job.Id, "scheduler", ct);
                var summary = await ExecuteJobAsync(planner, executor, h3Coverage, job, parallelism, ct);
                await jobRepo.CompleteExecutionAsync(execution.Id, summary, ct);

                _lastRunTimes[job.Id] = now;

                logger.LogInformation(
                    "Crawl job {JobId} completed: requests={Requests}, new={New}, updated={Updated}, failed={Failed}, cells={Cells}/{Total}",
                    job.Id, summary.TotalRequests, summary.NewRecords, summary.UpdatedRecords,
                    summary.FailedRecords, summary.CellsProcessed, summary.CellsProcessed + summary.CellsFailed);
            }
            catch (NeshanQuotaExceededException ex)
            {
                logger.LogWarning(
                    ex,
                    "Neshan quota exceeded during job {JobId}. Halting remaining jobs this cycle.",
                    job.Id);

                if (execution is not null)
                    await SafeFailAsync(jobRepo, execution.Id, $"Quota exceeded: {ex.Message}", ct);

                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Crawl job {JobId} failed: {Error}", job.Id, ex.Message);

                if (execution is not null)
                    await SafeFailAsync(jobRepo, execution.Id, ex.Message, ct);

                _lastRunTimes[job.Id] = now;
            }
        }
    }

    private async Task<CrawlExecutionSummary> ExecuteJobAsync(
        ICrawlPlanner planner,
        ICrawlExecutor executor,
        IH3CoverageRepository h3Coverage,
        CrawlJob job,
        int parallelism,
        CancellationToken ct)
    {
        var staleOnly = job.Name.Contains("stale", StringComparison.OrdinalIgnoreCase);
        var planRequest = new CrawlPlanRequest(
            Resolution: job.H3Resolution,
            CategoryIds: job.TargetCategoryId.HasValue ? [job.TargetCategoryId.Value] : null,
            StaleOnly: staleOnly);

        var cells = await planner.PlanAsync(planRequest, ct);
        var summary = new CrawlExecutionSummary();
        var effectiveParallelism = Math.Max(1, Math.Min(parallelism, job.MaxParallelCells));

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

                    if (result.Error is null) summary.CellsProcessed++;
                    else summary.CellsFailed++;
                }
            }
            catch (NeshanQuotaExceededException)
            {
                lock (summary) { summary.CellsFailed++; }
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cell H3={H3Index} failed during job {JobId}", cell.H3Index, job.Id);
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

    private bool IsDue(Guid jobId, string? cronExpression, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return false;

        try
        {
            var schedule = CronExpression.Parse(cronExpression, CronFormat.Standard);
            var lastRun = _lastRunTimes.TryGetValue(jobId, out var lr)
                ? lr
                : DateTimeOffset.UtcNow.AddDays(-1);

            var next = schedule.GetNextOccurrence(lastRun.UtcDateTime, TimeZoneInfo.Utc);
            return next.HasValue && next.Value <= now.UtcDateTime;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invalid cron expression '{Cron}' for job {JobId}", cronExpression, jobId);
            return false;
        }
    }

    private async Task SafeFailAsync(ICrawlJobRepository jobRepo, Guid executionId, string error, CancellationToken ct)
    {
        try
        {
            await jobRepo.FailExecutionAsync(executionId, error, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record execution failure for {ExecutionId}", executionId);
        }
    }
}
