namespace Didibood.LocationAccess.Application.Configuration;

public sealed class ApiSettings
{
    public const string SectionName = "ApiSettings";

    /// <summary>Public base URL shown in API discovery docs (e.g. https://map.didibood.ir).</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5080";

    /// <summary>Expose Swagger UI outside Development when true.</summary>
    public bool EnableSwagger { get; set; }
}
