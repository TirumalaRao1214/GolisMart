using FluentAssertions;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GOLISMart.API.Tests;

/// <summary>
/// Unit tests for DeliveryService — no database required.
/// </summary>
public class DeliveryServiceTests
{
    private readonly Mock<IDeliveryRepository> _repo = new();

    private DeliveryService CreateService() =>
        new(_repo.Object, NullLogger<DeliveryService>.Instance);

    // ── Format validation ────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("1234567")]
    [InlineData("ABCDEF")]
    [InlineData("000000")]
    [InlineData("12345a")]
    public async Task CheckServiceabilityAsync_ReturnsNotServiceable_ForInvalidPinFormat(string pin)
    {
        var service = CreateService();
        var result  = await service.CheckServiceabilityAsync(pin);

        result.IsServiceable.Should().BeFalse();
        result.UnavailableReason.Should().Contain("Invalid PIN code format");
    }

    [Fact]
    public async Task CheckServiceabilityAsync_ReturnsNotServiceable_WhenPinNotConfigured()
    {
        _repo.Setup(r => r.GetByPinCodeAsync("999999", default))
             .ReturnsAsync((DeliveryArea?)null);

        var service = CreateService();
        var result  = await service.CheckServiceabilityAsync("999999");

        result.IsServiceable.Should().BeFalse();
        result.UnavailableReason.Should().Contain("not available");
    }

    [Fact]
    public async Task CheckServiceabilityAsync_ReturnsServiceable_WhenPinConfigured()
    {
        var area = new DeliveryArea
        {
            Id       = 1,
            PinCode  = "560001",
            AreaName = "Bengaluru – MG Road",
            IsActive = true,
            EstimatedDeliveryWindow = "Today 6 – 9 PM",
            MinOrderValue  = 100m,
            DeliveryFee    = 40m,
            FreeDeliveryAbove = 500m
        };

        _repo.Setup(r => r.GetByPinCodeAsync("560001", default))
             .ReturnsAsync(area);

        var service = CreateService();
        var result  = await service.CheckServiceabilityAsync("560001");

        result.IsServiceable.Should().BeTrue();
        result.AreaName.Should().Be("Bengaluru – MG Road");
        result.EstimatedDeliveryWindow.Should().Be("Today 6 – 9 PM");
        result.MinOrderValue.Should().Be(100m);
        result.DeliveryFee.Should().Be(40m);
        result.FreeDeliveryAbove.Should().Be(500m);
        result.UnavailableReason.Should().BeNull();
    }

    [Fact]
    public async Task CheckServiceabilityAsync_ReturnsServiceable_WhenNoOptionalFieldsSet()
    {
        var area = new DeliveryArea
        {
            Id       = 2,
            PinCode  = "560003",
            AreaName = "Bengaluru – Malleshwaram",
            IsActive = true,
            EstimatedDeliveryWindow = null,
            MinOrderValue  = null,
            DeliveryFee    = null,
            FreeDeliveryAbove = null
        };

        _repo.Setup(r => r.GetByPinCodeAsync("560003", default))
             .ReturnsAsync(area);

        var service = CreateService();
        var result  = await service.CheckServiceabilityAsync("560003");

        result.IsServiceable.Should().BeTrue();
        result.EstimatedDeliveryWindow.Should().BeNull();
        result.MinOrderValue.Should().BeNull();
        result.DeliveryFee.Should().BeNull();
        result.FreeDeliveryAbove.Should().BeNull();
    }

    // ── GetActiveAreasAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveAreasAsync_ReturnsMappedDtos()
    {
        var areas = new List<DeliveryArea>
        {
            new() { Id = 1, PinCode = "560001", AreaName = "Area A", IsActive = true },
            new() { Id = 2, PinCode = "560002", AreaName = "Area B", IsActive = true }
        };

        _repo.Setup(r => r.GetAllActiveAsync(default)).ReturnsAsync(areas);

        var service = CreateService();
        var result  = await service.GetActiveAreasAsync();

        result.Should().HaveCount(2);
        result[0].PinCode.Should().Be("560001");
        result[1].PinCode.Should().Be("560002");
    }

    [Fact]
    public async Task GetActiveAreasAsync_ReturnsEmpty_WhenNoneConfigured()
    {
        _repo.Setup(r => r.GetAllActiveAsync(default))
             .ReturnsAsync(new List<DeliveryArea>());

        var service = CreateService();
        var result  = await service.GetActiveAreasAsync();

        result.Should().BeEmpty();
    }
}
