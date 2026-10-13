using System.Security.Claims;
using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace GOLISMart.API.Controllers;

[ApiController]
[Route("api/v1/checkout")]
[Produces("application/json")]
[Authorize]
public class CheckoutController : ControllerBase
{
    private readonly ICheckoutService _checkout;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(ICheckoutService checkout, ILogger<CheckoutController> logger)
    {
        _checkout = checkout;
        _logger   = logger;
    }

    /// <summary>
    /// Validates the cart and delivery address before order placement.
    /// The customer identity is resolved from the bearer token — the client
    /// must never submit a customerId in the request body.
    /// </summary>
    [HttpPost("validate")]
    [SwaggerOperation(Summary = "Validate cart and delivery address for checkout")]
    [ProducesResponseType(typeof(CheckoutValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Validate(
        [FromBody] CheckoutValidationRequest request,
        CancellationToken ct = default)
    {
        // Resolve the authenticated customer ID from the JWT — never from the request
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Checkout validate called without a valid NameIdentifier claim");
            return Unauthorized(new ErrorResponse("Authentication required."));
        }

        if (string.IsNullOrWhiteSpace(request.SessionKey))
            return BadRequest(new ErrorResponse("SessionKey is required."));

        if (request.AddressId <= 0)
            return BadRequest(new ErrorResponse("A valid AddressId is required."));

        _logger.LogDebug("Checkout validate: userId={UserId}, session={Session}, address={AddressId}",
            userId, request.SessionKey, request.AddressId);

        // ValidateAsync catches its own exceptions and returns IsValid:false — never throws
        var result = await _checkout.ValidateAsync(request.SessionKey, request.AddressId, userId, ct);

        if (result.IsValid)
        {
            _logger.LogInformation("Checkout validate succeeded for user {UserId}", userId);
        }
        else
        {
            _logger.LogInformation(
                "Checkout validate returned issues for user {UserId}: {Issues}",
                userId, string.Join("; ", result.Issues));
        }

        return Ok(result);
    }
}
