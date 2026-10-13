using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers;

[ApiController]
[Route("api/location")]
[Produces("application/json")]
public class LocationController : ControllerBase
{
    private readonly ILocationApiClient _locationApi;
    private readonly ILogger<LocationController> _logger;

    public LocationController(ILocationApiClient locationApi, ILogger<LocationController> logger)
    {
        _locationApi = locationApi;
        _logger      = logger;
    }

    [HttpGet("reverse-geocode")]
    public async Task<IActionResult> ReverseGeocode(
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        CancellationToken ct = default)
    {
        if (latitude is null || longitude is null)
            return BadRequest(new { success = false, error = "Latitude and longitude are required." });

        if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
            return BadRequest(new { success = false, error = "Invalid coordinate ranges." });

        var result = await _locationApi.ReverseGeocodeAsync(latitude.Value, longitude.Value, ct);
        if (result is null)
            return StatusCode(503, new { success = false, error = "Location service is unavailable." });

        return Ok(result);
    }
}
