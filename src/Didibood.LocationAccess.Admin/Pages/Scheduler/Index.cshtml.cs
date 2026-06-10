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
                j.CronExpression, j.H3Resolution, j.IsEnabled,
                last?.LastRunAt, last?.Status);
        }).ToList();
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await db.CrawlJobs.FindAsync(new object[] { id }, cancellationToken);
        if (job is null)
        {
            TempData["Error"] = "کار مورد نظر یافت نشد.";
            return RedirectToPage();
        }

        job.IsEnabled  = !job.IsEnabled;
        job.UpdatedAt  = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = job.IsEnabled
            ? $"کار «{job.Name}» فعال شد."
            : $"کار «{job.Name}» غیرفعال شد.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveCronAsync(Guid id, string cronExpression, CancellationToken cancellationToken)
    {
        var job = await db.CrawlJobs.FindAsync(new object[] { id }, cancellationToken);
        if (job is null)
        {
            TempData["Error"] = "کار مورد نظر یافت نشد.";
            return RedirectToPage();
        }

        job.CronExpression = cronExpression?.Trim();
        job.UpdatedAt      = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        TempData["Success"] = $"Cron expression کار «{job.Name}» به‌روزرسانی شد.";
        return RedirectToPage();
    }
}
