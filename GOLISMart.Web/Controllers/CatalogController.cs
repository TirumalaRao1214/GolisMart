using GOLISMart.Web.Models;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers;

public class CatalogController : Controller
{
    private readonly ICatalogApiClient _api;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(ICatalogApiClient api, ILogger<CatalogController> logger)
    {
        _api    = api;
        _logger = logger;
    }

    private bool IsAjaxRequest() =>
        HttpContext != null && (
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ||
            Request.Headers.Accept.Any(a => a?.Contains("application/json") == true) ||
            Request.Headers["X-SPA-Request"] == "true"
        );

    private bool IsPartialOnly() =>
        HttpContext != null && (
            Request.Headers["X-Partial-Only"] == "true" ||
            Request.Query["partial"] == "true"
        );

    /// <summary>
    /// GET /catalog — Product listing with search, filter, sort and pagination
    /// </summary>
    public async Task<IActionResult> Index(
        string? search,
        string? category,
        string? sortBy = "name",
        string? sortOrder = "asc",
        int page = 1,
        bool? inStock = null,
        CancellationToken ct = default)
    {
        // Sanitise pagination
        page = Math.Max(1, page);

        try
        {
            var (categories, products) = await (
                _api.GetCategoriesAsync(ct),
                _api.GetProductsAsync(
                    page: page, pageSize: 20,
                    search: search, category: category,
                    sortBy: sortBy ?? "name", sortOrder: sortOrder ?? "asc",
                    inStock: inStock, ct: ct)
            ).WhenAll();

            var vm = new ProductListViewModel
            {
                Products         = products,
                Categories       = categories,
                SelectedCategory = category,
                SearchQuery      = search,
                SortBy           = sortBy ?? "name",
                SortOrder        = sortOrder ?? "asc",
                InStock          = inStock,
                ApiAvailable     = true
            };

            if (IsPartialOnly())
                return PartialView("_ProductGrid", vm);

            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load catalog from API");
            var errorVm = new ProductListViewModel { ApiAvailable = false };
            if (IsPartialOnly())
                return PartialView("_ProductGrid", errorVm);
            return View(errorVm);
        }
    }

    /// <summary>
    /// GET /catalog/category/{slug} — Products filtered to a specific category
    /// </summary>
    [Route("catalog/category/{slug}")]
    public async Task<IActionResult> Category(
        string slug,
        string? sortBy = "name",
        string? sortOrder = "asc",
        int page = 1,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);

        try
        {
            var (categories, catDetails, products) = await (
                _api.GetCategoriesAsync(ct),
                _api.GetCategoryBySlugAsync(slug, ct),
                _api.GetProductsAsync(
                    page: page, pageSize: 20,
                    category: slug,
                    sortBy: sortBy ?? "name", sortOrder: sortOrder ?? "asc",
                    ct: ct)
            ).WhenAll3();

            if (catDetails is null)
                return RedirectToAction(nameof(Index));

            ViewData["CategoryName"] = catDetails.Name;

            var vm = new ProductListViewModel
            {
                Products         = products,
                Categories       = categories,
                SelectedCategory = slug,
                SortBy           = sortBy ?? "name",
                SortOrder        = sortOrder ?? "asc",
                ApiAvailable     = true
            };

            if (IsPartialOnly())
                return PartialView("_ProductGrid", vm);

            return View("Index", vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load category '{Slug}' from API", slug);
            var errorVm = new ProductListViewModel { ApiAvailable = false };
            if (IsPartialOnly())
                return PartialView("_ProductGrid", errorVm);
            return View("Index", errorVm);
        }
    }

    /// <summary>
    /// GET /catalog/suggestions — Live search suggestions for header search dropdown
    /// </summary>
    [HttpGet("catalog/suggestions")]
    public async Task<IActionResult> Suggestions(string? q, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Ok(new { items = Array.Empty<object>() });

        try
        {
            var results = await _api.GetProductsAsync(page: 1, pageSize: 6, search: q.Trim(), ct: ct);
            var items = results?.Items.Select(p => new
            {
                id        = p.Id,
                name      = p.Name,
                slug      = p.Slug,
                price     = (p.OfferPrice ?? p.Price).ToString("N0"),
                unitLabel = p.UnitLabel,
                imagePath = p.ImagePath,
                inStock   = p.IsInStock
            }) ?? Enumerable.Empty<object>();

            return Ok(new { items });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get suggestions for '{Q}'", q);
            return Ok(new { items = Array.Empty<object>() });
        }
    }

    /// <summary>
    /// GET /catalog/search — Full-text search results page
    /// </summary>
    [Route("catalog/search")]
    public async Task<IActionResult> Search(
        string? q,
        int page = 1,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);

        try
        {
            var results = string.IsNullOrWhiteSpace(q)
                ? null
                : await _api.GetProductsAsync(page: page, pageSize: 20, search: q, ct: ct);

            var vm = new SearchViewModel
            {
                Results      = results,
                Query        = q,
                Page         = page,
                ApiAvailable = true
            };
            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for query '{Q}'", q);
            return View(new SearchViewModel { Query = q, ApiAvailable = false });
        }
    }
}

file static class CatalogTaskExtensions
{
    public static async Task<(T1, T2)> WhenAll<T1, T2>(
        this (Task<T1> t1, Task<T2> t2) tasks)
    {
        await Task.WhenAll(tasks.t1, tasks.t2);
        return (tasks.t1.Result, tasks.t2.Result);
    }

    public static async Task<(T1, T2, T3)> WhenAll3<T1, T2, T3>(
        this (Task<T1> t1, Task<T2> t2, Task<T3> t3) tasks)
    {
        await Task.WhenAll(tasks.t1, tasks.t2, tasks.t3);
        return (tasks.t1.Result, tasks.t2.Result, tasks.t3.Result);
    }
}
