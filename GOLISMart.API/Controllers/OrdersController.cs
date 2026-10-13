using System.Security.Claims;
using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace GOLISMart.API.Controllers;

[ApiController]
[Route("api/v1/orders")]
[Produces("application/json")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orders;
    private readonly IDeliveryManagementService _deliveryMgmt;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IOrderService orders,
        IDeliveryManagementService deliveryMgmt,
        ILogger<OrdersController> logger)
    {
        _orders       = orders;
        _deliveryMgmt = deliveryMgmt;
        _logger       = logger;
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    // ── POST /api/v1/orders — Place an order ──────────────────────────────────

    [HttpPost]
    [SwaggerOperation(Summary = "Place a new order from the customer's cart")]
    [ProducesResponseType(typeof(PlaceOrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PlaceOrder(
        [FromBody] PlaceOrderRequest request,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("PlaceOrder called without a valid NameIdentifier claim");
            return Unauthorized(new ErrorResponse("Authentication required."));
        }

        if (string.IsNullOrWhiteSpace(request.SessionKey))
            return BadRequest(new ErrorResponse("SessionKey is required."));

        if (request.AddressId <= 0)
            return BadRequest(new ErrorResponse("A valid AddressId is required."));

        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
            return BadRequest(new ErrorResponse("PaymentMethod is required."));

        _logger.LogDebug(
            "PlaceOrder: userId={UserId}, session={Session}, address={AddressId}, payment={PaymentMethod}",
            userId, request.SessionKey, request.AddressId, request.PaymentMethod);

        try
        {
            var result = await _orders.PlaceOrderAsync(request, userId, ct);

            _logger.LogInformation(
                "Order {OrderNumber} placed for user {UserId}", result.OrderNumber, userId);

            return CreatedAtAction(
                nameof(GetOrderDetail),
                new { orderId = result.OrderId },
                result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation(ex, "PlaceOrder validation failed for user {UserId}", userId);
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation(ex, "PlaceOrder business rule failed for user {UserId}", userId);
            return UnprocessableEntity(new ErrorResponse(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PlaceOrder unexpected error for user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse("An unexpected error occurred while placing your order. Please try again."));
        }
    }

    // ── GET /api/v1/orders — List customer's orders ───────────────────────────

    [HttpGet]
    [SwaggerOperation(Summary = "Get the authenticated customer's orders (paged)")]
    [ProducesResponseType(typeof(OrdersPagedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? status = null,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo   = null,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ErrorResponse("Authentication required."));

        DateTime? from = DateTime.TryParse(dateFrom, out var df) ? df : null;
        DateTime? to   = DateTime.TryParse(dateTo,   out var dt) ? dt : null;

        try
        {
            var result = await _orders.GetMyOrdersAsync(userId, page, pageSize, status, from, to, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMyOrders failed for user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse("Failed to load orders. Please try again."));
        }
    }

    // ── GET /api/v1/orders/{orderId} — Get order detail ───────────────────────

    [HttpGet("{orderId:int}")]
    [SwaggerOperation(Summary = "Get the detail of a specific order belonging to the authenticated customer")]
    [ProducesResponseType(typeof(OrderDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetOrderDetail(
        int orderId,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ErrorResponse("Authentication required."));

        try
        {
            var result = await _orders.GetOrderDetailAsync(orderId, userId, ct);
            if (result is null)
                return NotFound(new ErrorResponse("Order not found."));

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetOrderDetail failed for orderId={OrderId}, user {UserId}", orderId, userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse("Failed to load order details. Please try again."));
        }
    }

    // ── POST /api/v1/orders/{orderId}/cancel — Cancel an order ────────────────

    [HttpPost("{orderId:int}/cancel")]
    [SwaggerOperation(Summary = "Cancel an eligible order (Pending or Confirmed only)")]
    [ProducesResponseType(typeof(CancelOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CancelOrder(
        int orderId,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ErrorResponse("Authentication required."));

        try
        {
            var result = await _orders.CancelOrderAsync(orderId, userId, ct);

            if (!result.Success)
            {
                if (result.NewStatus is null)
                    return NotFound(new ErrorResponse(result.Message));

                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CancelOrder failed for orderId={OrderId}, user {UserId}", orderId, userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse("Failed to cancel order. Please try again."));
        }
    }

    // ── GET /api/v1/orders/{orderId}/tracking — Delivery tracking ─────────────

    [HttpGet("{orderId:int}/tracking")]
    [SwaggerOperation(Summary = "Get delivery tracking timeline for a customer's order")]
    [ProducesResponseType(typeof(DeliveryTrackingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTracking(int orderId, CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ErrorResponse("Authentication required."));

        var tracking = await _deliveryMgmt.GetTrackingForOrderAsync(orderId, userId, ct);
        return tracking is null ? NotFound() : Ok(tracking);
    }

    // ── GET /api/v1/orders/notifications — In-app inbox ───────────────────────

    [HttpGet("notifications")]
    [SwaggerOperation(Summary = "Get in-app delivery notifications for the authenticated customer")]
    [ProducesResponseType(typeof(IReadOnlyList<InAppNotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ErrorResponse("Authentication required."));

        limit = Math.Clamp(limit, 1, 50);
        var notifications = await _deliveryMgmt.GetInAppNotificationsAsync(userId, limit, ct);
        return Ok(notifications);
    }
}
