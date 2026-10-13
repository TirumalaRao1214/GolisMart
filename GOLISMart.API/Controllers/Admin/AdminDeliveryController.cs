using System.Security.Claims;
using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.API.Controllers.Admin;

/// <summary>Admin endpoints for delivery management and delivery personnel.</summary>
[ApiController]
[Route("api/v1/admin/delivery")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class AdminDeliveryController : ControllerBase
{
    private readonly IDeliveryManagementService _delivery;

    public AdminDeliveryController(IDeliveryManagementService delivery)
        => _delivery = delivery;

    private string AdminId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // ── Delivery orders list ──────────────────────────────────────────────────

    [HttpGet("orders")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? pincode,
        [FromQuery] int? personnelId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (orders, total) = await _delivery.GetDeliveryOrdersAsync(
            search, status, from, to, pincode, personnelId, page, pageSize, ct);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        return Ok(new PagedResult<AdminDeliveryOrderListDto>(orders, page, pageSize, total, totalPages));
    }

    [HttpGet("orders/{orderId:int}")]
    [ProducesResponseType(typeof(AdminDeliveryOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(int orderId, CancellationToken ct)
    {
        var order = await _delivery.GetDeliveryOrderDetailAsync(orderId, ct);
        return order is null ? NotFound() : Ok(order);
    }

    // ── Assignment management ─────────────────────────────────────────────────

    [HttpPost("orders/{orderId:int}/assign")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Assign(
        int orderId,
        [FromBody] AssignDeliveryRequest request,
        CancellationToken ct)
    {
        var (success, error) = await _delivery.AssignOrderAsync(
            orderId, request.DeliveryPersonnelId, AdminId, request.Notes, ct);

        return success ? NoContent() : UnprocessableEntity(new { message = error });
    }

    [HttpPost("orders/{orderId:int}/unassign")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Unassign(
        int orderId,
        [FromBody] UnassignDeliveryRequest request,
        CancellationToken ct)
    {
        var (success, error) = await _delivery.UnassignOrderAsync(orderId, AdminId, request.Reason, ct);
        return success ? NoContent() : UnprocessableEntity(new { message = error });
    }

    // ── Delivery Personnel CRUD ───────────────────────────────────────────────

    [HttpGet("personnel")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPersonnel(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, total) = await _delivery.GetPersonnelAsync(search, isActive, page, pageSize, ct);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        return Ok(new PagedResult<DeliveryPersonnelDto>(items, page, pageSize, total, totalPages));
    }

    [HttpGet("personnel/{id:int}")]
    [ProducesResponseType(typeof(DeliveryPersonnelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPersonnelById(int id, CancellationToken ct)
    {
        var item = await _delivery.GetPersonnelByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("personnel")]
    [ProducesResponseType(typeof(DeliveryPersonnelDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreatePersonnel(
        [FromBody] CreateDeliveryPersonnelRequest request,
        CancellationToken ct)
    {
        var (success, error, newId) = await _delivery.CreatePersonnelAsync(request, AdminId, ct);
        if (!success) return UnprocessableEntity(new { message = error });

        var created = await _delivery.GetPersonnelByIdAsync(newId, ct);
        return CreatedAtAction(nameof(GetPersonnelById), new { id = newId }, created);
    }

    [HttpPut("personnel/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdatePersonnel(
        int id,
        [FromBody] UpdateDeliveryPersonnelRequest request,
        CancellationToken ct)
    {
        var (success, error) = await _delivery.UpdatePersonnelAsync(id, request, AdminId, ct);
        if (!success)
        {
            if (error?.Contains("not found") == true) return NotFound(new { message = error });
            return UnprocessableEntity(new { message = error });
        }
        return NoContent();
    }
}
