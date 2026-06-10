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
    int CellsProcessed);

public class IndexModel(AppDbContext db) : PageModel
{
    [BindProperty] public string CrawlMode           { get; set; } = "full";
    [BindProperty] public List<short> SelectedCategories { get; set; } = [];
    [BindProperty] public Guid? BaseJobId            { get; set; }

    public List<PoiCategory> Categories     { get; private set; } = [];
    public List<CrawlJob>    AvailableJobs  { get; private set; } = [];
    public int               RunningCount   { get; private set; }
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
        CrawlJob? job = null;

        if (BaseJobId.HasValue)
            job = await db.CrawlJobs.FindAsync(new object[] { BaseJobId.Value }, cancellationToken);

        job ??= await db.CrawlJobs
            .OrderBy(j => j.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            TempData["Error"] = "هیچ Crawl Job تعریف‌شده‌ای یافت نشد.";
            return RedirectToPage();
        }

        if (CrawlMode == "categories" && SelectedCategories.Count == 0)
        {
            TempData["Error"] = "حداقل یک دسته‌بندی را انتخاب کنید.";
            return RedirectToPage();
        }

        var triggeredBy = CrawlMode switch
        {
            "categories" => $"admin_manual:categories:{string.Join(',', SelectedCategories)}",
            "failed" => "admin_manual:failed",
            _ => "admin_manual:full"
        };

        var execution = new CrawlJobExecution
        {
            Id          = Guid.NewGuid(),
            CrawlJobId  = job.Id,
            Status      = "running",
            StartedAt   = DateTimeOffset.UtcNow,
            TriggeredBy = triggeredBy
        };

        db.CrawlJobExecutions.Add(execution);
        await db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = $"Crawl «{job.Name}» در صف Worker قرار گرفت (ID: {execution.Id:N}). برای اجرا Worker را روشن نگه دارید.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostStopAsync(CancellationToken cancellationToken)
    {
        var running = await db.CrawlJobExecutions
            .Where(e => e.Status == "running")
            .ToListAsync(cancellationToken);

        if (running.Count == 0)
        {
            TempData["Error"] = "هیچ کار فعالی برای توقف وجود ندارد.";
            return RedirectToPage();
        }

        foreach (var e in running)
        {
            e.Status  = "cancelled";
            e.EndedAt = DateTimeOffset.UtcNow;
            e.DurationMs = (long)(DateTimeOffset.UtcNow - e.StartedAt).TotalMilliseconds;
        }

        await db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = $"{running.Count} کار متوقف شد.";
        return RedirectToPage();
    }

    private async Task LoadPageDataAsync(CancellationToken cancellationToken)
    {
        Categories      = await db.PoiCategories.Where(c => c.IsEnabled).OrderBy(c => c.DisplayOrder).ToListAsync(cancellationToken);
        AvailableJobs   = await db.CrawlJobs.OrderBy(j => j.Name).ToListAsync(cancellationToken);
        RunningCount    = await db.CrawlJobExecutions.CountAsync(e => e.Status == "running", cancellationToken);
        ActiveExecutions = await BuildActiveExecutionsAsync(cancellationToken);
    }

    private async Task<List<ActiveExecutionViewModel>> BuildActiveExecutionsAsync(CancellationToken cancellationToken)
    {
        return await db.CrawlJobExecutions
            .Where(e => e.Status == "running")
            .OrderByDescending(e => e.StartedAt)
            .Join(db.CrawlJobs, e => e.CrawlJobId, j => j.Id, (e, j) => new ActiveExecutionViewModel(
                e.Id, j.Name, e.Status, e.StartedAt,
                e.RequestCount, e.NewRecords, e.UpdatedRecords, e.FailedRecords, e.CellsProcessed))
            .ToListAsync(cancellationToken);
    }
}
