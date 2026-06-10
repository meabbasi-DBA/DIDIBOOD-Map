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
    string? ErrorSummary);

public class IndexModel(AppDbContext db) : PageModel
{
    private const int PageSize = 20;

    [BindProperty(SupportsGet = true)] public int    CurrentPage   { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }

    public List<ExecutionRow> Executions { get; private set; } = [];
    public int TotalPages  { get; private set; }
    public int TotalCount  { get; private set; }

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

        Executions = await query
            .OrderByDescending(x => x.e.StartedAt)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .Select(x => new ExecutionRow(
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
                x.e.ErrorSummary))
            .ToListAsync(cancellationToken);
    }
}
