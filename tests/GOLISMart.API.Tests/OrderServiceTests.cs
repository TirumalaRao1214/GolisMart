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
/// Unit tests for OrderService — no database required.
/// </summary>
public class OrderServiceTests
{
    private readonly Mock<ICartRepository>      _cartRepo    = new();
    private readonly Mock<IProductRepository>   _productRepo = new();
    private readonly Mock<IInventoryRepository> _invRepo     = new();
    private readonly Mock<IAddressRepository>   _addrRepo    = new();
    private readonly Mock<IDeliveryService>     _delivery    = new();
    private readonly Mock<IOrderRepository>     _orderRepo   = new();

    private const string UserId     = "user-abc-123";
    private const string SessionKey = "session-key-xyz";

    private IConfiguration BuildConfig(decimal deliveryFee = 40m, decimal freeAbove = 500m)
    {
        var dict = new Dictionary<string, string?>
        {
            ["CartSettings:DeliveryFee"]       = deliveryFee.ToString(),
            ["CartSettings:FreeDeliveryAbove"] = freeAbove.ToString()
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private OrderService CreateService(IConfiguration? config = null) =>
        new(_cartRepo.Object, _productRepo.Object, _invRepo.Object,
            _addrRepo.Object, _delivery.Object, _orderRepo.Object,
            config ?? BuildConfig(),
            NullLogger<OrderService>.Instance);

    private static Cart MakeCart(int id = 1) =>
        new() { Id = id, SessionKey = SessionKey, UserId = UserId };

    private static CartItem MakeCartItem(int productId, decimal qty = 2m) =>
        new() { Id = productId * 100, CartId = 1, ProductId = productId, Quantity = qty, UnitPrice = 50m };

    private static Product MakeProduct(int id, decimal price = 50m, decimal? offerPrice = null) =>
        new() { Id = id, Name = $"Product {id}", SKU = $"SKU{id}", UnitLabel = "1 kg",
                Price = price, OfferPrice = offerPrice, IsActive = true };

    private static InventoryStatus MakeInventory(int productId, decimal qty = 100m) =>
        new() { ProductId = productId, QuantityOnHand = qty, ReservedQuantity = 0m, LowStockThreshold = 5m };

    private static UserAddress MakeAddress() =>
        new() { Id = 1, UserId = UserId, HouseNumber = "12", BuildingStreet = "Main St",
                Locality = "Area", City = "City", State = "State", Pincode = "560001" };

    private static ServiceabilityResponse MakeServiceable(decimal? minOrder = null) =>
        new("560001", true, "Area", "Today 6-9 PM", minOrder, 40m, 500m, null);

    private static Order MakeCreatedOrder() =>
        new() { Id = 1, OrderNumber = "GM-20261011-ABCDEF01", UserId = UserId,
                ShipToAddressLine1 = "12, Main St", ShipToCity = "City",
                ShipToState = "State", ShipToPincode = "560001",
                SubTotal = 100m, DeliveryFee = 40m, TotalAmount = 140m,
                PaymentMethod = "COD", PaymentStatus = "Pending", Status = "Pending",
                CreatedAt = DateTime.UtcNow };

    // ── Empty cart ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_Throws_WhenCartIsNull()
    {
        _cartRepo.Setup(r => r.GetBySessionKeyAsync(SessionKey, default))
                 .ReturnsAsync((Cart?)null);

        var service = CreateService();
        var request = new PlaceOrderRequest(SessionKey, 1, "COD", null, null);

        var act = () => service.PlaceOrderAsync(request, UserId);
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*cart is empty*");
    }

    [Fact]
    public async Task PlaceOrderAsync_Throws_WhenCartItemsEmpty()
    {
        _cartRepo.Setup(r => r.GetBySessionKeyAsync(SessionKey, default))
                 .ReturnsAsync(MakeCart());
        _cartRepo.Setup(r => r.GetItemsAsync(1, default))
                 .ReturnsAsync(Array.Empty<CartItem>());

        var service = CreateService();
        var request = new PlaceOrderRequest(SessionKey, 1, "COD", null, null);

        var act = () => service.PlaceOrderAsync(request, UserId);
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*cart is empty*");
    }

    // ── Address not found ─────────────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_Throws_WhenAddressNotFound()
    {
        _cartRepo.Setup(r => r.GetBySessionKeyAsync(SessionKey, default))
                 .ReturnsAsync(MakeCart());
        _cartRepo.Setup(r => r.GetItemsAsync(1, default))
                 .ReturnsAsync(new[] { MakeCartItem(1) });
        _addrRepo.Setup(r => r.GetByIdAndUserIdAsync(1, UserId, default))
                 .ReturnsAsync((UserAddress?)null);

        var service = CreateService();
        var request = new PlaceOrderRequest(SessionKey, 1, "COD", null, null);

        var act = () => service.PlaceOrderAsync(request, UserId);
        await act.Should().ThrowAsync<ArgumentException>()
                 .WithMessage("*delivery address was not found*");
    }

    // ── Delivery not serviceable ──────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_Throws_WhenPincodeNotServiceable()
    {
        _cartRepo.Setup(r => r.GetBySessionKeyAsync(SessionKey, default))
                 .ReturnsAsync(MakeCart());
        _cartRepo.Setup(r => r.GetItemsAsync(1, default))
                 .ReturnsAsync(new[] { MakeCartItem(1) });
        _addrRepo.Setup(r => r.GetByIdAndUserIdAsync(1, UserId, default))
                 .ReturnsAsync(MakeAddress());
        _delivery.Setup(s => s.CheckServiceabilityAsync("560001", default))
                 .ReturnsAsync(new ServiceabilityResponse("560001", false, null, null, null, null, null, "Not available"));

        var service = CreateService();
        var request = new PlaceOrderRequest(SessionKey, 1, "COD", null, null);

        var act = () => service.PlaceOrderAsync(request, UserId);
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*Not available*");
    }

    // ── Invalid payment method ────────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_Throws_WhenPaymentMethodInvalid()
    {
        var service = CreateService();
        var request = new PlaceOrderRequest(SessionKey, 1, "Bitcoin", null, null);

        // Even before cart check, payment method is validated first only for valid requests.
        // The service actually checks payment method AFTER idempotency — but cart check is first.
        // Let's set up so the first check that throws is payment method.
        _cartRepo.Setup(r => r.GetBySessionKeyAsync(SessionKey, default))
                 .ReturnsAsync(MakeCart());
        _cartRepo.Setup(r => r.GetItemsAsync(1, default))
                 .ReturnsAsync(new[] { MakeCartItem(1) });

        var act = () => service.PlaceOrderAsync(request, UserId);
        await act.Should().ThrowAsync<ArgumentException>()
                 .WithMessage("*not supported*");
    }

    // ── Product unavailable ───────────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_Throws_WhenProductInactive()
    {
        var inactiveProduct = MakeProduct(1);
        inactiveProduct.IsActive = false;

        _cartRepo.Setup(r => r.GetBySessionKeyAsync(SessionKey, default))
                 .ReturnsAsync(MakeCart());
        _cartRepo.Setup(r => r.GetItemsAsync(1, default))
                 .ReturnsAsync(new[] { MakeCartItem(1) });
        _addrRepo.Setup(r => r.GetByIdAndUserIdAsync(1, UserId, default))
                 .ReturnsAsync(MakeAddress());
        _delivery.Setup(s => s.CheckServiceabilityAsync("560001", default))
                 .ReturnsAsync(MakeServiceable());
        _invRepo.Setup(r => r.GetByProductIdsAsync(new[] { 1 }, default))
                .ReturnsAsync(new Dictionary<int, InventoryStatus> { [1] = MakeInventory(1) });
        _productRepo.Setup(r => r.GetByIdAsync(1, default))
                    .ReturnsAsync(inactiveProduct);

        var service = CreateService();
        var request = new PlaceOrderRequest(SessionKey, 1, "COD", null, null);

        var act = () => service.PlaceOrderAsync(request, UserId);
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*no longer available*");
    }

    // ── Insufficient stock ────────────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_Throws_WhenInsufficientStock()
    {
        _cartRepo.Setup(r => r.GetBySessionKeyAsync(SessionKey, default))
                 .ReturnsAsync(MakeCart());
        _cartRepo.Setup(r => r.GetItemsAsync(1, default))
                 .ReturnsAsync(new[] { MakeCartItem(1, qty: 10m) });
        _addrRepo.Setup(r => r.GetByIdAndUserIdAsync(1, UserId, default))
                 .ReturnsAsync(MakeAddress());
        _delivery.Setup(s => s.CheckServiceabilityAsync("560001", default))
                 .ReturnsAsync(MakeServiceable());
        _invRepo.Setup(r => r.GetByProductIdsAsync(new[] { 1 }, default))
                .ReturnsAsync(new Dictionary<int, InventoryStatus> { [1] = MakeInventory(1, qty: 3m) });  // only 3 in stock
        _productRepo.Setup(r => r.GetByIdAsync(1, default))
                    .ReturnsAsync(MakeProduct(1));

        var service = CreateService();
        var request = new PlaceOrderRequest(SessionKey, 1, "COD", null, null);

        var act = () => service.PlaceOrderAsync(request, UserId);
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*3*available*");
    }

    // ── Minimum order not met ─────────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_Throws_WhenMinOrderNotMet()
    {
        _cartRepo.Setup(r => r.GetBySessionKeyAsync(SessionKey, default))
                 .ReturnsAsync(MakeCart());
        _cartRepo.Setup(r => r.GetItemsAsync(1, default))
                 .ReturnsAsync(new[] { MakeCartItem(1, qty: 1m) });   // qty=1, price=50 → subtotal=50
        _addrRepo.Setup(r => r.GetByIdAndUserIdAsync(1, UserId, default))
                 .ReturnsAsync(MakeAddress());
        _delivery.Setup(s => s.CheckServiceabilityAsync("560001", default))
                 .ReturnsAsync(MakeServiceable(minOrder: 200m));       // min order = 200
        _invRepo.Setup(r => r.GetByProductIdsAsync(new[] { 1 }, default))
                .ReturnsAsync(new Dictionary<int, InventoryStatus> { [1] = MakeInventory(1) });
        _productRepo.Setup(r => r.GetByIdAsync(1, default))
                    .ReturnsAsync(MakeProduct(1, price: 50m));

        var service = CreateService();
        var request = new PlaceOrderRequest(SessionKey, 1, "COD", null, null);

        var act = () => service.PlaceOrderAsync(request, UserId);
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*minimum order*");
    }

    // ── Successful order placement ────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_Succeeds_WithValidRequest()
    {
        var cart      = MakeCart();
        var cartItems = new[] { MakeCartItem(1, qty: 2m) };
        var product   = MakeProduct(1, price: 50m);
        var inv       = MakeInventory(1);
        var address   = MakeAddress();
        var created   = MakeCreatedOrder();

        _cartRepo.Setup(r => r.GetBySessionKeyAsync(SessionKey, default))
                 .ReturnsAsync(cart);
        _cartRepo.Setup(r => r.GetItemsAsync(1, default))
                 .ReturnsAsync(cartItems);
        _addrRepo.Setup(r => r.GetByIdAndUserIdAsync(1, UserId, default))
                 .ReturnsAsync(address);
        _delivery.Setup(s => s.CheckServiceabilityAsync("560001", default))
                 .ReturnsAsync(MakeServiceable());
        _invRepo.Setup(r => r.GetByProductIdsAsync(new[] { 1 }, default))
                .ReturnsAsync(new Dictionary<int, InventoryStatus> { [1] = inv });
        _productRepo.Setup(r => r.GetByIdAsync(1, default))
                    .ReturnsAsync(product);
        _orderRepo.Setup(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<IReadOnlyList<OrderItem>>(), default))
                  .ReturnsAsync(created);
        _cartRepo.Setup(r => r.ClearCartAsync(1, default)).Returns(Task.CompletedTask);

        var service  = CreateService();
        var request  = new PlaceOrderRequest(SessionKey, 1, "COD", null, null);
        var response = await service.PlaceOrderAsync(request, UserId);

        response.Should().NotBeNull();
        response.OrderId.Should().Be(1);
        response.Status.Should().Be("Pending");
        response.PaymentMethod.Should().Be("COD");

        // Cart must be cleared exactly once after successful placement
        _cartRepo.Verify(r => r.ClearCartAsync(1, default), Times.Once);
    }

    // ── Idempotency: duplicate request returns existing order ─────────────────

    [Fact]
    public async Task PlaceOrderAsync_ReturnsExistingOrder_WhenIdempotencyKeyMatches()
    {
        var idempotencyKey = Guid.NewGuid().ToString("N");
        var existingOrder  = MakeCreatedOrder();
        existingOrder.IdempotencyKey = idempotencyKey;

        _orderRepo.Setup(r => r.GetByIdempotencyKeyAsync(idempotencyKey, UserId, default))
                  .ReturnsAsync(existingOrder);

        // Simulate the detail load for building the confirmation response
        _orderRepo.Setup(r => r.GetDetailByIdAndUserIdAsync(existingOrder.Id, UserId, default))
                  .ReturnsAsync((existingOrder, (IReadOnlyList<OrderItem>)Array.Empty<OrderItem>()));

        var service  = CreateService();
        var request  = new PlaceOrderRequest(SessionKey, 1, "COD", null, idempotencyKey);
        var response = await service.PlaceOrderAsync(request, UserId);

        response.OrderId.Should().Be(existingOrder.Id);
        response.OrderNumber.Should().Be(existingOrder.OrderNumber);

        // Cart should NOT be cleared on duplicate detection
        _cartRepo.Verify(r => r.ClearCartAsync(It.IsAny<int>(), default), Times.Never);
        // Order should NOT be created again
        _orderRepo.Verify(r => r.CreateOrderAsync(It.IsAny<Order>(), It.IsAny<IReadOnlyList<OrderItem>>(), default), Times.Never);
    }

    // ── Cancel order ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelOrderAsync_Succeeds_WhenOrderIsCancellable()
    {
        _orderRepo.Setup(r => r.CancelOrderAsync(1, UserId, UserId, default))
                  .ReturnsAsync(true);

        var service  = CreateService();
        var response = await service.CancelOrderAsync(1, UserId);

        response.Success.Should().BeTrue();
        response.NewStatus.Should().Be("Cancelled");
    }

    [Fact]
    public async Task CancelOrderAsync_Fails_WhenOrderNotCancellable()
    {
        var deliveredOrder = MakeCreatedOrder();
        deliveredOrder.Status = "Delivered";

        _orderRepo.Setup(r => r.CancelOrderAsync(1, UserId, UserId, default))
                  .ReturnsAsync(false);
        _orderRepo.Setup(r => r.GetByIdAndUserIdAsync(1, UserId, default))
                  .ReturnsAsync(deliveredOrder);

        var service  = CreateService();
        var response = await service.CancelOrderAsync(1, UserId);

        response.Success.Should().BeFalse();
        response.NewStatus.Should().Be("Delivered");
    }

    [Fact]
    public async Task CancelOrderAsync_ReturnsNotFound_WhenOrderDoesNotExist()
    {
        _orderRepo.Setup(r => r.CancelOrderAsync(999, UserId, UserId, default))
                  .ReturnsAsync(false);
        _orderRepo.Setup(r => r.GetByIdAndUserIdAsync(999, UserId, default))
                  .ReturnsAsync((Order?)null);

        var service  = CreateService();
        var response = await service.CancelOrderAsync(999, UserId);

        response.Success.Should().BeFalse();
        response.Message.Should().Contain("not found");
    }

    // ── Server-side price calculation ─────────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_UsesServerSidePrice_NotClientSubmittedPrice()
    {
        // CartItem has unitPrice = 100 (stale), but live product price is 50
        var cartItemWithStalePrice = new CartItem
        {
            Id = 101, CartId = 1, ProductId = 1,
            Quantity = 2m,
            UnitPrice = 100m   // stale price in cart
        };

        var liveProduct = MakeProduct(1, price: 50m);   // live price is 50
        var created     = MakeCreatedOrder();
        created.SubTotal = 100m;   // 2 * 50 = 100 (server computed)

        _cartRepo.Setup(r => r.GetBySessionKeyAsync(SessionKey, default))
                 .ReturnsAsync(MakeCart());
        _cartRepo.Setup(r => r.GetItemsAsync(1, default))
                 .ReturnsAsync(new[] { cartItemWithStalePrice });
        _addrRepo.Setup(r => r.GetByIdAndUserIdAsync(1, UserId, default))
                 .ReturnsAsync(MakeAddress());
        _delivery.Setup(s => s.CheckServiceabilityAsync("560001", default))
                 .ReturnsAsync(MakeServiceable());
        _invRepo.Setup(r => r.GetByProductIdsAsync(new[] { 1 }, default))
                .ReturnsAsync(new Dictionary<int, InventoryStatus> { [1] = MakeInventory(1) });
        _productRepo.Setup(r => r.GetByIdAsync(1, default))
                    .ReturnsAsync(liveProduct);
        _orderRepo.Setup(r => r.CreateOrderAsync(
                      It.Is<Order>(o => o.SubTotal == 100m),   // subtotal = 2 * 50 (server price)
                      It.IsAny<IReadOnlyList<OrderItem>>(), default))
                  .ReturnsAsync(created);
        _cartRepo.Setup(r => r.ClearCartAsync(1, default)).Returns(Task.CompletedTask);

        var service  = CreateService();
        var request  = new PlaceOrderRequest(SessionKey, 1, "COD", null, null);
        var response = await service.PlaceOrderAsync(request, UserId);

        // Verify server used the live price (50), not the stale cart price (100)
        _orderRepo.Verify(r => r.CreateOrderAsync(
            It.Is<Order>(o => o.SubTotal == 100m),
            It.Is<IReadOnlyList<OrderItem>>(items =>
                items.Count == 1 &&
                items[0].EffectivePrice == 50m &&
                items[0].LineTotal == 100m),
            default), Times.Once);
    }

    // ── Free delivery threshold ───────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_AppliesFreeDelivery_WhenSubtotalMeetsThreshold()
    {
        // 3 items * price 200 = 600 ≥ freeAbove 500 → delivery = 0
        var cartItem = MakeCartItem(1, qty: 3m);
        var product  = MakeProduct(1, price: 200m);
        var created  = MakeCreatedOrder();
        created.DeliveryFee = 0m;
        created.SubTotal    = 600m;
        created.TotalAmount = 600m;

        _cartRepo.Setup(r => r.GetBySessionKeyAsync(SessionKey, default))
                 .ReturnsAsync(MakeCart());
        _cartRepo.Setup(r => r.GetItemsAsync(1, default))
                 .ReturnsAsync(new[] { cartItem });
        _addrRepo.Setup(r => r.GetByIdAndUserIdAsync(1, UserId, default))
                 .ReturnsAsync(MakeAddress());
        _delivery.Setup(s => s.CheckServiceabilityAsync("560001", default))
                 .ReturnsAsync(MakeServiceable());
        _invRepo.Setup(r => r.GetByProductIdsAsync(new[] { 1 }, default))
                .ReturnsAsync(new Dictionary<int, InventoryStatus> { [1] = MakeInventory(1, 100m) });
        _productRepo.Setup(r => r.GetByIdAsync(1, default))
                    .ReturnsAsync(product);
        _orderRepo.Setup(r => r.CreateOrderAsync(
                      It.Is<Order>(o => o.DeliveryFee == 0m),
                      It.IsAny<IReadOnlyList<OrderItem>>(), default))
                  .ReturnsAsync(created);
        _cartRepo.Setup(r => r.ClearCartAsync(1, default)).Returns(Task.CompletedTask);

        var service  = CreateService();
        var request  = new PlaceOrderRequest(SessionKey, 1, "COD", null, null);
        var response = await service.PlaceOrderAsync(request, UserId);

        _orderRepo.Verify(r => r.CreateOrderAsync(
            It.Is<Order>(o => o.DeliveryFee == 0m && o.TotalAmount == 600m),
            It.IsAny<IReadOnlyList<OrderItem>>(), default), Times.Once);
    }
}
