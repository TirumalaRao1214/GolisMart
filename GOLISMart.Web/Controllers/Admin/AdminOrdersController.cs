using GOLISMart.Web.Models;
using GOLISMart.Web.Services;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers.Admin;

[Authorize(Policy = "Admin")]
public class AdminOrdersController(IAdminApiClient admin, ILogger<AdminOrdersController> logger) : Controller
{
    private const string TokenCookie  = "GOLISMart_AuthToken";
    private const string IndexView    = "~/Views/Admin/Orders/Index.cshtml";
    private const string DetailView   = "~/Views/Admin/Orders/Detail.cshtml";
    private string? Token => HttpContext.Request.Cookies[TokenCookie];

    [HttpGet("admin/orders")]
    public async Task<IActionResult> Index(
        string? search, string? status, string? paymentStatus, string? from, string? to,
        int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Orders";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        try
        {
            DateTime? fromDate = DateTime.TryParse(from, out var f) ? f : null;
            DateTime? toDate   = DateTime.TryParse(to,   out var t) ? t : null;

            var result = await admin.GetAdminOrdersAsync(token, page, 20, search, status, paymentStatus, fromDate, toDate, ct);
            return View(IndexView, new AdminOrderListViewModel
            {
                Orders        = result,
                Search        = search,
                Status        = status,
                PaymentStatus = paymentStatus,
                From          = from,
                To            = to,
                Page          = page,
                ApiAvailable  = result is not null
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Admin orders index failed to load");
            return View(IndexView, new AdminOrderListViewModel { ApiAvailable = false });
        }
    }

    [HttpGet("admin/orders/{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct = default)
    {
        ViewData["Title"] = $"Order #{id}";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var order = await admin.GetAdminOrderDetailAsync(token, id, ct);
        if (order is null) return NotFound();

        return View(DetailView, new AdminOrderDetailViewModel { Order = order });
    }

    [HttpPost("admin/orders/{id:int}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, AdminOrderDetailViewModel model, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        if (string.IsNullOrWhiteSpace(model.NewStatus))
        {
            TempData["ErrorMessage"] = "Please select a new status.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var (success, error) = await admin.UpdateOrderStatusAsync(
            token, id,
            new AdminUpdateOrderStatusRequest(model.NewStatus, model.StatusNotes),
            ct);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] =
            success ? "Order status updated." : (error ?? "Status update failed.");
        return RedirectToAction(nameof(Detail), new { id });
    }
}
