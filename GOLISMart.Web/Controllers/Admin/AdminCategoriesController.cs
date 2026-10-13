using GOLISMart.Web.Models;
using GOLISMart.Web.Services;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers.Admin;

[Authorize(Policy = "Admin")]
public class AdminCategoriesController(IAdminApiClient admin, ILogger<AdminCategoriesController> logger) : Controller
{
    private const string TokenCookie = "GOLISMart_AuthToken";
    private const string IndexView   = "~/Views/Admin/Categories/Index.cshtml";
    private const string FormView    = "~/Views/Admin/Categories/Form.cshtml";
    private string? Token => HttpContext.Request.Cookies[TokenCookie];

    [HttpGet("admin/categories")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Categories";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        try
        {
            var result = await admin.GetCategoriesAsync(token, page, 50, ct);
            return View(IndexView, new AdminCategoryListViewModel { Categories = result, Page = page, ApiAvailable = result is not null });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Admin categories index failed to load");
            return View(IndexView, new AdminCategoryListViewModel { ApiAvailable = false });
        }
    }

    [HttpGet("admin/categories/create")]
    public IActionResult Create()
    {
        ViewData["Title"] = "Create Category";
        return View(FormView, new AdminCategoryFormViewModel());
    }

    [HttpPost("admin/categories/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminCategoryFormViewModel model, CancellationToken ct = default)
    {
        ViewData["Title"] = "Create Category";
        if (!ModelState.IsValid) return View(FormView, model);

        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var (success, error, _) = await admin.CreateCategoryAsync(
            token,
            new AdminCreateCategoryRequest(model.Name, model.Slug, model.Description, model.SortOrder),
            ct);

        if (!success)
        {
            model.ErrorMessage = error ?? "Failed to create category.";
            return View(FormView, model);
        }

        TempData["SuccessMessage"] = $"Category '{model.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("admin/categories/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
    {
        ViewData["Title"] = "Edit Category";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var cats = await admin.GetCategoriesAsync(token, 1, 200, ct);
        var cat = cats?.Items.FirstOrDefault(c => c.Id == id);
        if (cat is null) return NotFound();

        return View(FormView, new AdminCategoryFormViewModel
        {
            CategoryId  = cat.Id,
            Name        = cat.Name,
            Slug        = cat.Slug,
            Description = cat.Description,
            SortOrder   = cat.SortOrder,
            IsActive    = cat.IsActive
        });
    }

    [HttpPost("admin/categories/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminCategoryFormViewModel model, CancellationToken ct = default)
    {
        ViewData["Title"] = "Edit Category";
        model.CategoryId = id;
        if (!ModelState.IsValid) return View(FormView, model);

        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var (success, error) = await admin.UpdateCategoryAsync(
            token, id,
            new AdminUpdateCategoryRequest(model.Name, model.Description, model.SortOrder, model.IsActive),
            ct);

        if (!success)
        {
            model.ErrorMessage = error ?? "Failed to update category.";
            return View(FormView, model);
        }

        TempData["SuccessMessage"] = "Category updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("admin/categories/{id:int}/toggle-active")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var (success, error) = await admin.ToggleCategoryActiveAsync(token, id, ct);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] =
            success ? "Category status updated." : (error ?? "Could not toggle category.");
        return RedirectToAction(nameof(Index));
    }
}
