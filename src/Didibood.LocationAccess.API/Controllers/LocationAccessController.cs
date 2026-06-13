using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.LocationAccess;
using Didibood.LocationAccess.Domain.Exceptions;
using Didibood.LocationAccess.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Didibood.LocationAccess.API.Controllers;

/// <summary>
/// Primary integration surface for Business Core — reads crawled POIs from PostGIS (no live Neshan call).
/// </summary>
[ApiController]
[Route("api/location-access")]
[EnableRateLimiting("api")]
public sealed class LocationAccessController(
    ILocationAccessService locationAccessService,
    IValidator<LocationAccessRequest> validator,
    AppDbContext db) : ControllerBase
{
    /// <summary>Returns nearby POIs grouped by category code (camelCase keys).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Dictionary<string, List<PoiResultDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetNearby(
        [FromBody] LocationAccessRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(validation.Errors);
        }

        try
        {
            var result = await locationAccessService.GetNearbyAsync(request, cancellationToken);
            return Ok(result.Categories);
        }
        catch (NeshanException ex)
        {
            return StatusCode(ex.HttpStatus ?? 502, new { ex.Code, ex.Message });
        }
    }

    /// <summary>Lists enabled category codes that may appear in location-access responses.</summary>
    [HttpGet("categories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var categories = await db.PoiCategories
            .AsNoTracking()
            .Where(c => c.IsEnabled)
            .OrderBy(c => c.DisplayOrder)
            .Select(c => new
            {
                c.Code,
                c.NameEn,
                c.NameFa
            })
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }
}
