using GOLISMart.Web.Models;
using GOLISMart.Web.Services;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers.Admin;

[Authorize(Policy = "Admin")]
public class AdminProductsController(IAdminApiClient admin, ILogger<AdminProductsController> logger) : Controller
{
    private const string TokenCookie = "GOLISMart_AuthToken";
    private const string IndexView   = "~/Views/Admin/Products/Index.cshtml";
    private const string FormView    = "~/Views/Admin/Products/Form.cshtml";
    private string? Token => HttpContext.Request.Cookies[TokenCookie];

    // ── List ──────────────────────────────────────────────────────────────────

    [HttpGet("admin/products")]
    public async Task<IActionResult> Index(string? search, int? categoryId, bool? isActive, int page = 1, CancellationToken ct = default)
    {
        ViewData["Title"] = "Products";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        try
        {
            var productsTask   = admin.GetProductsAsync(token, page, 20, search, categoryId, isActive, ct);
            var categoriesTask = admin.GetCategoriesAsync(token, 1, 200, ct);
            await Task.WhenAll(productsTask, categoriesTask);

            return View(IndexView, new AdminProductListViewModel
            {
                Products     = productsTask.Result,
                Categories   = categoriesTask.Result?.Items ?? [],
                Search       = search,
                CategoryId   = categoryId,
                IsActive     = isActive,
                Page         = page,
                ApiAvailable = productsTask.Result is not null
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Admin products index failed to load");
            return View(IndexView, new AdminProductListViewModel { ApiAvailable = false });
        }
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [HttpGet("admin/products/create")]
    public async Task<IActionResult> Create(CancellationToken ct = default)
    {
        ViewData["Title"] = "Create Product";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var cats = await admin.GetCategoriesAsync(token, 1, 200, ct);
        return View(FormView, new AdminProductFormViewModel { Categories = cats?.Items ?? [] });
    }

    [HttpPost("admin/products/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminProductFormViewModel model, IFormFile? image, CancellationToken ct = default)
    {
        ViewData["Title"] = "Create Product";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        if (!ModelState.IsValid)
        {
            var cats = await admin.GetCategoriesAsync(token, 1, 200, ct);
            model.Categories = cats?.Items ?? [];
            return View(FormView, model);
        }

        using var form = BuildProductForm(model, image);
        var created = await admin.CreateProductAsync(token, form, ct);
        if (created is null)
        {
            model.ErrorMessage = "Failed to create product. Please try again.";
            var cats = await admin.GetCategoriesAsync(token, 1, 200, ct);
            model.Categories = cats?.Items ?? [];
            return View(FormView, model);
        }

        TempData["SuccessMessage"] = $"Product '{created.Name}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ── Edit ──────────────────────────────────────────────────────────────────

    [HttpGet("admin/products/{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct = default)
    {
        ViewData["Title"] = "Edit Product";
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var productTask    = admin.GetProductByIdAsync(token, id, ct);
        var categoriesTask = admin.GetCategoriesAsync(token, 1, 200, ct);
        await Task.WhenAll(productTask, categoriesTask);

        var p = productTask.Result;
        if (p is null) return NotFound();

        return View(FormView, new AdminProductFormViewModel
        {
            ProductId         = p.Id,
            Name              = p.Name,
            SKU               = p.SKU,
            Description       = p.Description,
            CategoryId        = p.CategoryId,
            UnitCode          = p.UnitCode,
            UnitLabel         = p.UnitLabel,
            Price             = p.Price,
            OfferPrice        = p.OfferPrice,
            IsFeatured        = p.IsFeatured,
            IsActive          = p.IsActive,
            InitialStock      = p.QuantityOnHand,
            LowStockThreshold = p.LowStockThreshold,
            ExistingImagePath = p.ImagePath,
            Categories        = categoriesTask.Result?.Items ?? []
        });
    }

    [HttpPost("admin/products/{id:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminProductFormViewModel model, IFormFile? image, CancellationToken ct = default)
    {
        ViewData["Title"] = "Edit Product";
        model.ProductId = id;
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        if (!ModelState.IsValid)
        {
            var cats = await admin.GetCategoriesAsync(token, 1, 200, ct);
            model.Categories = cats?.Items ?? [];
            return View(FormView, model);
        }

        using var form = BuildProductForm(model, image);
        var (success, error) = await admin.UpdateProductAsync(token, id, form, ct);
        if (!success)
        {
            model.ErrorMessage = error ?? "Failed to update product.";
            var cats = await admin.GetCategoriesAsync(token, 1, 200, ct);
            model.Categories = cats?.Items ?? [];
            return View(FormView, model);
        }

        TempData["SuccessMessage"] = "Product updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    // ── Deactivate ────────────────────────────────────────────────────────────

    [HttpPost("admin/products/{id:int}/deactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct = default)
    {
        var token = Token;
        if (token is null) return RedirectToAction("Login", "Account");

        var (success, error) = await admin.DeactivateProductAsync(token, id, ct);
        TempData[success ? "SuccessMessage" : "ErrorMessage"] =
            success ? "Product deactivated." : (error ?? "Could not deactivate product.");
        return RedirectToAction(nameof(Index));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static MultipartFormDataContent BuildProductForm(AdminProductFormViewModel model, IFormFile? image)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(model.Name), "Name" },
            { new StringContent(model.SKU), "SKU" },
            { new StringContent(model.Description ?? ""), "Description" },
            { new StringContent(model.CategoryId.ToString()), "CategoryId" },
            { new StringContent(model.UnitCode), "UnitCode" },
            { new StringContent(model.UnitLabel), "UnitLabel" },
            { new StringContent(model.Price.ToString()), "Price" },
            { new StringContent(model.OfferPrice?.ToString() ?? ""), "OfferPrice" },
            { new StringContent(model.IsFeatured.ToString()), "IsFeatured" },
            { new StringContent(model.IsActive.ToString()), "IsActive" },
            { new StringContent(model.InitialStock.ToString()), "InitialStock" },
            { new StringContent(model.LowStockThreshold.ToString()), "LowStockThreshold" }
        };
        if (image is { Length: > 0 })
        {
            var stream = image.OpenReadStream();
            form.Add(new StreamContent(stream), "Image", image.FileName);
        }
        return form;
    }
}
