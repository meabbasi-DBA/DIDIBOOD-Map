using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Domain;
using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Admin.Pages;

public class IndexModel(AppDbContext db, ICrawlLiveTelemetry liveTelemetry) : PageModel
{
    public int TotalPois        { get; private set; }
    public int ActivePois       { get; private set; }
    public int TotalCategories  { get; private set; }
    public int RunningJobs      { get; private set; }
    public int H3Total          { get; private set; }
    public int H3Pending        { get; private set; }
    public int H3Success        { get; private set; }
    public int H3Failed         { get; private set; }
    public int H3Stale          { get; private set; }
    public int H3Crawled        { get; private set; }
    public int H3Remaining      { get; private set; }
    public double H3SuccessRate { get; private set; }
    public int ApiCallsUsedThisHour { get; private set; }
    public int ApiCallsRemainingThisHour { get; private set; }
    public int ApiCallLimitPerHour { get; private set; }
    public string SchedulerState { get; private set; } = "idle";
    public CrawlTimelineRow? LastCrawl { get; private set; }
    public List<GridQueueRow> NextQueuedGrids { get; private set; } = [];
    public List<GridDashboardRow> DashboardGrids { get; private set; } = [];
    public int? CurrentRunningGridNumber { get; private set; }
    public int? LastCrawledGridNumber => LastCrawl?.GridNumber;
    public DateTimeOffset? LastCrawledDatetime => LastCrawl?.Timestamp;
    public DateTimeOffset? LastSuccessfulCrawl { get; private set; }
    public bool DbHealthy       { get; private set; }
    public bool PostgisHealthy  { get; private set; }
    public DateTimeOffset LastRefresh { get; } = DateTimeOffset.UtcNow;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadStatsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnGetCrawlStatusAsync(CancellationToken cancellationToken)
    {
        var rateLimit = await LoadRateLimitStatusAsync(cancellationToken);
        var active = await db.CrawlJobExecutions
            .AsNoTracking()
            .Where(e => e.Status == "running" || e.Status == "paused" || e.Status == "queued")
            .OrderByDescending(e => e.StartedAt)
            .Join(
                db.CrawlJobs.AsNoTracking(),
                e => e.CrawlJobId,
                j => j.Id,
                (e, j) => new { Execution = e, Job = j })
            .FirstOrDefaultAsync(cancellationToken);

        var runningCount = await db.CrawlJobExecutions
            .CountAsync(e => e.Status == "running" || e.Status == "queued", cancellationToken);

        var pausedCount = await db.CrawlJobExecutions
            .CountAsync(e => e.Status == "paused", cancellationToken);

        var h3Total = await db.H3CoverageCells.CountAsync(cancellationToken);
        var h3Success = await db.H3CoverageCells.CountAsync(c => c.Status == "success", cancellationToken);
        var h3Crawled = await db.H3CoverageCells.CountAsync(c => c.LastCrawlAt != null, cancellationToken);
        var h3Failed = await db.H3CoverageCells.CountAsync(c => c.Status == "failed", cancellationToken);
        var h3Remaining = Math.Max(0, h3Total - h3Crawled);
        var h3SuccessRate = h3Crawled == 0 ? 0 : Math.Round(h3Success * 100.0 / h3Crawled, 1);
        var nextQueuedGrids = await LoadNextQueuedGridsAsync(cancellationToken);
        var lastCrawl = await LoadLastCrawlAsync(cancellationToken);
        var activeGridContext = active is null
            ? ActiveGridContext.Empty
            : await LoadActiveGridContextAsync(active.Execution.Id, cancellationToken);
        var dashboardGrids = await LoadDashboardGridsAsync(activeGridContext, cancellationToken);

        if (active is null)
        {
            return new JsonResult(new
            {
                isActive = false,
                runningCount,
                pausedCount,
                h3Total,
                h3Failed,
                h3Success,
                h3Crawled,
                h3Remaining,
                h3SuccessRate,
                schedulerState = rateLimit.Remaining <= 0 ? "blocked" : "idle",
                nextQueuedGrids,
                lastCrawl,
                currentRunningGridNumber = activeGridContext.CurrentGridNumber,
                lastCrawledGridNumber = lastCrawl?.GridNumber,
                lastCrawledDatetime = lastCrawl?.Timestamp,
                dashboardGrids,
                apiCallsUsedThisHour = rateLimit.Used,
                apiCallsRemainingThisHour = rateLimit.Remaining,
                apiCallLimitPerHour = rateLimit.Limit
            });
        }

        var e = active.Execution;
        var live = liveTelemetry.GetSnapshot(e.Id);
        var runningGridNumber = live.CurrentCell?.H3Index is null
            ? null
            : await LoadGridNumberAsync(live.CurrentCell.H3Index, cancellationToken);
        var elapsedSec = Math.Max(1, (DateTimeOffset.UtcNow - e.StartedAt).TotalSeconds);
        var tasksDone = e.CellsProcessed + e.CellsFailed;
        var progress = e.TotalTasksPlanned > 0
            ? (int)Math.Min(100, Math.Round((tasksDone * 100.0) / e.TotalTasksPlanned))
            : (e.Status == "queued" ? 0 : 0);

        return new JsonResult(new
        {
            isActive = true,
            runningCount,
            pausedCount,
            executionId = e.Id,
            jobName = active.Job.Name,
            status = e.Status,
            startedAt = e.StartedAt,
            requestCount = e.RequestCount,
            cellsProcessed = e.CellsProcessed,
            cellsFailed = e.CellsFailed,
            tasksDone,
            totalTasksPlanned = e.TotalTasksPlanned,
            newRecords = e.NewRecords,
            updatedRecords = e.UpdatedRecords,
            failedRecords = e.FailedRecords,
            progressPercent = progress,
            liveError = e.Status is "running" or "queued" ? live.Error : null,
            currentGrid = live.CurrentCell?.H3Index,
            currentGridNumber = runningGridNumber,
            currentGridCategoryId = live.CurrentCell?.CategoryId,
            currentGridSearchTerm = live.CurrentCell?.SearchTerm,
            queuedGrids = live.QueuedCells,
            schedulerState = rateLimit.Remaining <= 0 ? "blocked" : e.Status,
            recentGrids = live.RecentCells.Select(c => c.H3Index).ToArray(),
            failedGrids = live.FailedCells.Select(c => new { c.H3Index, c.Error, c.UpdatedAt }).ToArray(),
            apiCallsUsedThisHour = rateLimit.Used,
            apiCallsRemainingThisHour = rateLimit.Remaining,
            apiCallLimitPerHour = rateLimit.Limit,
            requestsPerMinute = Math.Round(e.RequestCount * 60.0 / elapsedSec, 1),
            cellsPerMinute = Math.Round(e.CellsProcessed * 60.0 / elapsedSec, 1),
            elapsedSeconds = (int)elapsedSec,
            h3Total,
            h3Failed,
            h3Success,
            h3Crawled,
            h3Remaining,
            h3SuccessRate,
            nextQueuedGrids,
            lastCrawl,
            currentRunningGridNumber = runningGridNumber,
            lastCrawledGridNumber = lastCrawl?.GridNumber,
            lastCrawledDatetime = lastCrawl?.Timestamp,
            dashboardGrids
        });
    }

