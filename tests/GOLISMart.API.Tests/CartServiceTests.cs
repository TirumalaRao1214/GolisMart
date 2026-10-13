using FluentAssertions;
using GOLISMart.API.DTOs;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GOLISMart.API.Tests;

/// <summary>
/// Unit tests for CartService — no database required.
/// </summary>
public class CartServiceTests
{
    private readonly Mock<ICartRepository>      _cartRepo  = new();
    private readonly Mock<IProductRepository>   _prodRepo  = new();
    private readonly Mock<IInventoryRepository> _invRepo   = new();

    private CartService CreateService() =>
        new(_cartRepo.Object, _prodRepo.Object, _invRepo.Object,
            NullLogger<CartService>.Instance);

    // ── GetCartAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCartAsync_ReturnsEmptyCart_WhenNoCartExists()
    {
        _cartRepo.Setup(r => r.GetBySessionKeyAsync("abc", default))
                 .ReturnsAsync((Cart?)null);

        var service = CreateService();
        var result  = await service.GetCartAsync("abc");

        result.Should().NotBeNull();
        result.SessionKey.Should().Be("abc");
        result.Items.Should().BeEmpty();
        result.TotalItems.Should().Be(0);
        result.SubTotal.Should().Be(0m);
    }

    [Fact]
    public async Task GetCartAsync_ReturnsCartWithItems_WhenCartExists()
    {
        var cart = new Cart { Id = 1, SessionKey = "sess1" };
        var items = new List<CartItem>
        {
            new() { Id = 1, CartId = 1, ProductId = 10, Quantity = 2, UnitPrice = 50m }
        };
        var product = BuildProduct(10, "Tomato", "tomato", 50m, null);
        var inv = new InventoryStatus { ProductId = 10, QuantityOnHand = 20, ReservedQuantity = 0, LowStockThreshold = 5 };

        _cartRepo.Setup(r => r.GetBySessionKeyAsync("sess1", default)).ReturnsAsync(cart);
        _cartRepo.Setup(r => r.GetItemsAsync(1, default)).ReturnsAsync(items);
        _prodRepo.Setup(r => r.GetByIdAsync(10, default)).ReturnsAsync(product);
        _invRepo.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<int>>(), default))
                .ReturnsAsync(new Dictionary<int, InventoryStatus> { [10] = inv });

        var service = CreateService();
        var result  = await service.GetCartAsync("sess1");

        result.Items.Should().HaveCount(1);
        result.Items[0].ProductId.Should().Be(10);
        result.Items[0].Quantity.Should().Be(2);
        result.Items[0].LineTotal.Should().Be(100m);
        result.TotalItems.Should().Be(2);
        result.SubTotal.Should().Be(100m);
    }

    // ── AddItemAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AddItemAsync_ThrowsArgumentException_ForZeroQuantity()
    {
        var service = CreateService();
        var act = () => service.AddItemAsync("sess", 1, 0);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Quantity*");
    }

    [Fact]
    public async Task AddItemAsync_ThrowsArgumentException_ForQuantityOver20()
    {
        var service = CreateService();
        var act = () => service.AddItemAsync("sess", 1, 21);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Quantity*");
    }

    [Fact]
    public async Task AddItemAsync_ThrowsKeyNotFoundException_ForMissingProduct()
    {
        _prodRepo.Setup(r => r.GetByIdAsync(999, default)).ReturnsAsync((Product?)null);

        var service = CreateService();
        var act = () => service.AddItemAsync("sess", 999, 1);
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*999*");
    }

    [Fact]
    public async Task AddItemAsync_ThrowsArgumentException_WhenOutOfStock()
    {
        var product = BuildProduct(1, "Apple", "apple", 30m, null);
        _prodRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(product);
        _invRepo.Setup(r => r.GetByProductIdAsync(1, default))
                .ReturnsAsync(new InventoryStatus { ProductId = 1, QuantityOnHand = 0, ReservedQuantity = 0, LowStockThreshold = 5 });

        var service = CreateService();
        var act = () => service.AddItemAsync("sess", 1, 1);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*out of stock*");
    }

    [Fact]
    public async Task AddItemAsync_Succeeds_AndReturnsCartWithItem()
    {
        var product = BuildProduct(1, "Onion", "onion", 40m, 35m);
        var inv     = new InventoryStatus { ProductId = 1, QuantityOnHand = 50, ReservedQuantity = 0, LowStockThreshold = 5 };
        var cart    = new Cart { Id = 10, SessionKey = "sess2" };
        var items   = new List<CartItem>
        {
            new() { Id = 1, CartId = 10, ProductId = 1, Quantity = 2, UnitPrice = 35m }
        };

        _prodRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(product);
        _invRepo.Setup(r => r.GetByProductIdAsync(1, default)).ReturnsAsync(inv);
        _cartRepo.Setup(r => r.GetOrCreateBySessionKeyAsync("sess2", default)).ReturnsAsync(cart);
        _cartRepo.Setup(r => r.GetItemsAsync(10, default)).ReturnsAsync(new List<CartItem>());  // empty before upsert
        _cartRepo.Setup(r => r.UpsertItemAsync(10, 1, 2m, 35m, default)).Returns(Task.CompletedTask);
        _cartRepo.Setup(r => r.TouchCartAsync(10, default)).Returns(Task.CompletedTask);
        // After upsert: return items
        _cartRepo.SetupSequence(r => r.GetItemsAsync(10, default))
            .ReturnsAsync(new List<CartItem>())
            .ReturnsAsync(items);
        _prodRepo.Setup(r => r.GetByIdAsync(1, default)).ReturnsAsync(product);
        _invRepo.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<int>>(), default))
                .ReturnsAsync(new Dictionary<int, InventoryStatus> { [1] = inv });

        var service = CreateService();
        var result  = await service.AddItemAsync("sess2", 1, 2m);

        result.Items.Should().HaveCount(1);
        result.Items[0].UnitPrice.Should().Be(35m); // OfferPrice used
        result.Items[0].OriginalPrice.Should().Be(40m);
    }

    // ── UpdateItemAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateItemAsync_Succeeds_AndReturnsUpdatedCart()
    {
        var cart = new Cart { Id = 5, SessionKey = "sess3" };
        var inv  = new InventoryStatus { ProductId = 2, QuantityOnHand = 100, ReservedQuantity = 0, LowStockThreshold = 5 };
        var updatedItems = new List<CartItem>
        {
            new() { Id = 1, CartId = 5, ProductId = 2, Quantity = 5, UnitPrice = 20m }
        };
        var product = BuildProduct(2, "Carrot", "carrot", 20m, null);

        _cartRepo.Setup(r => r.GetBySessionKeyAsync("sess3", default)).ReturnsAsync(cart);
        _invRepo.Setup(r => r.GetByProductIdAsync(2, default)).ReturnsAsync(inv);
        _cartRepo.Setup(r => r.UpdateItemQuantityAsync(5, 2, 5m, default)).Returns(Task.CompletedTask);
        _cartRepo.Setup(r => r.TouchCartAsync(5, default)).Returns(Task.CompletedTask);
        _cartRepo.Setup(r => r.GetItemsAsync(5, default)).ReturnsAsync(updatedItems);
        _prodRepo.Setup(r => r.GetByIdAsync(2, default)).ReturnsAsync(product);
        _invRepo.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<int>>(), default))
                .ReturnsAsync(new Dictionary<int, InventoryStatus> { [2] = inv });

        var service = CreateService();
        var result  = await service.UpdateItemAsync("sess3", 2, 5m);

        result.Items.Should().HaveCount(1);
        result.Items[0].Quantity.Should().Be(5);
    }

    // ── RemoveItemAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveItemAsync_ReturnsCartWithoutRemovedItem()
    {
        var cart = new Cart { Id = 7, SessionKey = "sess4" };

        _cartRepo.Setup(r => r.GetBySessionKeyAsync("sess4", default)).ReturnsAsync(cart);
        _cartRepo.Setup(r => r.RemoveItemAsync(7, 3, default)).ReturnsAsync(true);
        _cartRepo.Setup(r => r.TouchCartAsync(7, default)).Returns(Task.CompletedTask);
        _cartRepo.Setup(r => r.GetItemsAsync(7, default)).ReturnsAsync(new List<CartItem>());
        _invRepo.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<int>>(), default))
                .ReturnsAsync(new Dictionary<int, InventoryStatus>());

        var service = CreateService();
        var result  = await service.RemoveItemAsync("sess4", 3);

        result.Items.Should().BeEmpty();
        result.SubTotal.Should().Be(0m);
    }

    // ── ClearCartAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task ClearCartAsync_ReturnsEmptyCart()
    {
        var cart = new Cart { Id = 9, SessionKey = "sess5" };

        _cartRepo.Setup(r => r.GetBySessionKeyAsync("sess5", default)).ReturnsAsync(cart);
        _cartRepo.Setup(r => r.ClearCartAsync(9, default)).Returns(Task.CompletedTask);
        _cartRepo.Setup(r => r.TouchCartAsync(9, default)).Returns(Task.CompletedTask);

        var service = CreateService();
        var result  = await service.ClearCartAsync("sess5");

        result.Items.Should().BeEmpty();
        result.TotalItems.Should().Be(0);
        result.SubTotal.Should().Be(0m);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Product BuildProduct(int id, string name, string slug, decimal price, decimal? offer) =>
        new()
        {
            Id           = id,
            Name         = name,
            Slug         = slug,
            SKU          = $"SKU-{id:000}",
            CategoryId   = 1,
            CategoryName = "Test Category",
            CategorySlug = "test",
            UnitCode     = "kg",
            UnitLabel    = "1 kg",
            Price        = price,
            OfferPrice   = offer,
            IsFeatured   = false,
            IsActive     = true
        };
}
