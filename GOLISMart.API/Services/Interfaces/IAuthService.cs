using GOLISMart.API.DTOs;

namespace GOLISMart.API.Services.Interfaces;

public interface IAuthService
{
    Task<(bool Success, string? Error, AuthResponse? Response)> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error, AuthResponse? Response)> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<MeResponse?> GetMeAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetUserRolesAsync(string userId, CancellationToken ct = default);
}
