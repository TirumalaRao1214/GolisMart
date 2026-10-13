using GOLISMart.API.DTOs;

namespace GOLISMart.API.Services.Interfaces;

public interface ICustomerService
{
    Task<CustomerProfileDto?> GetProfileAsync(string userId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error)> ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct = default);
}
