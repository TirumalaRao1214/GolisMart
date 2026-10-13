using GOLISMart.API.DTOs;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using GOLISMart.API.Models;

namespace GOLISMart.API.Services.Implementations;

public class CustomerService : ICustomerService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher<ApplicationUser> _hasher;
    private readonly ILogger<CustomerService> _logger;

    public CustomerService(
        IUserRepository users,
        IPasswordHasher<ApplicationUser> hasher,
        ILogger<CustomerService> logger)
    {
        _users  = users;
        _hasher = hasher;
        _logger = logger;
    }

    public async Task<CustomerProfileDto?> GetProfileAsync(string userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null) return null;
        return new CustomerProfileDto(
            user.Id, user.Email!, user.DisplayName!, user.PhoneNumber,
            user.CreatedAt, user.UpdatedAt);
    }

    public async Task<(bool Success, string? Error)> UpdateProfileAsync(
        string userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null) return (false, "User not found.");

        user.DisplayName     = request.DisplayName.Trim();
        user.PhoneNumber     = AuthService.NormalizeMobile(request.Mobile);
        user.ConcurrencyStamp = Guid.NewGuid().ToString();

        await _users.UpdateAsync(user, ct);
        _logger.LogInformation("Profile updated for {UserId}", userId);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(
        string userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null) return (false, "User not found.");

        var verifyResult = _hasher.VerifyHashedPassword(user, user.PasswordHash!, request.CurrentPassword);
        if (verifyResult == PasswordVerificationResult.Failed)
            return (false, "Current password is incorrect.");

        var newHash      = _hasher.HashPassword(user, request.NewPassword);
        var newStamp     = Guid.NewGuid().ToString();
        await _users.UpdatePasswordHashAsync(userId, newHash, newStamp, ct);
        _logger.LogInformation("Password changed for {UserId}", userId);
        return (true, null);
    }
}
