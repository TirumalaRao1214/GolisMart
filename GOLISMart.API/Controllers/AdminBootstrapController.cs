using Dapper;
using GOLISMart.API.Data;
using GOLISMart.API.DTOs;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;

namespace GOLISMart.API.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Produces("application/json")]
public class AdminBootstrapController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IDbConnectionFactory _db;
    private readonly IPasswordHasher<ApplicationUser> _hasher;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminBootstrapController> _logger;

    public AdminBootstrapController(
        IUserRepository users,
        IDbConnectionFactory db,
        IPasswordHasher<ApplicationUser> hasher,
        IConfiguration config,
        ILogger<AdminBootstrapController> logger)
    {
        _users  = users;
        _db     = db;
        _hasher = hasher;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Creates the first admin account. Only works when AdminBootstrap:Enabled = true in config.
    /// Protected by a shared secret to prevent abuse.
    /// </summary>
    [HttpPost("bootstrap")]
    [AllowAnonymous]
    public async Task<IActionResult> Bootstrap(
        [FromBody] AdminBootstrapRequest request,
        CancellationToken ct)
    {
        // 1. Check feature flag
        if (!_config.GetValue<bool>("AdminBootstrap:Enabled"))
            return NotFound();

        // 2. Check bootstrap secret
        var expectedSecret = _config["AdminBootstrap:Secret"];
        if (string.IsNullOrEmpty(expectedSecret) || request.BootstrapSecret != expectedSecret)
        {
            _logger.LogWarning("Bootstrap attempted with invalid secret from {IP}",
                HttpContext.Connection.RemoteIpAddress);
            return Forbid();
        }

        // 3. Check if email already exists
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        if (await _users.ExistsByNormalizedEmailAsync(normalizedEmail, ct))
            return Conflict(new { message = "An account with this email address already exists." });

        // 4. Create user
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id                 = userId,
            UserName           = normalizedEmail,
            NormalizedUserName = normalizedEmail,
            Email              = request.Email.Trim(),
            NormalizedEmail    = normalizedEmail,
            EmailConfirmed     = true,
            DisplayName        = request.DisplayName.Trim(),
            LockoutEnabled     = true,
            SecurityStamp      = Guid.NewGuid().ToString(),
            ConcurrencyStamp   = Guid.NewGuid().ToString(),
            IsActive           = true
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        await _users.CreateAsync(user, ct);

        // 5. Get admin role id
        const string roleSql = "SELECT Id FROM AspNetRoles WHERE NormalizedName = 'ADMIN';";
        using var conn = _db.CreateConnection();
        var roleId = await conn.QuerySingleOrDefaultAsync<string>(roleSql);

        if (roleId is null)
        {
            _logger.LogError("Admin role not found in AspNetRoles during bootstrap");
            return StatusCode(500, new { message = "Admin role not found. Ensure database migrations have run." });
        }

        // 6. Assign role
        await _users.AssignRoleAsync(userId, roleId, ct);

        _logger.LogInformation("Admin account created via bootstrap for {Email}", request.Email);
        return Ok(new { message = "Admin account created." });
    }
}
