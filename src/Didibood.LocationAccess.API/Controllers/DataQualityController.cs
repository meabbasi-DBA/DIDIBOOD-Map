using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.DataQuality;
using Didibood.LocationAccess.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Didibood.LocationAccess.API.Controllers;

[ApiController]
[Route("api/data-quality")]
public sealed class DataQualityController(IDataQualityService dataQualityService) : ControllerBase
{
    [HttpPost("compare")]
    public async Task<IActionResult> Compare(
        [FromBody] DataQualityCompareRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SearchTerm))
            return BadRequest(new { message = "searchTerm is required." });

        try
        {
            var result = await dataQualityService.CompareAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (NeshanException ex)
        {
            return StatusCode(ex.HttpStatus ?? 502, new { ex.Code, ex.Message });
        }
    }

    [HttpGet("poi/{id:guid}")]
    public async Task<IActionResult> GetPoi(Guid id, CancellationToken cancellationToken)
    {
        var poi = await dataQualityService.GetPoiDetailAsync(id, cancellationToken);
        return poi is null ? NotFound() : Ok(poi);
    }
}
