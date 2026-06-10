using Didibood.LocationAccess.Domain.Entities;
using Didibood.LocationAccess.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace Didibood.LocationAccess.Admin.Pages.StaticMap;

public class IndexModel(AppDbContext db, IHttpClientFactory httpClientFactory, IConfiguration config) : PageModel
{
    private const int RecentCount = 20;

    [BindProperty] public decimal Latitude  { get; set; } = 35.6892m;
    [BindProperty] public decimal Longitude { get; set; } = 51.3890m;
    [BindProperty] public short   Zoom      { get; set; } = 12;
    [BindProperty] public int     Width     { get; set; } = 600;
    [BindProperty] public int     Height    { get; set; } = 400;
    [BindProperty] public string  Style     { get; set; } = "light";
    [BindProperty] public string? Marker    { get; set; }

    public string?  GeneratedImageUrl  { get; private set; }
    public string?  GenerateError      { get; private set; }
    public List<StaticMapSnapshot> RecentSnapshots { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadRecentAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostGenerateAsync(CancellationToken cancellationToken)
    {
        await LoadRecentAsync(cancellationToken);

        var apiBase = config["ApiSettings:BaseUrl"]?.TrimEnd('/')
                   ?? "http://localhost:5001";

        var qs =
            $"latitude={Latitude}&longitude={Longitude}&zoom={Zoom}&width={Width}&height={Height}&style={Uri.EscapeDataString(Style ?? "light")}";
        if (!string.IsNullOrWhiteSpace(Marker))
            qs += $"&marker={Uri.EscapeDataString(Marker)}";

        var requestUrl = $"{apiBase}/api/static-map?{qs}";

        try
        {
            var client   = httpClientFactory.CreateClient("api");
            var response = await client.GetAsync(requestUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                if (contentType.StartsWith("image/"))
                {
                    // Return the URL itself so the browser can fetch it directly
                    GeneratedImageUrl = requestUrl;
                }
                else
                {
                    // JSON response with image URL
                    var json = await response.Content.ReadFromJsonAsync<StaticMapApiResponse>(cancellationToken: cancellationToken);
                    GeneratedImageUrl = json?.Url ?? json?.ImageUrl ?? requestUrl;
                }
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                GenerateError = $"HTTP {(int)response.StatusCode}: {body[..Math.Min(200, body.Length)]}";
            }
        }
        catch (HttpRequestException ex)
        {
            GenerateError = $"خطای اتصال به API: {ex.Message}";
        }
        catch (Exception ex)
        {
            GenerateError = ex.Message;
        }

        return Page();
    }

    private async Task LoadRecentAsync(CancellationToken cancellationToken)
    {
        RecentSnapshots = await db.StaticMapSnapshots
            .OrderByDescending(s => s.CreatedAt)
            .Take(RecentCount)
            .ToListAsync(cancellationToken);
    }

    private record StaticMapApiResponse(string? Url, string? ImageUrl);
}
