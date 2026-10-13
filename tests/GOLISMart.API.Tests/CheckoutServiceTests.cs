using FluentAssertions;
using GOLISMart.API.DTOs;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Implementations;
using GOLISMart.API.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GOLISMart.API.Tests;

/// <summary>
/// Unit tests for CheckoutService — no database required.
/// </summary>
public class CheckoutServiceTests
{
    private readonly Mock<ICartRepository>      _cartRepo   = new();
    private readonly Mock<IProductRepository>   _productRepo = new();
    private readonly Mock<IInventoryRepository> _invRepo    = new();
    private readonly Mock<IAddressRepository>   _addrRepo   = new();
    private readonly Mock<IDeliveryService>     _delivery   = new();

    private IConfiguration BuildConfig(decimal deliveryFee = 40m, decimal freeAbove = 500m)
    {
        var dict = new Dictionary<string, string?>
        {
            ["CartSettings:DeliveryFee"]     = deliveryFee.ToString(),
            ["CartSettings:FreeDeliveryAbove"] = freeAbove.ToString()
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private CheckoutService CreateService(IConfiguration? config = null) =>
        new(_cartRepo.Object, _productRepo.Object, _invRepo.Object,
            _addrRepo.Object, _delivery.Object,
            config ?? BuildConfig(),
            NullLogger<CheckoutService>.Instance);

    // ── Empty cart ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_Fails_WhenCartDoesNotExist()
    {
        _cartRepo.Setup(r => r.GetBySessionKeyAsync("sess", default))
                 .ReturnsAsync((Cart?)null);

        var service = CreateService();
        var result  = await service.ValidateAsync("sess", 1, "user1");

        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainMatch("*empty*");
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenCartHasNoItems()
    {
        var cart = new Cart { Id = 1, SessionKey = "sess" };
        _cartRepo.Setup(r => r.GetBySessionKeyAsync("sess", default)).ReturnsAsync(cart);
        _cartRepo.Setup(r => r.GetItemsAsync(1, default)).ReturnsAsync(new List<CartItem>());

        var service = CreateService();
        var result  = await service.ValidateAsync("sess", 1, "user1");

        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainMatch("*empty*");
    }

    // ── Address ownership ────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_Fails_WhenAddressDoesNotBelongToUser()
    {
        SetupCartWithOneItem("sess", cartId: 1, productId: 10, qty: 2m, price: 50m);
        // Address NOT found for this user (ownership enforced)
        _addrRepo.Setup(r => r.GetByIdAndUserIdAsync(99, "user1", default))
                 .ReturnsAsync((UserAddress?)null);

        var service = CreateService();
        var result  = await service.ValidateAsync("sess", 99, "user1");

        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainMatch("*address*");
    }

    // ── Delivery serviceability ───────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_Fails_WhenPinNotServiceable()
    {
        SetupCartWithOneItem("sess", cartId: 1, productId: 10, qty: 2m, price: 50m);
        SetupProduct(10, price: 50m);
        SetupInventory(10, qty: 100m);

        var address = BuildAddress(addressId: 1, userId: "user1", pincode: "999999");
        _addrRepo.Setup(r => r.GetByIdAndUserIdAsync(1, "user1", default))
                 .ReturnsAsync(address);

        _delivery.Setup(d => d.CheckServiceabilityAsync("999999", default))
                 .ReturnsAsync(new ServiceabilityResponse(
                     PinCode: "999999", IsServiceable: false,
                     AreaName: null, EstimatedDeliveryWindow: null,
                     MinOrderValue: null, DeliveryFee: null, FreeDeliveryAbove: null,
                     UnavailableReason: "Delivery not available."));

        var service = CreateService();
        var result  = await service.ValidateAsync("sess", 1, "user1");

        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainMatch("*not available*");
    }

    // ── Cart item validation ─────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_Fails_WhenProductInactive()
    {
        SetupCartWithOneItem("sess", cartId: 1, productId: 10, qty: 2m, price: 50m);
        SetupServiceablePin("560001");

        // Product is inactive
        _productRepo.Setup(r => r.GetByIdAsync(10, default))
                    .ReturnsAsync(BuildProduct(10, active: false));
        SetupAddress(1, "user1", "560001");

        var service = CreateService();
        var result  = await service.ValidateAsync("sess", 1, "user1");

        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainMatch("*no longer available*");
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenProductOutOfStock()
    {
        SetupCartWithOneItem("sess", cartId: 1, productId: 10, qty: 2m, price: 50m);
        SetupServiceablePin("560001");
        SetupAddress(1, "user1", "560001");

        _productRepo.Setup(r => r.GetByIdAsync(10, default))
                    .ReturnsAsync(BuildProduct(10, active: true));

        // Out of stock
        _invRepo.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<int>>(), default))
                .ReturnsAsync(new Dictionary<int, InventoryStatus>
                {
                    [10] = new InventoryStatus { ProductId = 10, QuantityOnHand = 0, ReservedQuantity = 0, LowStockThreshold = 5 }
                });

        var service = CreateService();
        var result  = await service.ValidateAsync("sess", 1, "user1");

        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainMatch("*out of stock*");
    }

    [Fact]
    public async Task ValidateAsync_Fails_WhenRequestedQtyExceedsStock()
    {
        SetupCartWithOneItem("sess", cartId: 1, productId: 10, qty: 10m, price: 50m);
        SetupServiceablePin("560001");
        SetupAddress(1, "user1", "560001");

        _productRepo.Setup(r => r.GetByIdAsync(10, default))
                    .ReturnsAsync(BuildProduct(10, active: true));

        // Only 3 in stock
        _invRepo.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<int>>(), default))
                .ReturnsAsync(new Dictionary<int, InventoryStatus>
                {
                    [10] = new InventoryStatus { ProductId = 10, QuantityOnHand = 3, ReservedQuantity = 0, LowStockThreshold = 5 }
                });

        var service = CreateService();
        var result  = await service.ValidateAsync("sess", 1, "user1");

        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainMatch("*3*available*");
    }

