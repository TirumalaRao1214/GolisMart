using System.Security.Claims;
using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/settings")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class AdminSettingsController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminSettingsController(IAdminService admin)
    {
        _admin = admin;
    }

    /// <summary>
    /// Returns all store settings grouped by category.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StoreSettingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var settings = await _admin.GetSettingsAsync(ct);
        return Ok(settings);
    }

    /// <summary>
    /// Updates one or more store settings by key.
    /// </summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateSettingsRequest request,
        CancellationToken ct)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var (success, error) = await _admin.UpdateSettingsAsync(request, adminId, ct);

        if (!success)
            return BadRequest(new { message = error });

        return NoContent();
    }
}
