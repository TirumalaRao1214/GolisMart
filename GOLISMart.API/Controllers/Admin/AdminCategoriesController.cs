using System.Security.Claims;
using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/categories")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class AdminCategoriesController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminCategoriesController(IAdminService admin)
    {
        _admin = admin;
    }

    /// <summary>
    /// Returns a paged list of categories with product counts.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var (categories, total) = await _admin.GetCategoriesAsync(page, pageSize, ct);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        return Ok(new PagedResult<AdminCategoryDto>(categories, page, pageSize, total, totalPages));
    }

    /// <summary>
    /// Creates a new category.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateCategoryRequest request,
        CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var (success, error, categoryId) = await _admin.CreateCategoryAsync(request, adminId, ct);

        if (!success)
            return UnprocessableEntity(new { message = error });

        return Created($"api/v1/admin/categories/{categoryId}", new { id = categoryId });
    }

    /// <summary>
    /// Updates an existing category's name, description, and sort order.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateCategory(
        int id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var (success, error) = await _admin.UpdateCategoryAsync(id, request, adminId, ct);

        if (!success)
            return error == "Category not found." ? NotFound() : UnprocessableEntity(new { message = error });

        return NoContent();
    }

    /// <summary>
    /// Toggles the IsActive flag of a category.
    /// </summary>
    [HttpPatch("{id:int}/toggle-active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken ct)
    {
        // Fetch current state then invert it
        var (categories, _) = await _admin.GetCategoriesAsync(1, int.MaxValue, ct);
        var category = categories.FirstOrDefault(c => c.Id == id);
        if (category is null) return NotFound();

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var request = new UpdateCategoryRequest(category.Name, category.Description, category.SortOrder, !category.IsActive);
        var (success, error) = await _admin.UpdateCategoryAsync(id, request, adminId, ct);

        if (!success)
            return error == "Category not found." ? NotFound() : UnprocessableEntity(new { message = error });

        return NoContent();
    }
}
