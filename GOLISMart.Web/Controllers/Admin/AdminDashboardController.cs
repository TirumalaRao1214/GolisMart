using GOLISMart.Web.Models;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers.Admin;

[Authorize(Policy = "Admin")]
public class AdminDashboardController(IAdminApiClient admin, ILogger<AdminDashboardController> logger) : Controller
{
    private const string TokenCookie = "GOLISMart_AuthToken";
    private string? Token => HttpContext.Request.Cookies[TokenCookie];

    [HttpGet("admin")]
    public IActionResult Index() => RedirectToAction(nameof(Dashboard));

    [HttpGet("admin/dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct = default)
    {
        ViewData["Title"] = "Dashboard";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        try
        {
            var summary = await admin.GetDashboardAsync(token, ct);
            return View("~/Views/Admin/Dashboard/Index.cshtml",
                new AdminDashboardViewModel { Summary = summary, ApiAvailable = summary is not null });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Admin dashboard failed to load");
            return View("~/Views/Admin/Dashboard/Index.cshtml",
                new AdminDashboardViewModel { ApiAvailable = false });
        }
    }
}
