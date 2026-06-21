using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Domain;
using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Admin.Pages.Crawl;

public record ActiveExecutionViewModel(
    Guid Id,
    string JobName,
    string Status,
    DateTimeOffset StartedAt,
    int RequestCount,
    int NewRecords,
    int UpdatedRecords,
    int FailedRecords,
    int CellsProcessed,
    int TotalTasksPlanned);

public class IndexModel(AppDbContext db, ICrawlLiveTelemetry liveTelemetry) : PageModel
{
    private static readonly string[] PreferredJobOrder = ["tehran-daily", "tehran-manual", "tehran-stale-refresh"];

    [BindProperty] public string CrawlMode { get; set; } = "full";
    [BindProperty] public List<short> SelectedCategories { get; set; } = [];
    [BindProperty] public string? BaseJobIdRaw { get; set; }

    public List<PoiCategory> Categories { get; private set; } = [];
    public List<CrawlJob> AvailableJobs { get; private set; } = [];
    public Guid? DefaultJobId { get; private set; }
    public int RunningCount { get; private set; }
    public List<ActiveExecutionViewModel> ActiveExecutions { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPageDataAsync(cancellationToken);
    }

    public async Task<IActionResult> OnGetActiveJobsPartialAsync(CancellationToken cancellationToken)
    {
        ActiveExecutions = await BuildActiveExecutionsAsync(cancellationToken);
        return Partial("_ActiveJobsPartial", ActiveExecutions);
    }

    public async Task<IActionResult> OnPostStartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var job = await ResolveBaseJobAsync(BaseJobIdRaw, cancellationToken);

            if (job is null)
            {
                TempData["Error"] = "Crawl Job انتخاب‌شده یافت نشد.";
                return RedirectToPage();
            }

            if (job.JobType == CrawlJobKinds.Manual && !job.IsEnabled)
            {
                TempData["Error"] = $"کار «{job.Name}» غیرفعال است. از صفحه Scheduler آن را فعال کنید.";
                return RedirectToPage();
            }

            if (CrawlMode == "categories" && SelectedCategories.Count == 0)
            {
                TempData["Error"] = "حداقل یک دسته‌بندی را انتخاب کنید.";
                return RedirectToPage();
            }

            var alreadyActive = await db.CrawlJobExecutions
                .AnyAsync(e => e.Status == "running" || e.Status == "queued", cancellationToken);
            if (alreadyActive)
            {
                TempData["Error"] = "یک Crawl دیگر در حال اجرا یا در صف است. ابتدا آن را متوقف کنید.";
                return RedirectToPage();
            }

            var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
            var usedThisHour = await db.NeshanUsageLedger
                .AsNoTracking()
                .Where(x => x.Accepted && x.Timestamp >= cutoff)
                .SumAsync(x => (int?)x.CostUnits, cancellationToken) ?? 0;
            var limitRaw = await db.SystemConfigurations
                .AsNoTracking()
                .Where(c => c.ConfigKey == "crawl.maxExecutionsPerHour")
                .Select(c => c.ConfigValue)
                .FirstOrDefaultAsync(cancellationToken);
            var limit = int.TryParse(limitRaw, out var parsedLimit) && parsedLimit > 0 ? parsedLimit : 5;
            if (usedThisHour >= limit)
            {
                TempData["Error"] = "سقف درخواست Neshan برای این ساعت پر شده است.";
                return RedirectToPage();
            }

            var triggeredBy = CrawlMode switch
            {
                "categories" => $"admin_manual:categories:{string.Join(',', SelectedCategories.OrderBy(x => x))}",
                "failed" => "admin_manual:failed",
                _ => "admin_manual:full"
            };

            var execution = new CrawlJobExecution
            {
                Id = Guid.NewGuid(),
                CrawlJobId = job.Id,
                Status = "queued",
                StartedAt = DateTimeOffset.UtcNow,
                TriggeredBy = triggeredBy
            };

            db.CrawlJobExecutions.Add(execution);
            await db.SaveChangesAsync(cancellationToken);

            TempData["Success"] =
                $"Crawl «{job.Name}» در صف Worker قرار گرفت — ظرف چند ثانیه شروع می‌شود (ID: {execution.Id:N}).";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"خطا در ثبت Crawl: {ex.InnerException?.Message ?? ex.Message}";
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostStopAsync(CancellationToken cancellationToken)
    {
        var active = await db.CrawlJobExecutions
            .Where(e => e.Status == "running" || e.Status == "queued" || e.Status == "paused")
            .ToListAsync(cancellationToken);

        if (active.Count == 0)
        {
            TempData["Error"] = "هیچ کار فعالی برای توقف وجود ندارد.";
            return RedirectToPage();
        }

        foreach (var e in active)
        {
            e.Status = "cancelled";
            e.EndedAt = DateTimeOffset.UtcNow;
            e.DurationMs = (long)(DateTimeOffset.UtcNow - e.StartedAt).TotalMilliseconds;
        }

        await db.SaveChangesAsync(cancellationToken);
        foreach (var e in active)
            liveTelemetry.Clear(e.Id);

        TempData["Success"] = $"{active.Count} کار در حال توقف…";
        return RedirectToPage();
    }

    private async Task<CrawlJob?> ResolveBaseJobAsync(string? baseJobIdRaw, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(baseJobIdRaw, out var baseJobId))
        {
            var selected = await db.CrawlJobs
                .FirstOrDefaultAsync(j => j.Id == baseJobId, cancellationToken);
            if (selected is not null)
                return selected;
        }

        return await db.CrawlJobs
            .OrderBy(j => j.Name == "tehran-manual" ? 0 : 1)
            .ThenBy(j => j.Name)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task LoadPageDataAsync(CancellationToken cancellationToken)
    {
        Categories = await db.PoiCategories.Where(c => c.IsEnabled).OrderBy(c => c.DisplayOrder).ToListAsync(cancellationToken);

        var allJobs = await db.CrawlJobs.ToListAsync(cancellationToken);
        AvailableJobs = allJobs
            .OrderBy(j => Array.IndexOf(PreferredJobOrder, j.Name) is var i and >= 0 ? i : PreferredJobOrder.Length)
            .ThenBy(j => j.Name)
            .ToList();

        DefaultJobId = AvailableJobs.FirstOrDefault(j => j.Name == "tehran-manual")?.Id
            ?? AvailableJobs.FirstOrDefault()?.Id;

        RunningCount = await db.CrawlJobExecutions
            .CountAsync(e => e.Status == "running" || e.Status == "queued", cancellationToken);
        ActiveExecutions = await BuildActiveExecutionsAsync(cancellationToken);
    }

    private async Task<List<ActiveExecutionViewModel>> BuildActiveExecutionsAsync(CancellationToken cancellationToken)
    {
        return await db.CrawlJobExecutions
            .Where(e => e.Status == "running" || e.Status == "paused" || e.Status == "queued")
            .OrderByDescending(e => e.StartedAt)
            .Join(db.CrawlJobs, e => e.CrawlJobId, j => j.Id, (e, j) => new ActiveExecutionViewModel(
                e.Id, j.Name, e.Status, e.StartedAt,
                e.RequestCount, e.NewRecords, e.UpdatedRecords, e.FailedRecords,
                e.CellsProcessed, e.TotalTasksPlanned))
            .ToListAsync(cancellationToken);
    }
}
