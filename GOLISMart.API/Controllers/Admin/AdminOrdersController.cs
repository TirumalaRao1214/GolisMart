using System.Security.Claims;
using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/orders")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class AdminOrdersController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminOrdersController(IAdminService admin)
    {
        _admin = admin;
    }

    /// <summary>
    /// Returns a paged list of orders with optional search, status and date filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? paymentStatus,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (orders, total) = await _admin.GetOrdersAsync(search, status, paymentStatus, from, to, page, pageSize, ct);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        return Ok(new PagedResult<AdminOrderListDto>(orders, page, pageSize, total, totalPages));
    }

    /// <summary>
    /// Returns full details of a single order including items and status history.
    /// </summary>
    [HttpGet("{orderId:int}")]
    [ProducesResponseType(typeof(AdminOrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(int orderId, CancellationToken ct)
    {
        var order = await _admin.GetOrderDetailAsync(orderId, ct);
        return order is null ? NotFound() : Ok(order);
    }

    /// <summary>
    /// Advances or changes an order's status. Validates allowed transitions server-side.
    /// </summary>
    [HttpPost("{orderId:int}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateOrderStatus(
        int orderId,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var (success, error) = await _admin.UpdateOrderStatusAsync(orderId, request.NewStatus, adminId, request.Notes, ct);

        if (!success)
        {
            if (error?.Contains("not found") == true)
                return NotFound(new { message = error });
            return UnprocessableEntity(new { message = error });
        }

        return NoContent();
    }
}
