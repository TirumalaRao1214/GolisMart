using System.Security.Claims;
using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/inventory")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class AdminInventoryController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminInventoryController(IAdminService admin)
    {
        _admin = admin;
    }

    /// <summary>
    /// Returns a paged inventory list with optional search and low-stock filter.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventory(
        [FromQuery] string? search,
        [FromQuery] bool? lowStock,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var (items, total) = await _admin.GetInventoryAsync(search, lowStock, page, pageSize, ct);
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        return Ok(new PagedResult<InventoryItemDto>(items, page, pageSize, total, totalPages));
    }

    /// <summary>
    /// Applies a manual inventory adjustment (positive or negative quantity change).
    /// </summary>
    [HttpPost("adjust")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AdjustInventory(
        [FromBody] InventoryAdjustRequest request,
        CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var (success, error) = await _admin.AdjustInventoryAsync(request, adminId, ct);

        if (!success)
            return UnprocessableEntity(new { message = error });

        return NoContent();
    }
}
