using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Admin.Pages.Jobs;

public record ExecutionRow(
    Guid Id,
    string JobName,
    string TriggeredBy,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    long? DurationMs,
    int RequestCount,
    int NewRecords,
    int UpdatedRecords,
    int FailedRecords,
    int CellsProcessed,
    int CellsFailed,
    int TotalTasksPlanned,
    long? CurrentGrid,
    IReadOnlyList<long> QueuedGrids,
    IReadOnlyList<long> RecentGrids,
    IReadOnlyList<long> FailedGrids,
    string? LiveError);

public class IndexModel(AppDbContext db, ICrawlLiveTelemetry liveTelemetry) : PageModel
{
    private const int PageSize = 20;

    [BindProperty(SupportsGet = true)] public int    CurrentPage   { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }

    public List<ExecutionRow> Executions { get; private set; } = [];
    public int TotalPages  { get; private set; }
    public int TotalCount  { get; private set; }
    public bool HasActiveCrawl { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadDataAsync(cancellationToken);
    }

    public async Task<IActionResult> OnGetJobsPartialAsync(CancellationToken cancellationToken)
    {
        await LoadDataAsync(cancellationToken);
        return Partial("_JobsTablePartial", this);
    }

    private async Task LoadDataAsync(CancellationToken cancellationToken)
    {
        if (CurrentPage < 1) CurrentPage = 1;

        var query = db.CrawlJobExecutions
            .Join(db.CrawlJobs, e => e.CrawlJobId, j => j.Id, (e, j) => new { e, j });

        if (!string.IsNullOrEmpty(StatusFilter))
            query = query.Where(x => x.e.Status == StatusFilter);

        TotalCount = await query.CountAsync(cancellationToken);
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        if (CurrentPage > TotalPages) CurrentPage = TotalPages;

        var rows = await query
            .OrderByDescending(x => x.e.StartedAt)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .Select(x => new
            {
                x.e.Id,
                x.j.Name,
                x.e.TriggeredBy,
                x.e.Status,
                x.e.StartedAt,
                x.e.EndedAt,
                x.e.DurationMs,
                x.e.RequestCount,
                x.e.NewRecords,
                x.e.UpdatedRecords,
                x.e.FailedRecords,
                x.e.CellsProcessed,
                x.e.CellsFailed,
                x.e.TotalTasksPlanned
            })
            .ToListAsync(cancellationToken);

        HasActiveCrawl = rows.Any(r => r.Status is "running" or "queued")
            || await db.CrawlJobExecutions.AnyAsync(e => e.Status == "running" || e.Status == "queued", cancellationToken);

        Executions = rows.Select(x =>
        {
            var live = x.Status is "running" or "queued"
                ? liveTelemetry.GetSnapshot(x.Id)
                : new CrawlLiveSnapshot(null, null, [], [], []);

            return new ExecutionRow(
                x.Id,
                x.Name,
                x.TriggeredBy,
                x.Status,
                x.StartedAt,
                x.EndedAt,
                x.DurationMs,
                x.RequestCount,
                x.NewRecords,
                x.UpdatedRecords,
                x.FailedRecords,
                x.CellsProcessed,
                x.CellsFailed,
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
