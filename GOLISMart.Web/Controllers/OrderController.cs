using GOLISMart.Web.Models;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers;

[Authorize]
public class OrderController : Controller
{
    private readonly IOrderApiClient _orderApi;
    private readonly IDeliveryManagementApiClient _deliveryApi;
    private readonly ILogger<OrderController> _logger;

    private const string TokenCookieName = "GOLISMart_AuthToken";

    public OrderController(
        IOrderApiClient orderApi,
        IDeliveryManagementApiClient deliveryApi,
        ILogger<OrderController> logger)
    {
        _orderApi    = orderApi;
        _deliveryApi = deliveryApi;
        _logger      = logger;
    }

    private string? GetToken() => HttpContext.Request.Cookies[TokenCookieName];

    // ── Order Confirmation page ──────────────────────────────────────────────

    [HttpGet("order/confirmation/{orderId:int}")]
    public async Task<IActionResult> Confirmation(int orderId, CancellationToken ct = default)
    {
        var token = GetToken();
        if (token is null)
            return RedirectToAction("Login", "Account", new { returnUrl = $"/order/confirmation/{orderId}" });

        try
        {
            var detail = await _orderApi.GetOrderDetailAsync(token, orderId, ct);

            if (detail is null)
            {
                _logger.LogWarning("Confirmation: order {OrderId} not found for user", orderId);
                TempData["OrderError"] = "Order not found. Please check My Orders.";
                return RedirectToAction(nameof(MyOrders));
            }

            return View(new OrderConfirmationViewModel
            {
                Order        = detail,
                ApiAvailable = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Confirmation page failed for orderId={OrderId}", orderId);
            return View(new OrderConfirmationViewModel
            {
                ApiAvailable = false,
                ErrorMessage = "Unable to load order confirmation. Please check My Orders."
            });
        }
    }

    // ── My Orders page ────────────────────────────────────────────────────────

    [HttpGet("orders")]
    public async Task<IActionResult> MyOrders(
        [FromQuery] int page = 1,
        [FromQuery] string? status = null,
        [FromQuery] string? dateFrom = null,
        [FromQuery] string? dateTo   = null,
        CancellationToken ct = default)
    {
        var token = GetToken();
        if (token is null)
            return RedirectToAction("Login", "Account", new { returnUrl = "/orders" });

        DateTime? from = DateTime.TryParse(dateFrom, out var df) ? df : null;
        DateTime? to   = DateTime.TryParse(dateTo,   out var dt) ? dt : null;

        try
        {
            var result = await _orderApi.GetMyOrdersAsync(token, page, 10, status, from, to, ct);

            return View(new MyOrdersViewModel
            {
                PagedOrders  = result,
                ApiAvailable = result is not null,
                StatusFilter = status,
                DateFrom     = dateFrom,
                DateTo       = dateTo,
                CurrentPage  = page,
                ErrorMessage = result is null ? "Unable to load orders. Please try again." : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyOrders page failed for user");
            return View(new MyOrdersViewModel
            {
                ApiAvailable = false,
                ErrorMessage = "Unable to load orders. Please try again."
            });
        }
    }

    // ── Order Detail page ─────────────────────────────────────────────────────

    [HttpGet("order/{orderId:int}")]
    public async Task<IActionResult> Detail(int orderId, CancellationToken ct = default)
    {
        var token = GetToken();
        if (token is null)
            return RedirectToAction("Login", "Account", new { returnUrl = $"/order/{orderId}" });

        try
        {
            var detailTask   = _orderApi.GetOrderDetailAsync(token, orderId, ct);
            var trackingTask = _deliveryApi.GetTrackingAsync(token, orderId, ct);
            await Task.WhenAll(detailTask, trackingTask);
            var detail   = detailTask.Result;
            var tracking = trackingTask.Result;

            if (detail is null)
            {
                TempData["OrderError"] = "Order not found.";
                return RedirectToAction(nameof(MyOrders));
            }

            return View(new OrderDetailViewModel
            {
                Order          = detail,
                Tracking       = tracking,
                ApiAvailable   = true,
                SuccessMessage = TempData["CancelSuccess"] as string
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order detail page failed for orderId={OrderId}", orderId);
            return View(new OrderDetailViewModel
            {
                ApiAvailable = false,
                ErrorMessage = "Unable to load order details. Please try again."
            });
        }
    }

    // ── Cancel order ──────────────────────────────────────────────────────────

    [HttpPost("order/{orderId:int}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int orderId, CancellationToken ct = default)
    {
        var token = GetToken();
        if (token is null)
            return RedirectToAction("Login", "Account");

        try
        {
            var result = await _orderApi.CancelOrderAsync(token, orderId, ct);

            if (result is null || !result.Success)
            {
                TempData["OrderError"] = result?.Message ?? "Unable to cancel order. Please try again.";
            }
            else
            {
                TempData["CancelSuccess"] = "Your order has been cancelled successfully.";
            }

            return RedirectToAction(nameof(Detail), new { orderId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cancel order failed for orderId={OrderId}", orderId);
            TempData["OrderError"] = "Unable to cancel order. Please try again.";
            return RedirectToAction(nameof(Detail), new { orderId });
        }
    }
}
