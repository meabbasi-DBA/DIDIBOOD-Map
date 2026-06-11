using System.Text.Json;
using Didibood.LocationAccess.Application.Configuration;
using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Didibood.LocationAccess.Admin.Pages.Coverage;

public class IndexModel(
    AppDbContext db,
    IConfiguration config,
    IOptions<NeshanOptions> neshanOptions) : PageModel
{
    private static readonly TehranBoundsBox Defaults = new(35.48, 35.92, 51.08, 51.65);

    public string ApiBaseUrl { get; private set; } = "";
    public string WebMapKey { get; private set; } = "";
    public bool HasWebMapKey { get; private set; }
    public TehranBoundsBox TehranBounds { get; private set; } = Defaults;
    public List<PoiCategory> Categories { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ApiBaseUrl = config["ApiSettings:PublicBaseUrl"]?.TrimEnd('/')
                     ?? config["ApiSettings:BaseUrl"]?.TrimEnd('/')
                     ?? "http://localhost:5080";

        WebMapKey = neshanOptions.Value.GetWebMapKey();
        HasWebMapKey = !string.IsNullOrWhiteSpace(WebMapKey);

        TehranBounds = await LoadTehranBoundsAsync(cancellationToken);

        Categories = await db.PoiCategories
            .AsNoTracking()
            .Where(c => c.IsEnabled)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken);
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

    private sealed class TehranBoundsDto
    {
        public double MinLat { get; set; }
        public double MaxLat { get; set; }
        public double MinLng { get; set; }
        public double MaxLng { get; set; }
    }
}
