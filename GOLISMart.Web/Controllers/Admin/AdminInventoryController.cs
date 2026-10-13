using GOLISMart.Web.Models;
using GOLISMart.Web.Services;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers.Admin;

[Authorize(Policy = "Admin")]
public class AdminInventoryController(IAdminApiClient admin, ILogger<AdminInventoryController> logger) : Controller
{
    private const string TokenCookie  = "GOLISMart_AuthToken";
    private const string IndexView    = "~/Views/Admin/Inventory/Index.cshtml";
    private const string AdjustView   = "~/Views/Admin/Inventory/Adjust.cshtml";
    private string? Token => HttpContext.Request.Cookies[TokenCookie];

    [HttpGet("admin/inventory")]
    public async Task<IActionResult> Index(string? search, bool? lowStock, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Inventory";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        try
        {
            var result = await admin.GetInventoryAsync(token, page, 30, search, lowStock, ct);
            return View(IndexView, new AdminInventoryViewModel
            {
                Inventory    = result,
                Search       = search,
                LowStock     = lowStock,
                Page         = page,
                ApiAvailable = result is not null
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Admin inventory index failed to load");
            return View(IndexView, new AdminInventoryViewModel { ApiAvailable = false });
        }
    }

    [HttpGet("admin/inventory/adjust")]
    public IActionResult Adjust(int? productId, string? productName)
    {
        ViewData["Title"] = "Adjust Inventory";
        return View(AdjustView, new AdminInventoryAdjustViewModel
        {
            ProductId   = productId ?? 0,
            ProductName = productName ?? string.Empty
        });
    }

    [HttpPost("admin/inventory/adjust")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(AdminInventoryAdjustViewModel model, CancellationToken ct = default)
    {
        ViewData["Title"] = "Adjust Inventory";
        if (!ModelState.IsValid) return View(AdjustView, model);

        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var (success, error) = await admin.AdjustInventoryAsync(
            token,
            new AdminInventoryAdjustRequest(model.ProductId, model.QuantityChange, model.Reason),
            ct);

        if (!success)
        {
            model.ErrorMessage = error ?? "Inventory adjustment failed.";
            return View(AdjustView, model);
        }

        TempData["SuccessMessage"] = "Inventory adjusted successfully.";
        return RedirectToAction(nameof(Index));
    }
}
