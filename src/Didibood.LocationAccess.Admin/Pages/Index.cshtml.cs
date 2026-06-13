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

        if (active is null)
        {
            return new JsonResult(new
            {
                isActive = false,
                runningCount,
                pausedCount,
                h3Total,
                h3Success
            });
        }

        var e = active.Execution;
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
            liveError = e.Status is "running" or "queued" ? liveTelemetry.GetLiveError(e.Id) : null,
            requestsPerMinute = Math.Round(e.RequestCount * 60.0 / elapsedSec, 1),
            cellsPerMinute = Math.Round(e.CellsProcessed * 60.0 / elapsedSec, 1),
            elapsedSeconds = (int)elapsedSec,
            h3Total,
            h3Success
        });
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
}
