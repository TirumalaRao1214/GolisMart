using GOLISMart.API.DTOs;

namespace GOLISMart.API.Services.Interfaces;

public interface IAddressService
{
    Task<IReadOnlyList<AddressDto>> GetAddressesAsync(string userId, CancellationToken ct = default);
    Task<AddressDto?> GetAddressAsync(int addressId, string userId, CancellationToken ct = default);
    Task<(bool Success, string? Error, AddressDto? Address)> CreateAddressAsync(string userId, CreateAddressRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error, AddressDto? Address)> UpdateAddressAsync(int addressId, string userId, UpdateAddressRequest request, CancellationToken ct = default);
    Task<(bool Success, string? Error)> DeleteAddressAsync(int addressId, string userId, CancellationToken ct = default);
    Task<(bool Success, string? Error)> SetDefaultAsync(int addressId, string userId, CancellationToken ct = default);
}
