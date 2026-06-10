using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Application.LocationAccess;
using Didibood.LocationAccess.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Didibood.LocationAccess.API.Controllers;

[ApiController]
[Route("api/location-access")]
[EnableRateLimiting("api")]
public sealed class LocationAccessController(
    ILocationAccessService locationAccessService,
    IValidator<LocationAccessRequest> validator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(LocationAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
}
