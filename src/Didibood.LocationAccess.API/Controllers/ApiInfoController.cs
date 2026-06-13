using Didibood.LocationAccess.Application.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Didibood.LocationAccess.API.Controllers;

[ApiController]
[Route("api")]
public sealed class ApiInfoController(IOptions<ApiSettings> apiSettings) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetApiInfo()
    {
        var baseUrl = apiSettings.Value.PublicBaseUrl.TrimEnd('/');

        return Ok(new
        {
            service = "Didibood Location Access API",
            version = "1.0",
            description = "POI lookup for Business Core — queries local PostGIS data (no live Neshan call on location-access).",
            baseUrl,
            swagger = $"{baseUrl}/swagger",
            endpoints = new[]
            {
                new
                {
                    method = "POST",
                    path = "/api/location-access",
                    audience = "Business Core",
                    description = "Nearby POIs grouped by category within radius (meters).",
                    curl = $"curl -X POST \"{baseUrl}/api/location-access\" -H \"Content-Type: application/json\" -d \"{{\\\"latitude\\\":35.6892,\\\"longitude\\\":51.389,\\\"radius\\\":2000}}\""
                },
                new
                {
                    method = "GET",
                    path = "/api/location-access/categories",
                    audience = "Business Core",
                    description = "Enabled POI category codes returned in location-access responses.",
                    curl = $"curl \"{baseUrl}/api/location-access/categories\""
                },
                new
                {
                    method = "GET",
                    path = "/health",
                    audience = "Ops",
                    description = "Combined health check.",
                    curl = $"curl \"{baseUrl}/health\""
                },
                new
                {
                    method = "GET",
                    path = "/health/ready",
                    audience = "Ops",
                    description = "Readiness probe (DB, PostGIS, Neshan connectivity).",
                    curl = $"curl \"{baseUrl}/health/ready\""
                }
            },
            externalDependencies = new object[]
            {
                new { name = "PostgreSQL + PostGIS", usedBy = "location-access (read), coverage, data-quality" },
                new { name = "Neshan Search API", url = "https://api.neshan.org/v1/search", usedBy = "Worker crawl, data-quality compare, health check — not location-access" },
                new { name = "Neshan Static Map API", url = "https://api.neshan.org/v5/static", usedBy = "GET /api/static-map, Admin coverage preview" }
            },
            documentation = "/docs/api-business-core.md"
        });
    }
}