    private async Task<(int Used, int Remaining, int Limit)> LoadRateLimitStatusAsync(CancellationToken cancellationToken)
    {
        var limitRaw = await db.SystemConfigurations
            .AsNoTracking()
            .Where(c => c.ConfigKey == "crawl.maxExecutionsPerHour"
                        || c.ConfigKey == "crawl.max_executions_per_hour")
            .OrderBy(c => c.ConfigKey == "crawl.maxExecutionsPerHour" ? 0 : 1)
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(cancellationToken);

        var limit = int.TryParse(limitRaw, out var parsed) && parsed > 0 ? parsed : 5;
        var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
        var used = await db.NeshanUsageLedger
            .AsNoTracking()
            .Where(e => e.Accepted && e.Timestamp >= cutoff)
            .SumAsync(e => (int?)e.CostUnits, cancellationToken) ?? 0;

        return (used, Math.Max(0, limit - used), limit);
    }

    private async Task<int?> LoadGridNumberAsync(long h3Index, CancellationToken cancellationToken)
    {
        return await db.H3CoverageCells
            .AsNoTracking()
            .Where(c => c.H3Index == h3Index)
            .Select(c => c.GridNumber)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<List<GridQueueRow>> LoadNextQueuedGridsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var eligible = await db.H3CoverageCells
            .AsNoTracking()
            .Where(c => (c.CrawlLockExpiresAt == null || c.CrawlLockExpiresAt <= now)
                        && (c.NextEligibleCrawlAt == null || c.NextEligibleCrawlAt <= now))
            .Select(c => new { c.GridNumber, c.H3Index })
            .ToListAsync(cancellationToken);

        return eligible
            .OrderBy(_ => Guid.NewGuid())
            .Take(5)
            .Select(c => new GridQueueRow(c.GridNumber, c.H3Index, "random"))
            .ToList();
    }