    // ── Minimum order ────────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_Fails_WhenSubtotalBelowMinOrder()
    {
        // Cart: 1 item at ₹50
        SetupCartWithOneItem("sess", cartId: 1, productId: 10, qty: 1m, price: 50m);
        SetupAddress(1, "user1", "560001");
        SetupProduct(10, price: 50m);
        SetupInventory(10, qty: 100m);

        _delivery.Setup(d => d.CheckServiceabilityAsync("560001", default))
                 .ReturnsAsync(new ServiceabilityResponse(
                     PinCode: "560001", IsServiceable: true,
                     AreaName: "Test Area", EstimatedDeliveryWindow: null,
                     MinOrderValue: 100m,   // min is ₹100
                     DeliveryFee: 40m, FreeDeliveryAbove: 500m,
                     UnavailableReason: null));

        var service = CreateService();
        var result  = await service.ValidateAsync("sess", 1, "user1");

        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainMatch("*minimum*");
    }

    // ── Successful checkout ──────────────────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_Succeeds_WithCorrectTotals()
    {
        // Cart: 2 items, ₹50 each → subtotal ₹100
        SetupCartWithOneItem("sess", cartId: 1, productId: 10, qty: 2m, price: 50m);
        SetupAddress(1, "user1", "560001");
        SetupProduct(10, price: 50m);
        SetupInventory(10, qty: 100m);

        _delivery.Setup(d => d.CheckServiceabilityAsync("560001", default))
                 .ReturnsAsync(new ServiceabilityResponse(
                     PinCode: "560001", IsServiceable: true,
                     AreaName: "Test Area", EstimatedDeliveryWindow: "Today 6 – 9 PM",
                     MinOrderValue: null, DeliveryFee: 40m, FreeDeliveryAbove: 500m,
                     UnavailableReason: null));

        var service = CreateService();
        var result  = await service.ValidateAsync("sess", 1, "user1");

        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
        result.Summary.Should().NotBeNull();
        result.Summary!.SubTotal.Should().Be(100m);
        result.Summary.DeliveryFee.Should().Be(40m);
        result.Summary.FinalTotal.Should().Be(140m);
        result.Summary.IsFreeDelivery.Should().BeFalse();
        result.Summary.EstimatedDeliveryWindow.Should().Be("Today 6 – 9 PM");
    }

