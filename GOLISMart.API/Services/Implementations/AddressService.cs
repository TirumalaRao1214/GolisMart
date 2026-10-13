using GOLISMart.API.DTOs;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Interfaces;

namespace GOLISMart.API.Services.Implementations;

public class AddressService : IAddressService
{
    private readonly IAddressRepository _addresses;
    private readonly ILogger<AddressService> _logger;

    public AddressService(IAddressRepository addresses, ILogger<AddressService> logger)
    {
        _addresses = addresses;
        _logger    = logger;
    }

    public async Task<IReadOnlyList<AddressDto>> GetAddressesAsync(string userId, CancellationToken ct = default)
    {
        var addresses = await _addresses.GetByUserIdAsync(userId, ct);
        return addresses.Select(ToDto).ToList();
    }

    public async Task<AddressDto?> GetAddressAsync(int addressId, string userId, CancellationToken ct = default)
    {
        var address = await _addresses.GetByIdAndUserIdAsync(addressId, userId, ct);
        return address is null ? null : ToDto(address);
    }

    public async Task<(bool Success, string? Error, AddressDto? Address)> CreateAddressAsync(
        string userId, CreateAddressRequest request, CancellationToken ct = default)
    {
        var address = new UserAddress
        {
            UserId               = userId,
            RecipientName        = request.RecipientName.Trim(),
            RecipientMobile      = AuthService.NormalizeMobile(request.RecipientMobile),
            AddressLabel         = request.AddressLabel,
            HouseNumber          = request.HouseNumber.Trim(),
            BuildingStreet       = request.BuildingStreet.Trim(),
            Locality             = request.Locality.Trim(),
            Landmark             = request.Landmark?.Trim(),
            City                 = request.City.Trim(),
            State                = request.State.Trim(),
            Pincode              = request.Pincode.Trim(),
            DeliveryInstructions = request.DeliveryInstructions?.Trim(),
            IsDefault            = request.IsDefault
        };

        var newId = await _addresses.CreateAsync(address, ct);
        address.Id = newId;
        _logger.LogInformation("Address {AddressId} created for user {UserId}", newId, userId);
        return (true, null, ToDto(address));
    }

    public async Task<(bool Success, string? Error, AddressDto? Address)> UpdateAddressAsync(
        int addressId, string userId, UpdateAddressRequest request, CancellationToken ct = default)
    {
        var existing = await _addresses.GetByIdAndUserIdAsync(addressId, userId, ct);
        if (existing is null)
            return (false, "Address not found.", null);

        existing.RecipientName        = request.RecipientName.Trim();
        existing.RecipientMobile      = AuthService.NormalizeMobile(request.RecipientMobile);
        existing.AddressLabel         = request.AddressLabel;
        existing.HouseNumber          = request.HouseNumber.Trim();
        existing.BuildingStreet       = request.BuildingStreet.Trim();
        existing.Locality             = request.Locality.Trim();
        existing.Landmark             = request.Landmark?.Trim();
        existing.City                 = request.City.Trim();
        existing.State                = request.State.Trim();
        existing.Pincode              = request.Pincode.Trim();
        existing.DeliveryInstructions = request.DeliveryInstructions?.Trim();
        existing.IsDefault            = request.IsDefault;

        await _addresses.UpdateAsync(existing, ct);
        _logger.LogInformation("Address {AddressId} updated for user {UserId}", addressId, userId);
        return (true, null, ToDto(existing));
    }

    public async Task<(bool Success, string? Error)> DeleteAddressAsync(
        int addressId, string userId, CancellationToken ct = default)
    {
        var existing = await _addresses.GetByIdAndUserIdAsync(addressId, userId, ct);
        if (existing is null)
            return (false, "Address not found.");

        await _addresses.SoftDeleteAsync(addressId, userId, ct);
        _logger.LogInformation("Address {AddressId} soft-deleted for user {UserId}", addressId, userId);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> SetDefaultAsync(
        int addressId, string userId, CancellationToken ct = default)
    {
        var existing = await _addresses.GetByIdAndUserIdAsync(addressId, userId, ct);
        if (existing is null)
            return (false, "Address not found.");

        await _addresses.SetDefaultAsync(addressId, userId, ct);
        _logger.LogInformation("Address {AddressId} set as default for user {UserId}", addressId, userId);
        return (true, null);
    }

    private static AddressDto ToDto(UserAddress a) => new(
        a.Id, a.RecipientName, a.RecipientMobile, a.AddressLabel,
        a.HouseNumber, a.BuildingStreet, a.Locality, a.Landmark,
        a.City, a.State, a.Pincode, a.DeliveryInstructions,
        a.IsDefault, a.CreatedAt, a.UpdatedAt);
}
