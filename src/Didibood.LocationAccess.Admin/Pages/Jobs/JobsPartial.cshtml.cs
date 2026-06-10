using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Admin.Pages.Jobs;

public class JobsPartialModel(AppDbContext db) : PageModel
{
    public List<ExecutionRow> Executions { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Executions = await db.CrawlJobExecutions
            .Where(e => e.Status == "running")
            .OrderByDescending(e => e.StartedAt)
            .Take(50)
            .Join(db.CrawlJobs, e => e.CrawlJobId, j => j.Id, (e, j) => new ExecutionRow(
                e.Id, j.Name, e.TriggeredBy, e.Status,
                e.StartedAt, e.EndedAt, e.DurationMs,
                e.RequestCount, e.NewRecords, e.UpdatedRecords,
                e.FailedRecords, e.CellsProcessed, e.CellsFailed,
                e.ErrorSummary))
            .ToListAsync(cancellationToken);
    }
}
