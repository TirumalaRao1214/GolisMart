using System.Security.Claims;
using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.API.Controllers;

/// <summary>
/// Endpoints for authenticated delivery personnel to view and update their own deliveries.
/// Access is restricted to users with the DeliveryPersonnel role.
/// </summary>
[ApiController]
[Route("api/v1/deliveries")]
[Produces("application/json")]
[Authorize(Roles = "DeliveryPersonnel")]
public class DeliveryPersonnelController : ControllerBase
{
    private readonly IDeliveryManagementService _delivery;

    public DeliveryPersonnelController(IDeliveryManagementService delivery)
        => _delivery = delivery;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>Returns all active assignments for the authenticated delivery person.</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(IReadOnlyList<DeliveryAssignmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAssignments(
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var assignments = await _delivery.GetMyAssignmentsAsync(UserId, status, ct);
        return Ok(assignments);
    }

    /// <summary>Returns details for a single assigned order.</summary>
    [HttpGet("my/{orderId:int}")]
    [ProducesResponseType(typeof(DeliveryAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyAssignment(int orderId, CancellationToken ct)
    {
        var detail = await _delivery.GetMyAssignmentDetailAsync(orderId, UserId, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>
    /// Advances delivery status. Valid transitions:
    /// Confirmed/Processing → PickedUp → OutForDelivery → Delivered
    /// PickedUp/OutForDelivery → DeliveryFailed (with reason in notes)
    /// </summary>
    [HttpPost("my/{orderId:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateStatus(
        int orderId,
        [FromBody] UpdateDeliveryStatusRequest request,
        CancellationToken ct)
    {
        if (string.Equals(request.NewStatus, "Delivered", StringComparison.OrdinalIgnoreCase)
            && request.Notes?.Contains("CONFIRMED") != true)
        {
            // Require explicit confirmation payload for Delivered
            return UnprocessableEntity(new
            {
                message = "Marking Delivered requires an explicit confirmation. " +
                          "Use the /confirm-delivered endpoint."
            });
        }

        var (success, error) = await _delivery.UpdateDeliveryStatusAsync(
            orderId, UserId, request.NewStatus, request.Notes, ct);

        return success ? NoContent() : UnprocessableEntity(new { message = error });
    }

    /// <summary>
    /// Marks an order as Delivered with an explicit confirmation step.
    /// </summary>
    [HttpPost("my/{orderId:int}/confirm-delivered")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmDelivered(
        int orderId,
        [FromBody] ConfirmDeliveredRequest request,
        CancellationToken ct)
    {
        if (!request.Confirmed)
            return UnprocessableEntity(new { message = "Confirmation was not provided." });

        var notes = request.Notes ?? "Delivered — CONFIRMED";
        if (!notes.Contains("CONFIRMED")) notes += " — CONFIRMED";

        var (success, error) = await _delivery.UpdateDeliveryStatusAsync(
            orderId, UserId, "Delivered", notes, ct);

        return success ? NoContent() : UnprocessableEntity(new { message = error });
    }
}
