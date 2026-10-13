using FluentAssertions;
using GOLISMart.API.DTOs;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GOLISMart.API.Tests;

/// <summary>
/// Unit tests for AddressService — no database required.
/// </summary>
public class AddressServiceTests
{
    private readonly Mock<IAddressRepository> _addressRepo = new();

    private AddressService CreateService() =>
        new(_addressRepo.Object, NullLogger<AddressService>.Instance);

    // ── GetAddresses ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAddresses_ReturnsEmptyList_WhenNoAddresses()
    {
        _addressRepo.Setup(r => r.GetByUserIdAsync("u1", default))
                    .ReturnsAsync(new List<UserAddress>());

        var svc = CreateService();
        var result = await svc.GetAddressesAsync("u1");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAddresses_ReturnsMappedDtos()
    {
        var addresses = new List<UserAddress>
        {
            BuildAddress(1, "u1", isDefault: true),
            BuildAddress(2, "u1", isDefault: false)
        };
        _addressRepo.Setup(r => r.GetByUserIdAsync("u1", default)).ReturnsAsync(addresses);

        var svc = CreateService();
        var result = await svc.GetAddressesAsync("u1");

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(1);
        result[0].IsDefault.Should().BeTrue();
    }

    // ── CreateAddress ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAddress_Succeeds_WithValidRequest()
    {
        _addressRepo.Setup(r => r.CreateAsync(It.IsAny<UserAddress>(), default))
                    .ReturnsAsync(42);

        var svc = CreateService();
        var request = BuildCreateRequest();
        var (success, error, address) = await svc.CreateAddressAsync("u1", request);

        success.Should().BeTrue();
        error.Should().BeNull();
        address.Should().NotBeNull();
        address!.Id.Should().Be(42);
    }

    // ── UpdateAddress ─────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAddress_Succeeds_WhenAddressExists()
    {
        var existing = BuildAddress(5, "u1");
        _addressRepo.Setup(r => r.GetByIdAndUserIdAsync(5, "u1", default)).ReturnsAsync(existing);
        _addressRepo.Setup(r => r.UpdateAsync(It.IsAny<UserAddress>(), default))
                    .Returns(Task.CompletedTask);

        var svc = CreateService();
        var request = BuildUpdateRequest();
        var (success, error, address) = await svc.UpdateAddressAsync(5, "u1", request);

        success.Should().BeTrue();
        error.Should().BeNull();
        address.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAddress_Fails_WhenAddressNotFound()
    {
        _addressRepo.Setup(r => r.GetByIdAndUserIdAsync(999, "u1", default))
                    .ReturnsAsync((UserAddress?)null);

        var svc = CreateService();
        var (success, error, _) = await svc.UpdateAddressAsync(999, "u1", BuildUpdateRequest());

        success.Should().BeFalse();
        error.Should().Contain("not found");
    }

    // ── DeleteAddress ─────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAddress_Succeeds_WhenAddressExists()
    {
        var existing = BuildAddress(3, "u1");
        _addressRepo.Setup(r => r.GetByIdAndUserIdAsync(3, "u1", default)).ReturnsAsync(existing);
        _addressRepo.Setup(r => r.SoftDeleteAsync(3, "u1", default)).Returns(Task.CompletedTask);

        var svc = CreateService();
        var (success, error) = await svc.DeleteAddressAsync(3, "u1");

        success.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAddress_Fails_WhenAddressBelongsToOtherUser()
    {
        // Address 3 belongs to "u2", not "u1" — repository returns null for wrong userId
        _addressRepo.Setup(r => r.GetByIdAndUserIdAsync(3, "u1", default))
                    .ReturnsAsync((UserAddress?)null);

        var svc = CreateService();
        var (success, error) = await svc.DeleteAddressAsync(3, "u1");

        success.Should().BeFalse();
        error.Should().Contain("not found");
    }

    // ── SetDefault ────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetDefault_Succeeds_WhenAddressExists()
    {
        var existing = BuildAddress(4, "u1");
        _addressRepo.Setup(r => r.GetByIdAndUserIdAsync(4, "u1", default)).ReturnsAsync(existing);
        _addressRepo.Setup(r => r.SetDefaultAsync(4, "u1", default)).Returns(Task.CompletedTask);

        var svc = CreateService();
        var (success, error) = await svc.SetDefaultAsync(4, "u1");

        success.Should().BeTrue();
        error.Should().BeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static UserAddress BuildAddress(int id, string userId, bool isDefault = false) => new()
    {
        Id             = id,
        UserId         = userId,
        RecipientName  = "Test Recipient",
        RecipientMobile = "+919876543210",
        AddressLabel   = "Home",
        HouseNumber    = "12",
        BuildingStreet = "Main Street",
        Locality       = "Central",
        City           = "Mumbai",
        State          = "Maharashtra",
        Pincode        = "400001",
        IsDefault      = isDefault,
        IsDeleted      = false,
        CreatedAt      = DateTime.UtcNow,
        UpdatedAt      = DateTime.UtcNow
    };

    private static CreateAddressRequest BuildCreateRequest() => new()
    {
        RecipientName  = "Test Recipient",
        RecipientMobile = "9876543210",
        AddressLabel   = "Home",
        HouseNumber    = "12",
        BuildingStreet = "Main Street",
        Locality       = "Central",
        City           = "Mumbai",
        State          = "Maharashtra",
        Pincode        = "400001",
        IsDefault      = false
    };

    private static UpdateAddressRequest BuildUpdateRequest() => new()
    {
        RecipientName  = "Updated Recipient",
        RecipientMobile = "9876543210",
        AddressLabel   = "Work",
        HouseNumber    = "15",
        BuildingStreet = "Updated Street",
        Locality       = "New Area",
        City           = "Mumbai",
        State          = "Maharashtra",
        Pincode        = "400002",
        IsDefault      = true
    };
}
