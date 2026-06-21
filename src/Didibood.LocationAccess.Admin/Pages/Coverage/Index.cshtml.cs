using System.Text.Json;
using Didibood.LocationAccess.Application.Configuration;
using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Didibood.LocationAccess.Admin.Pages.Coverage;

public class IndexModel(
    AppDbContext db,
    IConfiguration config,
    IOptions<NeshanOptions> neshanOptions,
    ILogger<IndexModel> logger) : PageModel
{
    private static readonly TehranBoundsBox Defaults = new(35.50, 35.88, 51.10, 51.62);

    public string ApiBaseUrl { get; private set; } = "";
    public string WebMapKey { get; private set; } = "";
    public bool HasWebMapKey { get; private set; }
    public TehranBoundsBox TehranBounds { get; private set; } = Defaults;
    public short GridResolution { get; private set; } = 7;
    public string BoundaryMode { get; private set; } = "municipality";
    public List<PoiCategory> Categories { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ApiBaseUrl = config["ApiSettings:PublicBaseUrl"]?.TrimEnd('/')
                     ?? config["ApiSettings:BaseUrl"]?.TrimEnd('/')
                     ?? "http://localhost:5080";

        WebMapKey = neshanOptions.Value.GetWebMapKey();
        HasWebMapKey = !string.IsNullOrWhiteSpace(WebMapKey);

        TehranBounds = await LoadTehranBoundsAsync(cancellationToken);
        GridResolution = await LoadGridResolutionAsync(cancellationToken);
        BoundaryMode = await LoadBoundaryModeAsync(cancellationToken);

        Categories = await db.PoiCategories
            .AsNoTracking()
            .Where(c => c.IsEnabled)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken);
    }

    public IActionResult OnPostTrace([FromBody] CoverageTraceEvent trace)
    {
        logger.LogInformation(
            "Coverage flow trace: session={SessionId} event={EventName} timestamp={Timestamp:o} durationMs={DurationMs} endpoint={Endpoint} gridNumber={GridNumber} h3Index={H3Index} status={Status} details={Details}",
            trace.SessionId,
            trace.EventName,
            trace.Timestamp,
            trace.DurationMs,
            trace.Endpoint,
            trace.GridNumber,
            trace.H3Index,
            trace.Status,
            trace.Details);

        return new JsonResult(new { ok = true });
    }

    private async Task<string> LoadBoundaryModeAsync(CancellationToken ct)
    {
        var mode = await db.SystemConfigurations
            .AsNoTracking()
            .Where(c => c.ConfigKey == "tehran.boundary.mode")
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrWhiteSpace(mode) ? "municipality" : mode;
    }

    private async Task<short> LoadGridResolutionAsync(CancellationToken ct)
    {
        var resolutions = await db.H3CoverageCells
            .AsNoTracking()
            .Select(c => c.Resolution)
            .Distinct()
            .ToListAsync(ct);

        if (resolutions.Count == 1)
            return resolutions[0];

        var configured = await db.SystemConfigurations
            .AsNoTracking()
            .Where(c => c.ConfigKey == "crawl.h3_resolution")
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        if (short.TryParse(configured, out var parsed))
            return parsed;

        return 7;
    }

    private async Task<TehranBoundsBox> LoadTehranBoundsAsync(CancellationToken ct)
    {
        var boundsJson = await db.SystemConfigurations
            .AsNoTracking()
            .Where(c => c.ConfigKey == "tehran.bounds")
            .Select(c => c.ConfigValue)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(boundsJson))
            return Defaults;

        try
        {
            var parsed = JsonSerializer.Deserialize<TehranBoundsDto>(boundsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed is null)
                return Defaults;

            return new TehranBoundsBox(parsed.MinLat, parsed.MaxLat, parsed.MinLng, parsed.MaxLng);
        }
        catch
        {
            return Defaults;
        }
    }

    public sealed record TehranBoundsBox(double MinLat, double MaxLat, double MinLng, double MaxLng);

    public sealed record CoverageTraceEvent(
        string SessionId,
        string EventName,
        DateTimeOffset Timestamp,
        double DurationMs,
        string? Endpoint,
        int? GridNumber,
        long? H3Index,
        string? Status,
        string? Details);

    private sealed class TehranBoundsDto
    {
        public double MinLat { get; set; }
        public double MaxLat { get; set; }
        public double MinLng { get; set; }
        public double MaxLng { get; set; }
    }
}
