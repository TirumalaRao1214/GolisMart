using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GOLISMart.API.Controllers;

/// <summary>
/// Location / geocoding endpoints.
///
/// NOTE: These endpoints call the Nominatim public geocoding service.
/// They do NOT require authentication because the reverse-geocode lookup
/// does not return any customer-specific data; it converts coordinates to
/// a generic street address suggestion.  The "geocode" rate-limiter policy
/// protects against abuse.
/// </summary>
[ApiController]
[Route("api/v1/location")]
[Produces("application/json")]
public class LocationController : ControllerBase
{
    private readonly IGeocodingService _geocoding;
    private readonly ILogger<LocationController> _logger;

    public LocationController(IGeocodingService geocoding, ILogger<LocationController> logger)
    {
        _geocoding = geocoding;
        _logger    = logger;
    }

    /// <summary>
    /// Reverse-geocode GPS coordinates to a normalized address suggestion.
    /// </summary>
    /// <param name="latitude">Decimal degrees, must be between -90 and 90.</param>
    /// <param name="longitude">Decimal degrees, must be between -180 and 180.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("reverse-geocode")]
    [EnableRateLimiting("geocode")]
    [ProducesResponseType(typeof(ReverseGeocodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ReverseGeocodeResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ReverseGeocode(
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        CancellationToken ct = default)
    {
        // ── Input validation ─────────────────────────────────────────────────

        if (latitude is null || longitude is null)
            return BadRequest(new ReverseGeocodeResponse(false, null,
                "Both latitude and longitude query parameters are required."));

        var lat = latitude.Value;
        var lon = longitude.Value;

        if (!double.IsFinite(lat) || lat < -90 || lat > 90)
            return BadRequest(new ReverseGeocodeResponse(false, null,
                "Latitude must be a finite number between -90 and 90."));

        if (!double.IsFinite(lon) || lon < -180 || lon > 180)
            return BadRequest(new ReverseGeocodeResponse(false, null,
                "Longitude must be a finite number between -180 and 180."));

        // ── Geocode ──────────────────────────────────────────────────────────

        // Do NOT log the exact coordinates to protect privacy.
        _logger.LogDebug("Reverse-geocode request received.");

        var result = await _geocoding.ReverseGeocodeAsync(lat, lon, ct);

        if (!result.Success)
        {
            // Use 503 when the underlying provider is unavailable / busy,
            // 200 (with Success=false) for a valid coordinates-but-no-result case.
            if (result.Error?.Contains("unavailable") == true
             || result.Error?.Contains("timed out") == true
             || result.Error?.Contains("busy") == true)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new ReverseGeocodeResponse(false, null, result.Error));

            return Ok(new ReverseGeocodeResponse(false, null, result.Error));
        }

        return Ok(result);
    }
}
