using GOLISMart.Web.Models;
using GOLISMart.Web.Services;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers;

[Authorize(Policy = "DeliveryPersonnel")]
public class DeliveryDashboardController(
    IDeliveryManagementApiClient deliveryApi,
    ILogger<DeliveryDashboardController> logger) : Controller
{
    private const string TokenCookie = "GOLISMart_AuthToken";
    private string? Token => HttpContext.Request.Cookies[TokenCookie];

    [HttpGet("delivery/dashboard")]
    public async Task<IActionResult> Index(string? status = null, CancellationToken ct = default)
    {
        ViewData["Title"] = "My Deliveries";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        try
        {
            var assignments = await deliveryApi.GetMyAssignmentsAsync(token, status, ct);
            return View("~/Views/Delivery/Dashboard.cshtml", new DeliveryDashboardViewModel
            {
                Assignments  = assignments ?? [],
                StatusFilter = status,
                ApiAvailable = assignments is not null
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Delivery dashboard failed");
            return View("~/Views/Delivery/Dashboard.cshtml", new DeliveryDashboardViewModel { ApiAvailable = false });
        }
    }

    [HttpGet("delivery/{orderId:int}")]
    public async Task<IActionResult> Detail(int orderId, CancellationToken ct = default)
    {
        ViewData["Title"] = $"Delivery #{orderId}";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var assignment = await deliveryApi.GetMyAssignmentDetailAsync(token, orderId, ct);
        if (assignment is null) return NotFound();

        return View("~/Views/Delivery/Detail.cshtml", new DeliveryDetailViewModel { Assignment = assignment });
    }

    [HttpPost("delivery/{orderId:int}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int orderId, DeliveryDetailViewModel model, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        if (string.IsNullOrWhiteSpace(model.StatusNotes) && IsFailureStatus(Request.Form["newStatus"].ToString()))
        {
            TempData["ErrorMessage"] = "Please provide a reason for the failed delivery.";
            return RedirectToAction(nameof(Detail), new { orderId });
        }

        var newStatus = Request.Form["newStatus"].ToString();

        // Delivered must go through confirm endpoint
        if (string.Equals(newStatus, "Delivered", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Please use the 'Confirm Delivered' button to mark as delivered.";
            return RedirectToAction(nameof(Detail), new { orderId });
        }

        var (success, error) = await deliveryApi.UpdateDeliveryStatusAsync(
            token, orderId,
            new UpdateDeliveryStatusRequest(newStatus, model.StatusNotes),
            ct);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] =
            success ? $"Status updated to {newStatus}." : (error ?? "Update failed.");
        return RedirectToAction(nameof(Detail), new { orderId });
    }

    [HttpPost("delivery/{orderId:int}/confirm-delivered")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmDelivered(int orderId, DeliveryDetailViewModel model, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        if (!model.DeliveredConfirmed)
        {
            TempData["ErrorMessage"] = "Please tick the confirmation checkbox.";
            return RedirectToAction(nameof(Detail), new { orderId });
        }

        var (success, error) = await deliveryApi.ConfirmDeliveredAsync(
            token, orderId,
            new ConfirmDeliveredRequest(true, model.StatusNotes),
            ct);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] =
            success ? "Order marked as Delivered." : (error ?? "Confirmation failed.");

        if (success) return RedirectToAction(nameof(Index));
        return RedirectToAction(nameof(Detail), new { orderId });
    }

    private static bool IsFailureStatus(string status) =>
        string.Equals(status, "DeliveryFailed", StringComparison.OrdinalIgnoreCase);
}
