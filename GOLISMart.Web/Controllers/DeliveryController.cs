using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers;

[ApiController]
[Route("api/delivery")]
[Produces("application/json")]
public class DeliveryController : ControllerBase
{
    private readonly IDeliveryApiClient _deliveryApi;
    private readonly ILogger<DeliveryController> _logger;

    public DeliveryController(IDeliveryApiClient deliveryApi, ILogger<DeliveryController> logger)
    {
        _deliveryApi = deliveryApi;
        _logger      = logger;
    }

    [HttpGet("check")]
    public async Task<IActionResult> CheckServiceability([FromQuery] string? pinCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pinCode) || pinCode.Trim().Length != 6)
            return BadRequest(new { isServiceable = false, unavailableReason = "Please provide a valid 6-digit PIN code." });

        var result = await _deliveryApi.CheckServiceabilityAsync(pinCode.Trim(), ct);
        if (result is null)
            return StatusCode(503, new { isServiceable = false, unavailableReason = "Delivery check service is currently unavailable." });

        return Ok(result);
    }

    [HttpGet("areas")]
    public async Task<IActionResult> GetAreas(CancellationToken ct = default)
    {
        var areas = await _deliveryApi.GetAreasAsync(ct);
        return Ok(areas);
    }
}
