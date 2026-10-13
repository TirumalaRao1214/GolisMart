using System.Security.Claims;
using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/products")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class AdminProductsController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminProductsController(IAdminService admin)
    {
        _admin = admin;
    }

    /// <summary>
    /// Returns a paged list of products with optional search and filter.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (products, total) = await _admin.GetProductsAsync(search, categoryId, isActive, page, pageSize, ct);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        return Ok(new PagedResult<AdminProductDto>(products, page, pageSize, total, totalPages));
    }

    /// <summary>
    /// Returns a single product by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AdminProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct(int id, CancellationToken ct)
    {
        var product = await _admin.GetProductByIdAsync(id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>
    /// Creates a new product. Accepts multipart/form-data for optional image upload.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateProduct(
        [FromForm] CreateProductRequest request,
        IFormFile? image,
        CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var (success, error, productId) = await _admin.CreateProductAsync(request, image, adminId, ct);

        if (!success)
            return UnprocessableEntity(new { message = error });

        return CreatedAtAction(nameof(GetProduct), new { id = productId }, new { id = productId });
    }

    /// <summary>
    /// Updates an existing product. Accepts multipart/form-data for optional image upload.
    /// </summary>
    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProduct(
        int id,
        [FromForm] UpdateProductRequest request,
        IFormFile? image,
        CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var (success, error) = await _admin.UpdateProductAsync(id, request, image, adminId, ct);

        if (!success)
            return error == "Product not found." ? NotFound() : UnprocessableEntity(new { message = error });

        return NoContent();
    }

    /// <summary>
    /// Soft-deactivates a product (sets IsActive = false).
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateProduct(int id, CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var (success, error) = await _admin.DeactivateProductAsync(id, adminId, ct);

        if (!success)
            return error == "Product not found." ? NotFound() : UnprocessableEntity(new { message = error });

        return NoContent();
    }
}
