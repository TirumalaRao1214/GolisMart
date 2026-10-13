using FluentAssertions;
using GOLISMart.Web.Controllers;
using GOLISMart.Web.Models;
using GOLISMart.Web.Services;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GOLISMart.Web.Tests;

/// <summary>
/// Unit tests for Web CartController — no HTTP calls, no database.
/// </summary>
public class CartControllerTests
{
    private readonly Mock<ICartApiClient> _cartApi = new();
    private const string SessionKey = "test-session-key-abc";

    private CartController CreateController()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CartSettings:DeliveryFee"]      = "40",
                ["CartSettings:FreeDeliveryAbove"] = "500",
                ["ApiSettings:BaseUrl"]            = "https://localhost:5001"
            })
            .Build();

        var controller = new CartController(
            _cartApi.Object,
            config,
            NullLogger<CartController>.Instance);

        // Set up HttpContext with a mock session
        var session = new MockSession(SessionKey);
        var httpContext = new DefaultHttpContext();
        httpContext.Session = session;
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        // TempData is required by Add/Remove/etc actions
        var tempDataProvider = new Mock<ITempDataProvider>();
        tempDataProvider.Setup(p => p.LoadTempData(It.IsAny<HttpContext>()))
                        .Returns(new Dictionary<string, object?>());
        controller.TempData = new TempDataDictionary(httpContext, tempDataProvider.Object);

        return controller;
    }

    private static CartDto BuildCartDto(int count = 1) =>
        new CartDto(
            CartId:     1,
            SessionKey: SessionKey,
            Items:      Enumerable.Range(1, count).Select(i => new CartItemDto(
                ProductId:     i,
                Name:          $"Product {i}",
                Slug:          $"product-{i}",
                UnitLabel:     "1 kg",
                ImagePath:     null,
                Quantity:      1m,
                UnitPrice:     50m,
                OriginalPrice: 50m,
                LineTotal:     50m,
                IsInStock:     true,
                IsAvailable:   true
            )).ToList(),
            SubTotal:   count * 50m,
            TotalItems: count);

    // ── Index ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_ReturnsViewWithCartViewModel()
    {
        var cart = BuildCartDto(2);
        _cartApi.Setup(a => a.GetCartAsync(SessionKey, default)).ReturnsAsync(cart);

        var controller = CreateController();
        var result = await controller.Index(default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<CartViewModel>().Subject;
        model.Cart.Should().NotBeNull();
        model.Cart!.TotalItems.Should().Be(2);
        model.ApiAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Index_WhenApiReturnsNull_SetsApiAvailableFalse()
    {
        _cartApi.Setup(a => a.GetCartAsync(SessionKey, default)).ReturnsAsync((CartDto?)null);

        var controller = CreateController();
        var result = await controller.Index(default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<CartViewModel>().Subject;
        model.ApiAvailable.Should().BeFalse();
    }

    // ── Add ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_WithValidData_CallsAddItemAndRedirects()
    {
        var cart = BuildCartDto(1);
        _cartApi.Setup(a => a.AddItemAsync(SessionKey, 5, 2m, default)).ReturnsAsync(cart);

        var controller = CreateController();
        var result = await controller.Add(5, 2m, null, default);

        result.Should().BeOfType<RedirectToActionResult>()
              .Which.ActionName.Should().Be("Index");
        _cartApi.Verify(a => a.AddItemAsync(SessionKey, 5, 2m, default), Times.Once);
    }

    [Fact]
    public async Task Add_WithInvalidProductId_RedirectsWithoutCallingApi()
    {
        var controller = CreateController();
        var result = await controller.Add(0, 1m, null, default);

        result.Should().BeOfType<RedirectToActionResult>();
        _cartApi.Verify(a => a.AddItemAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Add_WithQuantityOver20_RedirectsWithoutCallingApi()
    {
        var controller = CreateController();
        var result = await controller.Add(1, 21m, null, default);

        result.Should().BeOfType<RedirectToActionResult>();
        _cartApi.Verify(a => a.AddItemAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Remove ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Remove_CallsRemoveItemAndRedirectsToIndex()
    {
        var cart = BuildCartDto(0);
        _cartApi.Setup(a => a.RemoveItemAsync(SessionKey, 3, default)).ReturnsAsync(cart);

        var controller = CreateController();
        var result = await controller.Remove(3, default);

        result.Should().BeOfType<RedirectToActionResult>()
              .Which.ActionName.Should().Be("Index");
        _cartApi.Verify(a => a.RemoveItemAsync(SessionKey, 3, default), Times.Once);
    }

    // ── Clear ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Clear_CallsClearCartAndRedirectsToIndex()
    {
        var cart = BuildCartDto(0);
        _cartApi.Setup(a => a.ClearCartAsync(SessionKey, default)).ReturnsAsync(cart);

        var controller = CreateController();
        var result = await controller.Clear(default);

        result.Should().BeOfType<RedirectToActionResult>()
              .Which.ActionName.Should().Be("Index");
        _cartApi.Verify(a => a.ClearCartAsync(SessionKey, default), Times.Once);
    }
}

// ─── Minimal ISession stub for testing ───────────────────────────────────────

/// <summary>
/// A simple in-memory ISession that pre-seeds a session key under the cart key name,
/// so the controller's GetOrCreateSessionKey() returns our known test value.
/// </summary>
internal class MockSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();
    private const string CartKeyName = "GOLISMart_CartSession";

    public MockSession(string preloadedCartKey)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(preloadedCartKey);
        _store[CartKeyName] = bytes;
    }

    public bool IsAvailable => true;
    public string Id => "test-session-id";
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);

    public void Set(string key, byte[] value) => _store[key] = value;

    public bool TryGetValue(string key, out byte[] value)
    {
        if (_store.TryGetValue(key, out var v))
        {
            value = v;
            return true;
        }
        value = [];
        return false;
    }
}
