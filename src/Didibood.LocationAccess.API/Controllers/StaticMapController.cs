using Didibood.LocationAccess.Application.Abstractions;
using Didibood.LocationAccess.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Didibood.LocationAccess.API.Controllers;

[ApiController]
[Route("api/static-map")]
public sealed class StaticMapController(
    IStaticMapProvider staticMapProvider,
    IValidator<StaticMapRequest> validator) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "image/png")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetStaticMap(
        [FromQuery(Name = "latitude")] double? latitude,
        [FromQuery(Name = "lat")] double? lat,
        [FromQuery(Name = "longitude")] double? longitude,
        [FromQuery(Name = "lng")] double? lng,
        [FromQuery] int zoom = 14,
        [FromQuery] int width = 600,
        [FromQuery] int height = 400,
        [FromQuery] string? style = "light",
        [FromQuery] string? marker = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedLat = latitude ?? lat;
        var resolvedLng = longitude ?? lng;
        if (resolvedLat is null || resolvedLng is null)
            return BadRequest(new { message = "latitude and longitude are required." });

        var request = new StaticMapRequest
        {
            Latitude = resolvedLat.Value,
            Longitude = resolvedLng.Value,
            Zoom = zoom,
            Width = width,
            Height = height,
            Style = style,
            Marker = marker
        };

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(validation.Errors);

        try
        {
            var result = await staticMapProvider.GetMapAsync(request, cancellationToken);
            if (result.ImageData is null or { Length: 0 })
                return Problem("No image data returned from provider.", statusCode: StatusCodes.Status502BadGateway);

            Response.Headers["X-Cache"] = result.FromCache ? "HIT" : "MISS";
            Response.Headers["X-Cache-Key"] = result.CacheKey ?? string.Empty;

            return File(result.ImageData, "image/png");
        }
        catch (NeshanException ex)
        {
            return StatusCode(ex.HttpStatus ?? 502, new { ex.Code, ex.Message });
        }
    }

    [HttpGet("cache")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCachedSnapshots(CancellationToken cancellationToken)
    {
        var snapshots = await staticMapProvider.GetCachedSnapshotsAsync(cancellationToken);
        var result = snapshots.Select(s => new
        {
            s.Id,
            s.Latitude,
            s.Longitude,
            s.Zoom,
            s.Width,
            s.Height,
            s.Style,
            s.Marker,
            s.CacheKey,
            s.CreatedAt
        });
        return Ok(result);
    }

    [HttpDelete("cache/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCachedSnapshot(Guid id, CancellationToken cancellationToken)
    {
        await staticMapProvider.DeleteCachedSnapshotAsync(id, cancellationToken);
        return NoContent();
    }
}
