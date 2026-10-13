using GOLISMart.Web.Models;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers;

public class ProductController : Controller
{
    private readonly ICatalogApiClient _api;
    private readonly ILogger<ProductController> _logger;

    public ProductController(ICatalogApiClient api, ILogger<ProductController> logger)
    {
        _api    = api;
        _logger = logger;
    }

    /// <summary>
    /// GET /product/{slug} — Full product detail page
    /// </summary>
    [Route("product/{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken ct)
    {
        try
        {
            var (product, related) = await (
                _api.GetProductBySlugAsync(slug, ct),
                _api.GetRelatedProductsAsync(slug, 6, ct)
            ).WhenAll();

            if (product is null)
            {
                return View(new ProductDetailViewModel { NotFound = true });
            }

            var vm = new ProductDetailViewModel
            {
                Product        = product,
                RelatedProducts= related,
                ApiAvailable   = true
            };
            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load product '{Slug}' from API", slug);
            return View(new ProductDetailViewModel { ApiAvailable = false });
        }
    }
}

file static class ProductTaskExtensions
{
    public static async Task<(T1, T2)> WhenAll<T1, T2>(
        this (Task<T1> t1, Task<T2> t2) tasks)
    {
        await Task.WhenAll(tasks.t1, tasks.t2);
        return (tasks.t1.Result, tasks.t2.Result);
    }
}
