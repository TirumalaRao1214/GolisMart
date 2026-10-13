namespace GOLISMart.Web.Services.Interfaces;

public interface IAuthApiClient
{
    Task<(bool Success, string? Error, AuthResponse? Response)> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error, AuthResponse? Response)> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<MeResponse?> GetMeAsync(string token, CancellationToken ct = default);
    Task<CustomerProfileDto?> GetProfileAsync(string token, CancellationToken ct = default);
    Task<(bool Success, string? Error, CustomerProfileDto? Profile)> UpdateProfileAsync(string token, UpdateProfileRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error)> ChangePasswordAsync(string token, ChangePasswordRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AddressDto>> GetAddressesAsync(string token, CancellationToken ct = default);
    Task<(bool Success, string? Error, AddressDto? Address)> CreateAddressAsync(string token, CreateAddressRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error, AddressDto? Address)> UpdateAddressAsync(string token, int addressId, UpdateAddressRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error)> DeleteAddressAsync(string token, int addressId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> SetDefaultAddressAsync(string token, int addressId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetUserRolesAsync(string token, CancellationToken ct = default);
}
