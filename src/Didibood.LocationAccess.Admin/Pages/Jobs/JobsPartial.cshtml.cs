using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Admin.Pages.Jobs;

public class JobsPartialModel(AppDbContext db, ICrawlLiveTelemetry liveTelemetry) : PageModel
{
    public List<ExecutionRow> Executions { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var rows = await db.CrawlJobExecutions
            .Where(e => e.Status == "running" || e.Status == "queued")
            .OrderByDescending(e => e.StartedAt)
            .Take(50)
            .Join(db.CrawlJobs, e => e.CrawlJobId, j => j.Id, (e, j) => new
            {
                e.Id, j.Name, e.TriggeredBy, e.Status,
                e.StartedAt, e.EndedAt, e.DurationMs,
                e.RequestCount, e.NewRecords, e.UpdatedRecords,
                e.FailedRecords, e.CellsProcessed, e.CellsFailed,
                e.TotalTasksPlanned
            })
            .ToListAsync(cancellationToken);

        Executions = rows.Select(x =>
        {
            var live = liveTelemetry.GetSnapshot(x.Id);
            return new ExecutionRow(
                x.Id, x.Name, x.TriggeredBy, x.Status,
                x.StartedAt, x.EndedAt, x.DurationMs,
                x.RequestCount, x.NewRecords, x.UpdatedRecords,
                x.FailedRecords, x.CellsProcessed, x.CellsFailed,
                x.TotalTasksPlanned,
                live.CurrentCell?.H3Index,
                live.QueuedCells,
                live.RecentCells.Select(c => c.H3Index).ToArray(),
                live.FailedCells.Select(c => c.H3Index).ToArray(),
                live.Error);
        })
            .ToList();
    }
}
