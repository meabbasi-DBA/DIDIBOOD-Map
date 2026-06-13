using Cronos;
using Didibood.LocationAccess.Application.Abstractions;
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
        var runner = scope.ServiceProvider.GetRequiredService<ICrawlExecutionRunner>();
        var h3Coverage = scope.ServiceProvider.GetRequiredService<IH3CoverageRepository>();
        var configStore = scope.ServiceProvider.GetRequiredService<ISystemConfigurationStore>();

        var jobs = await jobRepo.GetEnabledJobsAsync(ct);
        var staleThresholdDays = await configStore.GetAsync("crawl.stale_threshold_days", 30, ct);
        var markedStale = await h3Coverage.MarkStaleCellsAsync(staleThresholdDays, ct);
        if (markedStale > 0)
            logger.LogInformation("Marked {Count} H3 cells as stale", markedStale);

        var now = DateTimeOffset.UtcNow;

        foreach (var job in jobs)
        {
            if (!IsDue(job.Id, job.CronExpression, now))
                continue;

            logger.LogInformation("Starting scheduled crawl job {JobId} ({JobName})", job.Id, job.Name);

            var execution = await jobRepo.StartExecutionAsync(job.Id, "scheduler", ct);
            try
            {
                var summary = await runner.RunJobAsync(job, execution.Id, ct);
                await jobRepo.CompleteExecutionAsync(execution.Id, summary, ct);
                _lastRunTimes[job.Id] = now;

                logger.LogInformation(
                    "Crawl job {JobId} completed: requests={Requests}, new={New}, updated={Updated}",
                    job.Id, summary.TotalRequests, summary.NewRecords, summary.UpdatedRecords);
            }
            catch (NeshanQuotaExceededException ex)
            {
                logger.LogWarning(ex, "Neshan quota exceeded during job {JobId}", job.Id);
                await SafeFailAsync(jobRepo, execution.Id, $"Quota exceeded: {ex.Message}", ct);
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Crawl job {JobId} failed", job.Id);
                await SafeFailAsync(jobRepo, execution.Id, ex.Message, ct);
                _lastRunTimes[job.Id] = now;
            }
        }
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

    private static async Task SafeFailAsync(ICrawlJobRepository jobRepo, Guid executionId, string error, CancellationToken ct)
    {
        try
        {
            await jobRepo.FailExecutionAsync(executionId, error, CancellationToken.None);
        }
        catch
        {
            // best effort
        }
    }
}
