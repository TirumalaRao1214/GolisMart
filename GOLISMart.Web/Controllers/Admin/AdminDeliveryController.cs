using GOLISMart.Web.Models;
using GOLISMart.Web.Services;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers.Admin;

[Authorize(Policy = "Admin")]
public class AdminDeliveryController(
    IDeliveryManagementApiClient deliveryApi,
    ILogger<AdminDeliveryController> logger) : Controller
{
    private const string TokenCookie = "GOLISMart_AuthToken";
    private string? Token => HttpContext.Request.Cookies[TokenCookie];

    // ── Delivery Orders ───────────────────────────────────────────────────────

    [HttpGet("admin/delivery")]
    public async Task<IActionResult> Index(
        string? search, string? status, string? from, string? to,
        string? pincode, int? personnelId,
        int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Delivery Management";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        try
        {
            DateTime? fromDate = DateTime.TryParse(from, out var f) ? f : null;
            DateTime? toDate   = DateTime.TryParse(to,   out var t) ? t : null;

            var ordersTask    = deliveryApi.GetDeliveryOrdersAsync(token, page, 20, search, status, fromDate, toDate, pincode, personnelId, ct);
            var personnelTask = deliveryApi.GetPersonnelAsync(token, 1, 200, null, true, ct);
            await Task.WhenAll(ordersTask, personnelTask);
            var orders      = ordersTask.Result;
            var personnelList = personnelTask.Result;

            return View("~/Views/Admin/Delivery/Index.cshtml", new AdminDeliveryListViewModel
            {
                Orders      = orders,
                Personnel   = personnelList?.Items ?? [],
                Search      = search,
                Status      = status,
                From        = from,
                To          = to,
                Pincode     = pincode,
                PersonnelId = personnelId,
                Page        = page,
                ApiAvailable = orders is not null
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Admin delivery index failed");
            return View("~/Views/Admin/Delivery/Index.cshtml", new AdminDeliveryListViewModel { ApiAvailable = false });
        }
    }

    [HttpGet("admin/delivery/{orderId:int}")]
    public async Task<IActionResult> Detail(int orderId, CancellationToken ct = default)
    {
        ViewData["Title"] = $"Delivery Order #{orderId}";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var orderTask2     = deliveryApi.GetDeliveryOrderDetailAsync(token, orderId, ct);
        var personnelTask2 = deliveryApi.GetPersonnelAsync(token, 1, 200, null, true, ct);
        await Task.WhenAll(orderTask2, personnelTask2);
        var order       = orderTask2.Result;
        var personnelList = personnelTask2.Result;

        if (order is null) return NotFound();

        return View("~/Views/Admin/Delivery/Detail.cshtml", new AdminDeliveryDetailViewModel
        {
            Order     = order,
            Personnel = personnelList?.Items ?? [],
        });
    }

    [HttpPost("admin/delivery/{orderId:int}/assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int orderId, AdminDeliveryDetailViewModel model, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        if (!model.AssignPersonnelId.HasValue || model.AssignPersonnelId <= 0)
        {
            TempData["ErrorMessage"] = "Please select a delivery person.";
            return RedirectToAction(nameof(Detail), new { orderId });
        }

        var (success, error) = await deliveryApi.AssignOrderAsync(
            token, orderId,
            new AdminAssignDeliveryRequest(model.AssignPersonnelId.Value, model.AssignNotes),
            ct);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] =
            success ? "Delivery person assigned." : (error ?? "Assignment failed.");
        return RedirectToAction(nameof(Detail), new { orderId });
    }

    [HttpPost("admin/delivery/{orderId:int}/unassign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unassign(int orderId, AdminDeliveryDetailViewModel model, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        if (string.IsNullOrWhiteSpace(model.UnassignReason))
        {
            TempData["ErrorMessage"] = "Please provide a reason for unassigning.";
            return RedirectToAction(nameof(Detail), new { orderId });
        }

        var (success, error) = await deliveryApi.UnassignOrderAsync(
            token, orderId,
            new AdminUnassignDeliveryRequest(model.UnassignReason),
            ct);

        TempData[success ? "SuccessMessage" : "ErrorMessage"] =
            success ? "Delivery person unassigned." : (error ?? "Unassign failed.");
        return RedirectToAction(nameof(Detail), new { orderId });
    }

    // ── Delivery Personnel ────────────────────────────────────────────────────

    [HttpGet("admin/delivery/personnel")]
    public async Task<IActionResult> Personnel(
        string? search, bool? isActive, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Delivery Personnel";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var result = await deliveryApi.GetPersonnelAsync(token, page, 20, search, isActive, ct);
        return View("~/Views/Admin/Delivery/Personnel.cshtml", new AdminPersonnelListViewModel
        {
            Personnel    = result,
            Search       = search,
            IsActive     = isActive,
            Page         = page,
            ApiAvailable = result is not null
        });
    }

    [HttpGet("admin/delivery/personnel/create")]
    public IActionResult CreatePersonnel()
    {
        ViewData["Title"] = "Add Delivery Person";
        return View("~/Views/Admin/Delivery/PersonnelForm.cshtml", new AdminPersonnelFormViewModel());
    }

    [HttpPost("admin/delivery/personnel/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePersonnel(AdminPersonnelFormViewModel model, CancellationToken ct = default)
    {
        ViewData["Title"] = "Add Delivery Person";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        if (!ModelState.IsValid)
            return View("~/Views/Admin/Delivery/PersonnelForm.cshtml", model);

        var (success, error, _) = await deliveryApi.CreatePersonnelAsync(
            token,
            new AdminCreatePersonnelRequest(model.FullName, model.ContactNumber, model.UserId, model.Notes),
            ct);

        if (!success)
        {
            model.ErrorMessage = error;
            return View("~/Views/Admin/Delivery/PersonnelForm.cshtml", model);
        }

        TempData["SuccessMessage"] = $"Delivery person '{model.FullName}' created.";
        return RedirectToAction(nameof(Personnel));
    }

    [HttpGet("admin/delivery/personnel/{id:int}/edit")]
    public async Task<IActionResult> EditPersonnel(int id, CancellationToken ct = default)
    {
        ViewData["Title"] = "Edit Delivery Person";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var person = await deliveryApi.GetPersonnelByIdAsync(token, id, ct);
        if (person is null) return NotFound();

        return View("~/Views/Admin/Delivery/PersonnelForm.cshtml", new AdminPersonnelFormViewModel
        {
            PersonnelId   = id,
            FullName      = person.FullName,
            ContactNumber = person.ContactNumber,
            UserId        = person.UserId,
            Notes         = person.Notes,
            IsActive      = person.IsActive
        });
    }

    [HttpPost("admin/delivery/personnel/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPersonnel(int id, AdminPersonnelFormViewModel model, CancellationToken ct = default)
    {
        ViewData["Title"] = "Edit Delivery Person";
        model.PersonnelId = id;
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        if (!ModelState.IsValid)
            return View("~/Views/Admin/Delivery/PersonnelForm.cshtml", model);

        var (success, error) = await deliveryApi.UpdatePersonnelAsync(
            token, id,
            new AdminUpdatePersonnelRequest(model.FullName, model.ContactNumber, model.UserId, model.Notes, model.IsActive),
            ct);

        if (!success)
        {
            model.ErrorMessage = error;
            return View("~/Views/Admin/Delivery/PersonnelForm.cshtml", model);
        }

        TempData["SuccessMessage"] = "Delivery person updated.";
        return RedirectToAction(nameof(Personnel));
    }
}
