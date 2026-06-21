namespace Didibood.LocationAccess.Application.Configuration;

public sealed class NeshanOptions
{
    public const string SectionName = "Neshan";

    public string SearchApiKey { get; set; } = string.Empty;
    public string ReverseGeocodeApiKey { get; set; } = string.Empty;
    public string RoutingApiKey { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string LocationApiKey { get; set; } = string.Empty;
    public int SearchRadius { get; set; } = 2000;
    public int MaxResultsPerCategory { get; set; } = 20;
    public string WebMapKey { get; set; } = string.Empty;

    public string GetWebMapKey() =>
        FirstConfigured(WebMapKey, LocationApiKey, SearchApiKey, ApiKey);

    public string GetSearchApiKey() =>
        FirstConfigured(SearchApiKey, LocationApiKey, ApiKey);

    public string GetLocationApiKey() =>
        FirstConfigured(LocationApiKey, ApiKey);

    public string GetReverseGeocodeApiKey() =>
        FirstConfigured(ReverseGeocodeApiKey, LocationApiKey, ApiKey);

    public string GetRoutingApiKey() =>
        FirstConfigured(RoutingApiKey, LocationApiKey, ApiKey);

    public static string Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "(missing)";

        var trimmed = value.Trim();
        return trimmed.Length <= 4 ? "****" : $"****{trimmed[^4..]}";
    }

    private static string FirstConfigured(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;
}
