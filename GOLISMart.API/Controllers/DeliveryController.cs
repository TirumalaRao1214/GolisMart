using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace GOLISMart.API.Controllers;

[ApiController]
[Route("api/v1/delivery")]
[Produces("application/json")]
public class DeliveryController : ControllerBase
{
    private readonly IDeliveryService _delivery;
    private readonly ILogger<DeliveryController> _logger;

    public DeliveryController(IDeliveryService delivery, ILogger<DeliveryController> logger)
    {
        _delivery = delivery;
        _logger   = logger;
    }

    /// <summary>Check whether delivery is available for a given PIN code.</summary>
    [HttpGet("serviceability")]
    [SwaggerOperation(Summary = "Check delivery serviceability for a PIN code")]
    [ProducesResponseType(typeof(ServiceabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckServiceability(
        [FromQuery] string? pinCode,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pinCode))
            return BadRequest(new ErrorResponse("pinCode query parameter is required."));

        var pinTrimmed = pinCode.Trim();
        var result = await _delivery.CheckServiceabilityAsync(pinTrimmed, ct);
        return Ok(result);
    }

    /// <summary>List all active serviceable delivery areas.</summary>
    [HttpGet("areas")]
    [SwaggerOperation(Summary = "List all active serviceable delivery areas")]
    [ProducesResponseType(typeof(IReadOnlyList<DeliveryAreaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAreas(CancellationToken ct = default)
    {
        var areas = await _delivery.GetActiveAreasAsync(ct);
        return Ok(areas);
    }
}