    private async Task<CrawlTimelineRow?> LoadLastCrawlAsync(CancellationToken cancellationToken)
    {
        return await db.CrawlHistory
            .AsNoTracking()
            .Where(h => h.Status == "success" || h.Status == "failed")
            .OrderByDescending(h => h.Timestamp)
            .Select(h => new CrawlTimelineRow(
                h.GridNumber,
                h.H3Index,
                h.Timestamp,
                h.DurationMs,
                h.Status))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<ActiveGridContext> LoadActiveGridContextAsync(Guid executionId, CancellationToken cancellationToken)
    {
        var live = liveTelemetry.GetSnapshot(executionId);
        var currentH3 = live.CurrentCell?.H3Index;
        var currentGridNumber = currentH3 is null
            ? null
            : await LoadGridNumberAsync(currentH3.Value, cancellationToken);

        return new ActiveGridContext(
            currentGridNumber,
            live.QueuedCells.ToHashSet(),
            currentH3);
    }

    private async Task<List<GridDashboardRow>> LoadDashboardGridsAsync(
        ActiveGridContext active,
        CancellationToken cancellationToken)
    {
        var rows = await db.H3CoverageCells
            .AsNoTracking()
            .OrderBy(c => c.GridNumber ?? int.MaxValue)
            .ThenBy(c => c.H3Index)
            .Take(50)
            .Select(c => new
            {
                c.GridNumber,
                c.H3Index,
                c.LastCrawlAt,
                c.LastCrawlStatus,
                c.Status,
                c.PoiCount,
                c.CoverageScore
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(c => new GridDashboardRow(
                c.GridNumber,
                ResolveGridStatus(c.H3Index, c.LastCrawlAt, c.LastCrawlStatus, c.Status, active),
                c.LastCrawlAt,
                c.PoiCount,
                Math.Round(c.CoverageScore, 3)))
            .ToList();
    }

    private static string ResolveGridStatus(
        long h3Index,
        DateTimeOffset? lastCrawlAt,
        string? lastCrawlStatus,
        string status,
        ActiveGridContext active)
    {
        if (active.CurrentH3Index == h3Index)
            return "Running";

        if (active.QueuedH3Indexes.Contains(h3Index))
            return "Queued";

        if (string.Equals(lastCrawlStatus, "failed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
            return "Failed";

        if (lastCrawlAt is null)
            return "Never Crawled";

        return "Completed";
    }

    public async Task<IActionResult> OnPostStartCrawlAsync(CancellationToken cancellationToken)
    {
        var job = await db.CrawlJobs
            .Where(j => j.JobType == CrawlJobKinds.Manual)
            .OrderBy(j => j.Name == "tehran-manual" ? 0 : 1)
            .ThenBy(j => j.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null)
            return new JsonResult(new { ok = false, message = "هیچ Crawl Job دستی (tehran-manual) یافت نشد." });

        var alreadyRunning = await db.CrawlJobExecutions
            .AnyAsync(e => e.Status == "running" || e.Status == "queued", cancellationToken);
        if (alreadyRunning)
            return new JsonResult(new { ok = false, message = "یک Crawl در حال اجراست." });

        var rateLimit = await LoadRateLimitStatusAsync(cancellationToken);
        if (rateLimit.Remaining <= 0)
            return new JsonResult(new { ok = false, message = "سقف درخواست Neshan برای این ساعت پر شده است." });

        var execution = new CrawlJobExecution
        {
            Id = Guid.NewGuid(),
            CrawlJobId = job.Id,
            Status = "queued",
            StartedAt = DateTimeOffset.UtcNow,
            TriggeredBy = "admin_manual:full"
        };

        db.CrawlJobExecutions.Add(execution);
        await db.SaveChangesAsync(cancellationToken);

        return new JsonResult(new { ok = true, message = $"Crawl «{job.Name}» شروع شد.", executionId = execution.Id });
    }

    public async Task<IActionResult> OnPostStopCrawlAsync(CancellationToken cancellationToken)
    {
        var running = await db.CrawlJobExecutions
            .Where(e => e.Status == "running" || e.Status == "queued" || e.Status == "paused")
            .ToListAsync(cancellationToken);

        if (running.Count == 0)
            return new JsonResult(new { ok = false, message = "هیچ کار فعالی برای توقف وجود ندارد." });

        foreach (var e in running)
            e.Status = "cancelled";

        await db.SaveChangesAsync(cancellationToken);
        foreach (var e in running)
            liveTelemetry.Clear(e.Id);

        return new JsonResult(new { ok = true, message = $"{running.Count} کار در حال توقف…" });
    }

    public async Task<IActionResult> OnPostPauseCrawlAsync(CancellationToken cancellationToken)
    {
        var running = await db.CrawlJobExecutions
            .Where(e => e.Status == "running")
            .ToListAsync(cancellationToken);

        if (running.Count == 0)
            return new JsonResult(new { ok = false, message = "هیچ کار فعالی برای مکث وجود ندارد." });

        foreach (var e in running)
            e.Status = "paused";

        await db.SaveChangesAsync(cancellationToken);
        return new JsonResult(new { ok = true, message = "Crawl در حال مکث…" });
    }

    public async Task<IActionResult> OnPostResumeCrawlAsync(CancellationToken cancellationToken)
    {
        var paused = await db.CrawlJobExecutions
            .Where(e => e.Status == "paused")
            .OrderByDescending(e => e.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (paused is null)
            return new JsonResult(new { ok = false, message = "هیچ Crawl متوقف‌شده‌ای برای ادامه وجود ندارد." });

        var alreadyRunning = await db.CrawlJobExecutions
            .AnyAsync(e => e.Status == "running", cancellationToken);
        if (alreadyRunning)
            return new JsonResult(new { ok = false, message = "یک Crawl دیگر در حال اجراست." });

        paused.Status = "running";
        paused.EndedAt = null;
        paused.DurationMs = null;
        await db.SaveChangesAsync(cancellationToken);

        return new JsonResult(new { ok = true, message = "Crawl از سر گرفته شد." });
    }

    private async Task LoadStatsAsync(CancellationToken cancellationToken)
    {
        TotalPois       = await db.Pois.CountAsync(cancellationToken);
        ActivePois      = await db.Pois.CountAsync(p => p.SupersededAt == null, cancellationToken);
        TotalCategories = await db.PoiCategories.CountAsync(cancellationToken);
        RunningJobs     = await db.CrawlJobExecutions.CountAsync(e => e.Status == "running", cancellationToken);
        H3Total         = await db.H3CoverageCells.CountAsync(cancellationToken);
        H3Pending       = await db.H3CoverageCells.CountAsync(c => c.Status == "pending",  cancellationToken);
        H3Success       = await db.H3CoverageCells.CountAsync(c => c.Status == "success",  cancellationToken);
        H3Failed        = await db.H3CoverageCells.CountAsync(c => c.Status == "failed",   cancellationToken);
        H3Stale         = await db.H3CoverageCells.CountAsync(c => c.Status == "stale",    cancellationToken);
        H3Crawled       = await db.H3CoverageCells.CountAsync(c => c.LastCrawlAt != null, cancellationToken);
        H3Remaining     = Math.Max(0, H3Total - H3Crawled);
        H3SuccessRate   = H3Crawled == 0 ? 0 : Math.Round(H3Success * 100.0 / H3Crawled, 1);

        var rate = await LoadRateLimitStatusAsync(cancellationToken);
        ApiCallsUsedThisHour = rate.Used;
        ApiCallsRemainingThisHour = rate.Remaining;
        ApiCallLimitPerHour = rate.Limit;
        SchedulerState = rate.Remaining <= 0
            ? "blocked"
            : await db.CrawlJobExecutions.AnyAsync(e => e.Status == "running" || e.Status == "queued", cancellationToken)
                ? "running"
                : "idle";

        LastCrawl = await LoadLastCrawlAsync(cancellationToken);
        NextQueuedGrids = await LoadNextQueuedGridsAsync(cancellationToken);

        var activeExecutionId = await db.CrawlJobExecutions
            .AsNoTracking()
            .Where(e => e.Status == "running" || e.Status == "queued")
            .OrderByDescending(e => e.StartedAt)
            .Select(e => (Guid?)e.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var activeGridContext = activeExecutionId is null
            ? ActiveGridContext.Empty
            : await LoadActiveGridContextAsync(activeExecutionId.Value, cancellationToken);

        CurrentRunningGridNumber = activeGridContext.CurrentGridNumber;
        DashboardGrids = await LoadDashboardGridsAsync(activeGridContext, cancellationToken);

        LastSuccessfulCrawl = await db.CrawlJobExecutions
            .Where(e => e.Status == "completed")
            .OrderByDescending(e => e.EndedAt)
            .Select(e => e.EndedAt)
            .FirstOrDefaultAsync(cancellationToken);

        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT PostGIS_Lib_Version()", cancellationToken);
            DbHealthy      = true;
            PostgisHealthy = true;
        }
        catch
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
                DbHealthy = true;
            }
            catch
            {
                DbHealthy = false;
            }
            PostgisHealthy = false;
        }
    }

    public sealed record GridQueueRow(int? GridNumber, long H3Index, string Priority);
    public sealed record CrawlTimelineRow(int? GridNumber, long H3Index, DateTimeOffset Timestamp, long? DurationMs, string Status);
    public sealed record GridDashboardRow(
        int? GridNumber,
        string Status,
        DateTimeOffset? LastCrawlTime,
        int PoiCount,
        double CoverageScore);

    private sealed record ActiveGridContext(int? CurrentGridNumber, HashSet<long> QueuedH3Indexes, long? CurrentH3Index)
    {
        public static ActiveGridContext Empty { get; } = new(null, [], null);
    }
}
