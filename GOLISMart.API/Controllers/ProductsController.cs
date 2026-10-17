using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.API.Controllers;

/// <summary>
/// Provides product catalogue endpoints.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly ICatalogService _catalog;
    private readonly ILogger<ProductsController> _logger;

    private const int MaxSearchLength = 100;

    private static readonly HashSet<string> ValidSortFields =
        new(StringComparer.OrdinalIgnoreCase) { "name", "price", "offerprice", "createdat" };

    public ProductsController(ICatalogService catalog, ILogger<ProductsController> logger)
    {
        _catalog = catalog;
        _logger  = logger;
    }

    /// <summary>
    /// Returns a paginated list of active products with optional filtering, searching and sorting.
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (1–100, default: 20)</param>
    /// <param name="search">Keyword to search in name, description and SKU</param>
    /// <param name="category">Category slug filter</param>
    /// <param name="sortBy">Sort field: name | price | offerprice | createdat</param>
    /// <param name="sortOrder">Sort direction: asc | desc</param>
    /// <param name="inStock">Filter to in-stock products only</param>
    /// <example>GET /api/v1/products?page=1&amp;pageSize=20&amp;category=fresh-vegetables&amp;sortBy=price&amp;sortOrder=asc</example>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? sortBy = "name",
        [FromQuery] string? sortOrder = "asc",
        [FromQuery] bool? inStock = null,
        CancellationToken ct = default)
    {
        if (search is { Length: > MaxSearchLength })
            return BadRequest(new ErrorResponse($"search must not exceed {MaxSearchLength} characters."));
        if (page < 1)
            return BadRequest(new ErrorResponse("page must be >= 1."));
        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new ErrorResponse("pageSize must be between 1 and 100."));
        if (sortBy is not null && !ValidSortFields.Contains(sortBy))
            return BadRequest(new ErrorResponse($"sortBy must be one of: {string.Join(", ", ValidSortFields)}."));
        if (sortOrder is not null && !string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase)
                                  && !string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ErrorResponse("sortOrder must be 'asc' or 'desc'."));

        var query = new ProductQueryParams
        {
            Page      = page,
            PageSize  = pageSize,
            Search    = search,
            Category  = category,
            SortBy    = sortBy ?? "name",
            SortOrder = sortOrder ?? "asc",
            InStock   = inStock
        };

        var result = await _catalog.GetProductsAsync(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns a list of active featured products.
    /// </summary>
    /// <example>GET /api/v1/products/featured</example>
    [HttpGet("featured")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeatured([FromQuery] int max = 8, CancellationToken ct = default)
    {
        var count = Math.Clamp(max, 1, 20);
        var products = await _catalog.GetFeaturedProductsAsync(count, ct);
        return Ok(products);
    }

    /// <summary>
    /// Returns full product details for the given URL slug.
    /// </summary>
    /// <example>GET /api/v1/products/fresh-tomatoes</example>
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(ProductDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return BadRequest(new ErrorResponse("Slug must not be empty."));

        var product = await _catalog.GetProductBySlugAsync(slug, ct);
        if (product is null)
        {
            _logger.LogInformation("Product not found: {Slug}", slug);
            return NotFound(new ErrorResponse($"Product '{slug}' was not found."));
        }

        return Ok(product);
    }

    /// <summary>
    /// Returns products related to the given product slug (same category).
    /// </summary>
    /// <example>GET /api/v1/products/fresh-tomatoes/related</example>
    [HttpGet("{slug}/related")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRelated(string slug, [FromQuery] int max = 6, CancellationToken ct = default)
    {
        var count    = Math.Clamp(max, 1, 20);
        var related  = await _catalog.GetRelatedProductsAsync(slug, count, ct);
        return Ok(related);
    }
}
