namespace Didibood.LocationAccess.Application.Configuration;

public sealed class NeshanOptions
{
    public const string SectionName = "Neshan";

    public string ApiKey { get; set; } = string.Empty;
    public string LocationApiKey { get; set; } = string.Empty;
    public int SearchRadius { get; set; } = 2000;
    public int MaxResultsPerCategory { get; set; } = 20;
    public string WebMapKey { get; set; } = string.Empty;

    public string GetWebMapKey() =>
        !string.IsNullOrWhiteSpace(WebMapKey) ? WebMapKey : ApiKey;

    public string GetSearchApiKey() =>
        !string.IsNullOrWhiteSpace(ApiKey) ? ApiKey : LocationApiKey;
}