    [Fact]
    public async Task ValidateAsync_Succeeds_WithFreeDeliveryWhenSubtotalMeetsThreshold()
    {
        // Cart: 12 items at ₹50 each → subtotal ₹600 (above ₹500 threshold)
        SetupCartWithOneItem("sess", cartId: 1, productId: 10, qty: 12m, price: 50m);
        SetupAddress(1, "user1", "560001");
        SetupProduct(10, price: 50m);
        SetupInventory(10, qty: 100m);

        _delivery.Setup(d => d.CheckServiceabilityAsync("560001", default))
                 .ReturnsAsync(new ServiceabilityResponse(
                     PinCode: "560001", IsServiceable: true,
                     AreaName: "Test Area", EstimatedDeliveryWindow: null,
                     MinOrderValue: null, DeliveryFee: 40m, FreeDeliveryAbove: 500m,
                     UnavailableReason: null));

        var service = CreateService();
        var result  = await service.ValidateAsync("sess", 1, "user1");

        result.IsValid.Should().BeTrue();
        result.Summary!.SubTotal.Should().Be(600m);
        result.Summary.DeliveryFee.Should().Be(0m);   // free delivery
        result.Summary.FinalTotal.Should().Be(600m);
        result.Summary.IsFreeDelivery.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_UsesOfferPriceForLineTotal()
    {
        SetupCartWithOneItem("sess", cartId: 1, productId: 10, qty: 3m, price: 60m);
        SetupAddress(1, "user1", "560001");
        SetupServiceablePin("560001");

        // Product has offer price ₹45, original ₹60
        _productRepo.Setup(r => r.GetByIdAsync(10, default))
                    .ReturnsAsync(BuildProduct(10, active: true, price: 60m, offerPrice: 45m));
        SetupInventory(10, qty: 100m);

        var service = CreateService();
        var result  = await service.ValidateAsync("sess", 1, "user1");

        result.IsValid.Should().BeTrue();
        result.Summary!.Items[0].UnitPrice.Should().Be(45m);
        result.Summary.Items[0].OriginalPrice.Should().Be(60m);
        result.Summary.Items[0].LineTotal.Should().Be(135m);  // 3 × 45
        result.Summary.SubTotal.Should().Be(135m);
    }

    // ── Exception-resilience regression tests ────────────────────────────────

    [Fact]
    public async Task ValidateAsync_ReturnsIsValidFalse_WhenDeliveryServiceThrows()
    {
        // Simulates the case where DeliveryAreas table does not exist:
        // DeliveryService catches SQL error 208 and re-throws for non-208 errors.
        // CheckoutService must catch any exception that bubbles up.
        SetupCartWithOneItem("sess", cartId: 1, productId: 10, qty: 2m, price: 50m);
        SetupAddress(1, "user1", "560001");
        SetupProduct(10, price: 50m);
        SetupInventory(10, qty: 100m);

        // Simulate an unexpected exception from the delivery service
        _delivery.Setup(d => d.CheckServiceabilityAsync("560001", default))
                 .ThrowsAsync(new InvalidOperationException("Simulated delivery service failure"));

        var service = CreateService();
        var result  = await service.ValidateAsync("sess", 1, "user1");

        // Must NOT throw — must return a safe IsValid:false response
        result.IsValid.Should().BeFalse();
        result.Issues.Should().NotBeEmpty();
        result.Issues[0].Should().Contain("server error");
    }

    [Fact]
    public async Task ValidateAsync_ReturnsIsValidFalse_WhenCartRepositoryThrows()
    {
        // Simulates a database connection failure when loading the cart
        _cartRepo.Setup(r => r.GetBySessionKeyAsync("sess", default))
                 .ThrowsAsync(new InvalidOperationException("Simulated DB failure"));

        var service = CreateService();
        var result  = await service.ValidateAsync("sess", 1, "user1");

        result.IsValid.Should().BeFalse();
        result.Issues.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ReturnsIsValidFalse_WhenDeliveryServiceReturnsNotConfigured()
    {
        // Simulates table existing but empty (no delivery areas configured)
        SetupCartWithOneItem("sess", cartId: 1, productId: 10, qty: 2m, price: 50m);
        SetupAddress(1, "user1", "999999");
        SetupProduct(10, price: 50m);
        SetupInventory(10, qty: 100m);

        _delivery.Setup(d => d.CheckServiceabilityAsync("999999", default))
                 .ReturnsAsync(new ServiceabilityResponse(
                     PinCode: "999999", IsServiceable: false,
                     AreaName: null, EstimatedDeliveryWindow: null,
                     MinOrderValue: null, DeliveryFee: null, FreeDeliveryAbove: null,
                     UnavailableReason: "Delivery serviceability has not been configured yet."));

        var service = CreateService();
        var result  = await service.ValidateAsync("sess", 1, "user1");

        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainMatch("*configured*");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetupCartWithOneItem(string sessionKey, int cartId, int productId, decimal qty, decimal price)
    {
        var cart = new Cart { Id = cartId, SessionKey = sessionKey };
        _cartRepo.Setup(r => r.GetBySessionKeyAsync(sessionKey, default)).ReturnsAsync(cart);
        _cartRepo.Setup(r => r.GetItemsAsync(cartId, default)).ReturnsAsync(
            new List<CartItem> { new() { CartId = cartId, ProductId = productId, Quantity = qty, UnitPrice = price } });
    }

    private void SetupProduct(int id, decimal price, decimal? offerPrice = null, bool active = true) =>
        _productRepo.Setup(r => r.GetByIdAsync(id, default))
                    .ReturnsAsync(BuildProduct(id, active, price, offerPrice));

    private void SetupInventory(int productId, decimal qty) =>
        _invRepo.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<int>>(), default))
                .ReturnsAsync(new Dictionary<int, InventoryStatus>
                {
                    [productId] = new InventoryStatus { ProductId = productId, QuantityOnHand = qty, ReservedQuantity = 0, LowStockThreshold = 5 }
                });

    private void SetupAddress(int addressId, string userId, string pincode) =>
        _addrRepo.Setup(r => r.GetByIdAndUserIdAsync(addressId, userId, default))
                 .ReturnsAsync(BuildAddress(addressId, userId, pincode));

    private void SetupServiceablePin(string pinCode) =>
        _delivery.Setup(d => d.CheckServiceabilityAsync(pinCode, default))
                 .ReturnsAsync(new ServiceabilityResponse(
                     PinCode: pinCode, IsServiceable: true,
                     AreaName: "Test Area", EstimatedDeliveryWindow: null,
                     MinOrderValue: null, DeliveryFee: 40m, FreeDeliveryAbove: 500m,
                     UnavailableReason: null));

    private static Product BuildProduct(int id, bool active = true, decimal price = 50m, decimal? offerPrice = null) =>
        new()
        {
            Id         = id,
            Name       = $"Product {id}",
            Slug       = $"product-{id}",
            SKU        = $"SKU-{id:000}",
            CategoryId = 1,
            UnitCode   = "kg",
            UnitLabel  = "1 kg",
            Price      = price,
            OfferPrice = offerPrice,
            IsActive   = active
        };

    private static UserAddress BuildAddress(int addressId, string userId, string pincode) =>
        new()
        {
            Id              = addressId,
            UserId          = userId,
            RecipientName   = "Test User",
            RecipientMobile = "+919876543210",
            AddressLabel    = "Home",
            HouseNumber     = "10",
            BuildingStreet  = "Test Street",
            Locality        = "Test Locality",
            City            = "Test City",
            State           = "Karnataka",
            Pincode         = pincode,
            IsDefault       = true
        };
}
