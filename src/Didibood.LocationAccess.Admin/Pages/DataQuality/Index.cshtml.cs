using Didibood.LocationAccess.Application.DataQuality;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Didibood.LocationAccess.Admin.Pages.DataQuality;

public class IndexModel(IHttpClientFactory httpClientFactory, IConfiguration config) : PageModel
{
    [BindProperty] public double Latitude { get; set; } = 35.6892;
    [BindProperty] public double Longitude { get; set; } = 51.3890;
    [BindProperty] public string SearchTerm { get; set; } = "ایستگاه مترو";
    [BindProperty] public int RadiusMeters { get; set; } = 2000;

    public DataQualityCompareResult? Result { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string ApiBaseUrl { get; private set; } = string.Empty;

    public void OnGet()
    {
        ApiBaseUrl = config["ApiSettings:PublicBaseUrl"]?.TrimEnd('/')
                     ?? config["ApiSettings:BaseUrl"]?.TrimEnd('/')
                     ?? "http://localhost:5080";
    }

    public async Task<IActionResult> OnPostCompareAsync(CancellationToken cancellationToken)
    {
        ApiBaseUrl = config["ApiSettings:PublicBaseUrl"]?.TrimEnd('/')
                     ?? config["ApiSettings:BaseUrl"]?.TrimEnd('/')
                     ?? "http://localhost:5080";

        var request = new DataQualityCompareRequest
        {
            Latitude = Latitude,
            Longitude = Longitude,
            SearchTerm = SearchTerm,
            RadiusMeters = RadiusMeters
        };

        try
        {
            var client = httpClientFactory.CreateClient("api");
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("api/data-quality/compare", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                Result = await response.Content.ReadFromJsonAsync<DataQualityCompareResult>(cancellationToken);
            }
            else
            {
                ErrorMessage = await response.Content.ReadAsStringAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        return Page();
    }
}
