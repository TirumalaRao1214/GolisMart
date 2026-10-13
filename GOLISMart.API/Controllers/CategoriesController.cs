using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.API.Controllers;

/// <summary>
/// Provides category catalogue endpoints.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly ICatalogService _catalog;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(ICatalogService catalog, ILogger<CategoriesController> logger)
    {
        _catalog = catalog;
        _logger  = logger;
    }

    /// <summary>
    /// Returns all active categories with product counts.
    /// </summary>
    /// <example>GET /api/v1/categories</example>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var categories = await _catalog.GetActiveCategoriesAsync(ct);
        return Ok(categories);
    }

    /// <summary>
    /// Returns a single category by its URL slug.
    /// </summary>
    /// <example>GET /api/v1/categories/fresh-vegetables</example>
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return BadRequest(new ErrorResponse("Slug must not be empty."));

        var category = await _catalog.GetCategoryBySlugAsync(slug, ct);
        if (category is null)
        {
            _logger.LogInformation("Category not found: {Slug}", slug);
            return NotFound(new ErrorResponse($"Category '{slug}' was not found."));
        }

        return Ok(category);
    }
}
