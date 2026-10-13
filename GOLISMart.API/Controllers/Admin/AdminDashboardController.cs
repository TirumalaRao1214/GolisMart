using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.API.Controllers.Admin;

[ApiController]
[Route("api/v1/admin/dashboard")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
public class AdminDashboardController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminDashboardController(IAdminService admin)
    {
        _admin = admin;
    }

    /// <summary>
    /// Returns a summary of orders, sales, low-stock items and recent activity.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var summary = await _admin.GetDashboardAsync(ct);
        return Ok(summary);
    }
}
