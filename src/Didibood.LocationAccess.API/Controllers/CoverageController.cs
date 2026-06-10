using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.Coverage;
using Microsoft.AspNetCore.Mvc;

namespace Didibood.LocationAccess.API.Controllers;

[ApiController]
[Route("api/coverage")]
public sealed class CoverageController(ICoverageService coverageService) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await coverageService.GetSummaryAsync(cancellationToken);
        return Ok(summary);
    }

    [HttpGet("cells")]
    public async Task<IActionResult> GetCells(
        [FromQuery] string? status,
        [FromQuery] short? resolution,
        [FromQuery] double? minLat,
        [FromQuery] double? maxLat,
        [FromQuery] double? minLng,
        [FromQuery] double? maxLng,
        [FromQuery] int? maxAgeDays,
        [FromQuery] int limit = 500,
        CancellationToken cancellationToken = default)
    {
        var query = new CoverageCellsQuery
        {
            Status = status,
            Resolution = resolution,
            MinLat = minLat,
            MaxLat = maxLat,
            MinLng = minLng,
            MaxLng = maxLng,
            MaxAgeDays = maxAgeDays,
            Limit = limit
        };

        var geoJson = await coverageService.GetCellsAsync(query, cancellationToken);
        return Ok(geoJson);
    }

    [HttpGet("cells/{h3Index:long}")]
    public async Task<IActionResult> GetCellDetail(long h3Index, CancellationToken cancellationToken)
    {
        var detail = await coverageService.GetCellDetailAsync(h3Index, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet("heatmap")]
    public async Task<IActionResult> GetHeatmap(
        [FromQuery] string? status,
        [FromQuery] short? resolution = 8,
        [FromQuery] int? minPoiCount = null,
        [FromQuery] int? maxAgeDays = null,
        [FromQuery] short? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new CoverageHeatmapQuery
        {
            Status = status,
            Resolution = resolution,
            MinPoiCount = minPoiCount,
            MaxAgeDays = maxAgeDays,
            CategoryId = categoryId
        };

        var points = await coverageService.GetHeatmapAsync(query, cancellationToken);
        return Ok(points);
    }
}
