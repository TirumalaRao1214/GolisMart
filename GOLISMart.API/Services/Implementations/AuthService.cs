using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using GOLISMart.API.DTOs;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace GOLISMart.API.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher<ApplicationUser> _hasher;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    private const int LockoutThreshold = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public AuthService(
        IUserRepository users,
        IPasswordHasher<ApplicationUser> hasher,
        IConfiguration config,
        ILogger<AuthService> logger)
    {
        _users  = users;
        _hasher = hasher;
        _config = config;
        _logger = logger;
    }

    public async Task<(bool Success, string? Error, AuthResponse? Response)> RegisterAsync(
        RegisterRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        if (await _users.ExistsByNormalizedEmailAsync(normalizedEmail, ct))
            return (false, "An account with this email address already exists.", null);

        var normalizedMobile = NormalizeMobile(request.Mobile);
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id                  = userId,
            UserName            = normalizedEmail,
            NormalizedUserName  = normalizedEmail,
            Email               = request.Email.Trim(),
            NormalizedEmail     = normalizedEmail,
            EmailConfirmed      = false,
            PhoneNumber         = normalizedMobile,
            DisplayName         = request.FullName.Trim(),
            LockoutEnabled      = true,
            SecurityStamp       = Guid.NewGuid().ToString(),
            ConcurrencyStamp    = Guid.NewGuid().ToString(),
            IsActive            = true
        };

        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        try
        {
            await _users.CreateAsync(user, ct);
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 2601 or 2627)
        {
            // Unique constraint violation — concurrent registration
            _logger.LogWarning("Concurrent registration attempt for email {Email}", normalizedEmail);
            return (false, "An account with this email address already exists.", null);
        }

        var token   = await GenerateJwtAsync(user, ct);
        var expires = DateTime.UtcNow.AddHours(GetTokenExpiryHours());
        _logger.LogInformation("New customer registered: {UserId}", userId);

        return (true, null, new AuthResponse(token, user.Id, user.Email!, user.DisplayName!, expires));
    }

    public async Task<(bool Success, string? Error, AuthResponse? Response)> LoginAsync(
        LoginRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);
        var user = await _users.GetByNormalizedEmailAsync(normalizedEmail, ct);

        if (user is null)
        {
            // Avoid timing attacks — still hash even when user not found
            _ = _hasher.HashPassword(new ApplicationUser(), request.Password);
            return (false, "Invalid email or password.", null);
        }

        // Check lockout
        if (user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            var remaining = (int)Math.Ceiling((user.LockoutEnd.Value - DateTimeOffset.UtcNow).TotalMinutes);
            return (false, $"Account is temporarily locked. Please try again in {remaining} minute(s).", null);
        }

        var verifyResult = _hasher.VerifyHashedPassword(user, user.PasswordHash!, request.Password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            await _users.IncrementAccessFailedCountAsync(user.Id, ct);
            var newCount = user.AccessFailedCount + 1;
            if (newCount >= LockoutThreshold)
            {
                await _users.SetLockoutEndAsync(user.Id, DateTimeOffset.UtcNow.Add(LockoutDuration), ct);
                _logger.LogWarning("Account locked after {Count} failed attempts: {UserId}", newCount, user.Id);
                return (false, $"Too many failed attempts. Account locked for {(int)LockoutDuration.TotalMinutes} minutes.", null);
            }
            return (false, "Invalid email or password.", null);
        }

        // Success — reset lockout counter
        await _users.ResetAccessFailedCountAsync(user.Id, ct);
        if (user.LockoutEnd.HasValue)
            await _users.SetLockoutEndAsync(user.Id, null, ct);

        var token   = await GenerateJwtAsync(user, ct);
        var expires = DateTime.UtcNow.AddHours(GetTokenExpiryHours());
        _logger.LogInformation("Customer logged in: {UserId}", user.Id);

        return (true, null, new AuthResponse(token, user.Id, user.Email!, user.DisplayName!, expires));
    }

    public async Task<MeResponse?> GetMeAsync(string userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null) return null;
        return new MeResponse(user.Id, user.Email!, user.DisplayName!, user.PhoneNumber, user.CreatedAt);
    }

    public async Task<IReadOnlyList<string>> GetUserRolesAsync(string userId, CancellationToken ct = default)
        => await _users.GetRolesAsync(userId, ct);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> GenerateJwtAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var key    = Encoding.UTF8.GetBytes(GetJwtKey());
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Name,  user.DisplayName ?? string.Empty),
            new(ClaimTypes.NameIdentifier,     user.Id),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString())
        };

        var roles = await _users.GetRolesAsync(user.Id, ct);
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject            = new ClaimsIdentity(claims),
            Expires            = DateTime.UtcNow.AddHours(GetTokenExpiryHours()),
            Issuer             = _config["Jwt:Issuer"],
            Audience           = _config["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private string GetJwtKey()
    {
        var key = _config["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");
        if (key.Length < 32)
            throw new InvalidOperationException("Jwt:SecretKey must be at least 32 characters.");
        return key;
    }

    private int GetTokenExpiryHours() =>
        _config.GetValue<int>("Jwt:ExpiryHours", 8);

    private static string NormalizeEmail(string email) =>
        email.Trim().ToUpperInvariant();

    public static string NormalizeMobile(string mobile)
    {
        var digits = Regex.Replace(mobile, @"[\s\-]", "");
        if (digits.StartsWith("+91") && digits.Length == 13)
            digits = digits[3..];
        if (digits.StartsWith("0") && digits.Length == 11)
            digits = digits[1..];
        return $"+91{digits}";
    }
}
