using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.Admin.Pages;

public class IndexModel(AppDbContext db) : PageModel
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
