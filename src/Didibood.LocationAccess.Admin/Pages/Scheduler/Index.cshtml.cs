using Didibood.LocationAccess.Domain;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Admin.Pages.Scheduler;

public record CrawlJobViewModel(
    Guid Id,
    string Name,
    string? Description,
    string JobType,
    string? CronExpression,
    short H3Resolution,
    int MaxParallelCells,
    bool IsEnabled,
    DateTimeOffset? LastRunAt,
    string? LastRunStatus);

public class IndexModel(AppDbContext db) : PageModel
{
    public List<CrawlJobViewModel> Jobs { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var jobs = await db.CrawlJobs
            .OrderBy(j => j.Name)
            .ToListAsync(cancellationToken);

        var lastExecutions = await db.CrawlJobExecutions
            .Where(e => jobs.Select(j => j.Id).Contains(e.CrawlJobId))
            .GroupBy(e => e.CrawlJobId)
            .Select(g => new
            {
                JobId     = g.Key,
                LastRunAt = g.OrderByDescending(e => e.StartedAt).Select(e => (DateTimeOffset?)e.StartedAt).First(),
                Status    = g.OrderByDescending(e => e.StartedAt).Select(e => e.Status).First()
            })
            .ToListAsync(cancellationToken);

        Jobs = jobs.Select(j =>
        {
            var last = lastExecutions.FirstOrDefault(e => e.JobId == j.Id);
            return new CrawlJobViewModel(
                j.Id, j.Name, j.Description, j.JobType,
                j.CronExpression, j.H3Resolution, j.MaxParallelCells, j.IsEnabled,
                last?.LastRunAt, last?.Status);
        }).ToList();
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await db.CrawlJobs.FindAsync([id], cancellationToken);
        if (job is null)
        {
            TempData["Error"] = "کار مورد نظر یافت نشد.";
            return RedirectToPage();
        }

        job.IsEnabled = !job.IsEnabled;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = job.IsEnabled
            ? $"کار «{job.Name}» فعال شد."
            : $"کار «{job.Name}» غیرفعال شد.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveJobAsync(
        Guid id,
        string? description,
        string? cronExpression,
        short h3Resolution,
        int maxParallelCells,
        CancellationToken cancellationToken)
    {
        var job = await db.CrawlJobs.FindAsync([id], cancellationToken);
        if (job is null)
        {
            TempData["Error"] = "کار مورد نظر یافت نشد.";
            return RedirectToPage();
        }

        var cron = string.IsNullOrWhiteSpace(cronExpression) ? null : cronExpression.Trim();

        if (job.JobType != CrawlJobKinds.Manual && string.IsNullOrWhiteSpace(cron))
        {
            TempData["Error"] = "برای کارهای scheduled، Cron Expression الزامی است.";
            return RedirectToPage();
        }

        if (h3Resolution is < 6 or > 9)
        {
            TempData["Error"] = "رزولوشن H3 باید بین ۶ تا ۹ باشد.";
            return RedirectToPage();
        }

        if (maxParallelCells is < 1 or > 10)
        {
            TempData["Error"] = "موازی‌سازی سلول باید بین ۱ تا ۱۰ باشد.";
            return RedirectToPage();
        }

        job.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        job.CronExpression = cron;
        job.H3Resolution = h3Resolution;
        job.MaxParallelCells = maxParallelCells;
        job.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = $"تنظیمات کار «{job.Name}» به‌روزرسانی شد.";
        return RedirectToPage();
    }
}
