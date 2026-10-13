using System.Diagnostics;
using GOLISMart.Web.Models;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers;

public class HomeController : Controller
{
    private readonly ICatalogApiClient _api;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ICatalogApiClient api, ILogger<HomeController> logger)
    {
        _api    = api;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        try
        {
            var (categories, featured, vegetables, fruits, essentials) = await (
                _api.GetCategoriesAsync(ct),
                _api.GetFeaturedProductsAsync(8, ct),
                _api.GetProductsByCategorySlugAsync("fresh-vegetables", 8, ct),
                _api.GetProductsByCategorySlugAsync("fresh-fruits", 8, ct),
                _api.GetProductsByCategorySlugAsync("dairy-and-eggs", 6, ct)
            ).WhenAll();

            var vm = new HomeViewModel
            {
                Categories      = categories,
                FeaturedProducts= featured,
                FreshVegetables = vegetables,
                FreshFruits     = fruits,
                DailyEssentials = essentials,
                ApiAvailable    = true
            };
            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load homepage data from API");
            return View(new HomeViewModel { ApiAvailable = false });
        }
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

// Tuple helper for parallel await
file static class TaskExtensions
{
    public static async Task<(T1, T2, T3, T4, T5)> WhenAll<T1, T2, T3, T4, T5>(
        this (Task<T1> t1, Task<T2> t2, Task<T3> t3, Task<T4> t4, Task<T5> t5) tasks)
    {
        await Task.WhenAll(tasks.t1, tasks.t2, tasks.t3, tasks.t4, tasks.t5);
        return (tasks.t1.Result, tasks.t2.Result, tasks.t3.Result, tasks.t4.Result, tasks.t5.Result);
    }
}
