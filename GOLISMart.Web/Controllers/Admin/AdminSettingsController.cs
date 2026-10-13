using GOLISMart.Web.Models;
using GOLISMart.Web.Services;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers.Admin;

[Authorize(Policy = "Admin")]
public class AdminSettingsController(IAdminApiClient admin, ILogger<AdminSettingsController> logger) : Controller
{
    private const string TokenCookie = "GOLISMart_AuthToken";
    private const string IndexView   = "~/Views/Admin/Settings/Index.cshtml";
    private string? Token => HttpContext.Request.Cookies[TokenCookie];

    [HttpGet("admin/settings")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        ViewData["Title"] = "Settings";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        try
        {
            var settings = await admin.GetSettingsAsync(token, ct);
            return View(IndexView, new AdminSettingsViewModel
            {
                Settings     = settings ?? [],
                ApiAvailable = settings is not null
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Admin settings failed to load");
            return View(IndexView, new AdminSettingsViewModel { Settings = [], ApiAvailable = false });
        }
    }

    [HttpPost("admin/settings")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Dictionary<string, string> values, CancellationToken ct = default)
    {
        ViewData["Title"] = "Settings";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var items = values.Select(kv => new AdminSettingUpdateItem(kv.Key, kv.Value)).ToList();
        var (success, error) = await admin.UpdateSettingsAsync(token, new AdminUpdateSettingsRequest(items), ct);

        if (!success)
        {
            TempData["ErrorMessage"] = error ?? "Failed to save settings.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = "Settings saved successfully.";
        return RedirectToAction(nameof(Index));
    }
}
