using System.Security.Claims;
using FluentAssertions;
using GOLISMart.Web.Controllers;
using GOLISMart.Web.Models;
using GOLISMart.Web.Services;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using System.Text.Json;

namespace GOLISMart.Web.Tests;

public class SpaAndAjaxControllerTests
{
    private const string TestSessionKey = "test-cart-session-123";

    private static ControllerContext CreateAjaxContext(string? sessionKey = null, string? token = null, Dictionary<string, string>? query = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        httpContext.Request.Headers["Accept"] = "application/json";

        if (!string.IsNullOrEmpty(token))
        {
            httpContext.Request.Headers["Cookie"] = $"GOLISMart_AuthToken={token}";
        }

        if (query != null)
        {
            var queryCollection = new QueryCollection(query.ToDictionary(k => k.Key, v => new Microsoft.Extensions.Primitives.StringValues(v.Value)));
            httpContext.Request.Query = queryCollection;
        }

        if (!string.IsNullOrEmpty(sessionKey))
        {
            httpContext.Session = new MockSession(sessionKey);
        }

        return new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task Catalog_Suggestions_ReturnsJsonResults()
    {
        // Arrange
        var mockApi = new Mock<ICatalogApiClient>();
        var fakeProducts = new List<ProductSummaryDto>
        {
            new(1, "SKU-001", "Fresh Milk", "fresh-milk", "1L", 60m, null, false, true, false, null, "Dairy", "dairy")
        };
        mockApi.Setup(a => a.GetProductsAsync(1, 6, "milk", null, "name", "asc", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ProductSummaryDto>(fakeProducts, 1, 6, 1, 1));

        var controller = new CatalogController(mockApi.Object, NullLogger<CatalogController>.Instance)
        {
            ControllerContext = CreateAjaxContext()
        };

        // Act
        var result = await controller.Suggestions("milk");

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Catalog_Index_PartialRequest_ReturnsPartialView()
    {
        // Arrange
        var mockApi = new Mock<ICatalogApiClient>();
        mockApi.Setup(a => a.GetProductsAsync(1, 20, null, null, "name", "asc", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ProductSummaryDto>(new List<ProductSummaryDto>(), 1, 20, 0, 0));
        mockApi.Setup(a => a.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CategoryDto>());

        var controller = new CatalogController(mockApi.Object, NullLogger<CatalogController>.Instance)
        {
            ControllerContext = CreateAjaxContext(query: new Dictionary<string, string> { { "partial", "true" } })
        };

        // Act
        var result = await controller.Index(search: null, category: null);

        // Assert
        var partialResult = result.Should().BeOfType<PartialViewResult>().Subject;
        partialResult.ViewName.Should().Be("_ProductGrid");
    }

    [Fact]
    public async Task Catalog_Index_LiveSearch_TrimsTermAndPassesFilters()
    {
        // Arrange
        var mockApi = new Mock<ICatalogApiClient>();
        var fakeProducts = new List<ProductSummaryDto>
        {
            new(1, "SKU-001", "Apple", "apple", "1 kg", 120m, null, false, true, false, null, "Fruits", "fresh-fruits")
        };
        mockApi.Setup(a => a.GetProductsAsync(1, 20, "app", "fresh-fruits", "name", "asc", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ProductSummaryDto>(fakeProducts, 1, 20, 1, 1));
        mockApi.Setup(a => a.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CategoryDto>());

        var controller = new CatalogController(mockApi.Object, NullLogger<CatalogController>.Instance)
        {
            ControllerContext = CreateAjaxContext(query: new Dictionary<string, string> { { "partial", "true" } })
        };

        // Act
        var result = await controller.Index(search: "   app  ", category: "fresh-fruits", inStock: true);

        // Assert
        var partial = result.Should().BeOfType<PartialViewResult>().Subject;
        var vm = partial.Model.Should().BeOfType<ProductListViewModel>().Subject;
        vm.SearchQuery.Should().Be("app");
        vm.Products!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Catalog_Index_PartialRequest_ApiFailure_Returns503_NotEmptyResults()
    {
        // Arrange — the API client returns null when the upstream call fails
        var mockApi = new Mock<ICatalogApiClient>();
        mockApi.Setup(a => a.GetProductsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedResult<ProductSummaryDto>?)null);
        mockApi.Setup(a => a.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CategoryDto>());

        var controller = new CatalogController(mockApi.Object, NullLogger<CatalogController>.Instance)
        {
            ControllerContext = CreateAjaxContext(query: new Dictionary<string, string> { { "partial", "true" } })
        };

        // Act
        var result = await controller.Index(search: "apple", category: null);

        // Assert
        result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task Cart_Add_AjaxRequest_ReturnsJsonResult()
    {
        // Arrange
        var mockApi = new Mock<ICartApiClient>();
        var cart = new CartDto(
            CartId: 1,
            SessionKey: TestSessionKey,
            Items: new List<CartItemDto>
            {
                new(1, "Tomato", "tomato", "1 kg", null, 2m, 30m, 30m, 60m, true, true)
            },
            SubTotal: 60m,
            TotalItems: 2);

        mockApi.Setup(a => a.AddItemAsync(TestSessionKey, 1, 2m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CartSettings:DeliveryFee"] = "40",
                ["CartSettings:FreeDeliveryAbove"] = "500"
            })
            .Build();

        var controller = new CartController(mockApi.Object, config, NullLogger<CartController>.Instance)
        {
            ControllerContext = CreateAjaxContext(sessionKey: TestSessionKey)
        };
        var tempDataProvider = new Mock<ITempDataProvider>();
        tempDataProvider.Setup(p => p.LoadTempData(It.IsAny<HttpContext>()))
            .Returns(new Dictionary<string, object?>());
        controller.TempData = new TempDataDictionary(controller.ControllerContext.HttpContext, tempDataProvider.Object);

        // Act
        var result = await controller.Add(1, 2m, null);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Cart_Update_AjaxRequest_ReturnsJsonResult()
    {
        // Arrange
        var mockApi = new Mock<ICartApiClient>();
        var cart = new CartDto(
            CartId: 1,
            SessionKey: TestSessionKey,
            Items: new List<CartItemDto>
            {
                new(1, "Tomato", "tomato", "1 kg", null, 3m, 30m, 30m, 90m, true, true)
            },
            SubTotal: 90m,
            TotalItems: 3);

        mockApi.Setup(a => a.UpdateItemAsync(TestSessionKey, 1, 3m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CartSettings:DeliveryFee"] = "40",
                ["CartSettings:FreeDeliveryAbove"] = "500"
            })
            .Build();

        var controller = new CartController(mockApi.Object, config, NullLogger<CartController>.Instance)
        {
            ControllerContext = CreateAjaxContext(sessionKey: TestSessionKey)
        };
        var tempDataProvider = new Mock<ITempDataProvider>();
        tempDataProvider.Setup(p => p.LoadTempData(It.IsAny<HttpContext>()))
            .Returns(new Dictionary<string, object?>());
        controller.TempData = new TempDataDictionary(controller.ControllerContext.HttpContext, tempDataProvider.Object);

        // Act
        var result = await controller.Update(1, 3m);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Delivery_Check_ReturnsServiceabilityJson()
    {
        // Arrange
        var mockApi = new Mock<IDeliveryApiClient>();
        var response = new ServiceabilityResponse(
            PinCode: "560001",
            IsServiceable: true,
            AreaName: "MG Road, Bengaluru",
            EstimatedDeliveryWindow: "2-3 hours",
            MinOrderValue: 99m,
            DeliveryFee: 40m,
            FreeDeliveryAbove: 499m,
            UnavailableReason: null);

        mockApi.Setup(a => a.CheckServiceabilityAsync("560001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var controller = new DeliveryController(mockApi.Object, NullLogger<DeliveryController>.Instance);

        // Act
        var result = await controller.CheckServiceability("560001");

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task Location_ReverseGeocode_ReturnsAddressFields()
    {
        // Arrange
        var mockLocationApi = new Mock<ILocationApiClient>();
        var fakeLocation = new ReverseGeocodeResponse(
            Success: true,
            Address: new ReverseGeocodeAddressDto(
                HouseNumber: "12",
                Street: "100 Feet Road",
                Locality: "Indiranagar",
                Landmark: "Metro Station",
                City: "Bengaluru",
                State: "Karnataka",
                PinCode: "560038",
                DisplayName: "Indiranagar, Bengaluru"),
            Error: null);

        mockLocationApi.Setup(a => a.ReverseGeocodeAsync(12.9716, 77.5946, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeLocation);

        var controller = new LocationController(mockLocationApi.Object, NullLogger<LocationController>.Instance);

        // Act
        var result = await controller.ReverseGeocode(12.9716, 77.5946);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(fakeLocation);
    }
    [Fact]
    public async Task Account_Login_Admin_RedirectsToAdminDashboard_InAjax()
    {
        // Arrange
        var mockAuthApi = new Mock<IAuthApiClient>();
        var authResponse = new AuthResponse("token-123", "user-admin", "admin@golismart.local", "Store Admin", DateTime.UtcNow.AddHours(8));

        mockAuthApi.Setup(a => a.LoginAsync(It.Is<LoginRequest>(r => r.Email == "admin@golismart.local"), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, null, authResponse));

        mockAuthApi.Setup(a => a.GetUserRolesAsync("token-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Admin" });

        var controller = new AccountController(mockAuthApi.Object, NullLogger<AccountController>.Instance)
        {
            ControllerContext = CreateAjaxContext()
        };

        var mockUrl = new Mock<IUrlHelper>();
        mockUrl.Setup(u => u.Action(It.Is<Microsoft.AspNetCore.Mvc.Routing.UrlActionContext>(c => c.Action == "Dashboard" && c.Controller == "AdminDashboard")))
            .Returns("/admin/dashboard");
        controller.Url = mockUrl.Object;

        var serviceProvider = new Mock<IServiceProvider>();
        var authServiceMock = new Mock<IAuthenticationService>();
        serviceProvider.Setup(s => s.GetService(typeof(IAuthenticationService)))
            .Returns(authServiceMock.Object);
        controller.ControllerContext.HttpContext.RequestServices = serviceProvider.Object;

        // Act
        var result = await controller.Login(new LoginViewModel
        {
            Email = "admin@golismart.local",
            Password = "Admin@123"
        });

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        json.Should().Contain("/admin/dashboard");
        json.Should().Contain("\"isAdmin\":true");
    }

    [Fact]
    public async Task Account_Login_Customer_RedirectsToHome_InAjax()
    {
        // Arrange
        var mockAuthApi = new Mock<IAuthApiClient>();
        var authResponse = new AuthResponse("token-456", "user-cust", "cust@example.com", "John Doe", DateTime.UtcNow.AddHours(8));

        mockAuthApi.Setup(a => a.LoginAsync(It.Is<LoginRequest>(r => r.Email == "cust@example.com"), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, null, authResponse));

        mockAuthApi.Setup(a => a.GetUserRolesAsync("token-456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var controller = new AccountController(mockAuthApi.Object, NullLogger<AccountController>.Instance)
        {
            ControllerContext = CreateAjaxContext()
        };

        var mockUrl = new Mock<IUrlHelper>();
        mockUrl.Setup(u => u.Action(It.Is<Microsoft.AspNetCore.Mvc.Routing.UrlActionContext>(c => c.Action == "Index" && c.Controller == "Home")))
            .Returns("/");
        controller.Url = mockUrl.Object;

        var serviceProvider = new Mock<IServiceProvider>();
        var authServiceMock = new Mock<IAuthenticationService>();
        serviceProvider.Setup(s => s.GetService(typeof(IAuthenticationService)))
            .Returns(authServiceMock.Object);
        controller.ControllerContext.HttpContext.RequestServices = serviceProvider.Object;

        // Act
        var result = await controller.Login(new LoginViewModel
        {
            Email = "cust@example.com",
            Password = "Password@123"
        });

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var json = JsonSerializer.Serialize(okResult.Value);
        json.Should().Contain("\"isAdmin\":false");
    }

    // ── Razorpay proxy endpoint unit tests ────────────────────────────────────

    private static CheckoutController CreateCheckoutController(
        Mock<IPaymentsApiClient> paymentsApiMock,
        string? token = "test-jwt-token",
        string? sessionKey = "cart-session-abc",
        int selectedAddressId = 5)
    {
        var checkoutApiMock = new Mock<ICheckoutApiClient>();
        var authApiMock     = new Mock<IAuthApiClient>();
        var cartApiMock     = new Mock<ICartApiClient>();
        var orderApiMock    = new Mock<IOrderApiClient>();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiSettings:BaseUrl"] = "http://localhost:5000"
            }).Build();

        var httpContext = new DefaultHttpContext();

        // Set the auth token cookie if provided
        if (!string.IsNullOrEmpty(token))
            httpContext.Request.Headers["Cookie"] = $"GOLISMart_AuthToken={token}";

        // Set up server-side session with cart session key and address
        var mockSession = new MockSessionFull(sessionKey, selectedAddressId);
        httpContext.Session = mockSession;

        var controller = new CheckoutController(
            checkoutApiMock.Object,
            authApiMock.Object,
            cartApiMock.Object,
            orderApiMock.Object,
            paymentsApiMock.Object,
            config,
            NullLogger<CheckoutController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        var tempDataProvider = new Mock<ITempDataProvider>();
        tempDataProvider.Setup(p => p.LoadTempData(It.IsAny<HttpContext>()))
            .Returns(new Dictionary<string, object?>());
        controller.TempData = new TempDataDictionary(httpContext, tempDataProvider.Object);

        return controller;
    }

    [Fact]
    public async Task RazorpayCreateOrder_NoToken_Returns401()
    {
        // Arrange — no auth cookie
        var paymentsApiMock = new Mock<IPaymentsApiClient>();
        var controller = CreateCheckoutController(paymentsApiMock, token: null);

        // Act
        var result = await controller.RazorpayCreateOrder(new RazorpayCreateOrderBody(null));

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        paymentsApiMock.Verify(p => p.CreateRazorpayOrderAsync(
            It.IsAny<string>(), It.IsAny<CreateRazorpayOrderRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RazorpayCreateOrder_NoSessionKey_Returns400()
    {
        // Arrange — valid auth token, but no cart session key in server-side Session
        var paymentsApiMock = new Mock<IPaymentsApiClient>();
        var controller = CreateCheckoutController(paymentsApiMock, sessionKey: null);

        // Act
        var result = await controller.RazorpayCreateOrder(new RazorpayCreateOrderBody(null));

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        paymentsApiMock.Verify(p => p.CreateRazorpayOrderAsync(
            It.IsAny<string>(), It.IsAny<CreateRazorpayOrderRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RazorpayCreateOrder_NoAddressInSession_Returns400()
    {
        // Arrange — valid session key, but no address ID stored
        var paymentsApiMock = new Mock<IPaymentsApiClient>();
        var controller = CreateCheckoutController(paymentsApiMock, selectedAddressId: 0);

        // Act
        var result = await controller.RazorpayCreateOrder(new RazorpayCreateOrderBody(null));

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        paymentsApiMock.Verify(p => p.CreateRazorpayOrderAsync(
            It.IsAny<string>(), It.IsAny<CreateRazorpayOrderRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RazorpayCreateOrder_ApiReturnsOrder_Returns200WithGatewayDetails()
    {
        // Arrange
        var paymentsApiMock = new Mock<IPaymentsApiClient>();
        var expectedOrder = new CreateRazorpayOrderResponse(
            InternalOrderId: 99,
            InternalOrderNumber: "GM-20250101-ABCD1234",
            RazorpayOrderId: "order_TestXXXXXXX",
            AmountPaise: 59500L,
            Currency: "INR",
            KeyId: "rzp_test_KEYID",
            CustomerName: "Test User",
            CustomerEmail: "test@example.com",
            CustomerPhone: "+919876543210");

        paymentsApiMock.Setup(p => p.CreateRazorpayOrderAsync(
                "test-jwt-token",
                It.Is<CreateRazorpayOrderRequest>(r => r.SessionKey == "cart-session-abc" && r.AddressId == 5),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrder);

        var controller = CreateCheckoutController(paymentsApiMock);

        // Act
        var result = await controller.RazorpayCreateOrder(new RazorpayCreateOrderBody(null));

        // Assert — OkObjectResult is a subtype of ObjectResult
        var ok = result.Should().BeAssignableTo<ObjectResult>().Subject;
        ok.StatusCode.Should().Be(200);
        ok.Value.Should().BeEquivalentTo(expectedOrder);
    }

    [Fact]
    public async Task RazorpayCreateOrder_GatewayUnavailable_Returns503()
    {
        // Arrange — API client throws HttpRequestException (gateway error / API 500 or 503)
        var paymentsApiMock = new Mock<IPaymentsApiClient>();
        paymentsApiMock.Setup(p => p.CreateRazorpayOrderAsync(
                It.IsAny<string>(), It.IsAny<CreateRazorpayOrderRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Payment service is currently unavailable."));

        var controller = CreateCheckoutController(paymentsApiMock);

        // Act
        var result = await controller.RazorpayCreateOrder(new RazorpayCreateOrderBody(null));

        // Assert
        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task RazorpayCreateOrder_BusinessRuleViolation_Returns422()
    {
        // Arrange — service throws InvalidOperationException (e.g., out of stock)
        var paymentsApiMock = new Mock<IPaymentsApiClient>();
        paymentsApiMock.Setup(p => p.CreateRazorpayOrderAsync(
                It.IsAny<string>(), It.IsAny<CreateRazorpayOrderRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Item is out of stock."));

        var controller = CreateCheckoutController(paymentsApiMock);

        // Act
        var result = await controller.RazorpayCreateOrder(new RazorpayCreateOrderBody(null));

        // Assert — UnprocessableEntityObjectResult is a subtype of ObjectResult
        var obj = result.Should().BeAssignableTo<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(422);
        var json = JsonSerializer.Serialize(obj.Value);
        json.Should().Contain("out of stock");
    }

    [Fact]
    public async Task RazorpayVerify_NoToken_Returns401()
    {
        // Arrange
        var paymentsApiMock = new Mock<IPaymentsApiClient>();
        var controller = CreateCheckoutController(paymentsApiMock, token: null);

        // Act
        var result = await controller.RazorpayVerify(new RazorpayVerifyBody("order_X", "pay_X", "sig_X"));

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        paymentsApiMock.Verify(p => p.VerifyRazorpayPaymentAsync(
            It.IsAny<string>(), It.IsAny<VerifyRazorpayPaymentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RazorpayVerify_ValidSignature_Returns200WithSuccess()
    {
        // Arrange
        var paymentsApiMock = new Mock<IPaymentsApiClient>();
        var expected = new VerifyRazorpayPaymentResponse(
            Success: true,
            Message: "Payment verified successfully.",
            InternalOrderId: 99,
            OrderNumber: "GM-20250101-ABCD1234",
            OrderStatus: "Pending",
            PaymentStatus: "Paid");

        paymentsApiMock.Setup(p => p.VerifyRazorpayPaymentAsync(
                "test-jwt-token",
                It.Is<VerifyRazorpayPaymentRequest>(r =>
                    r.RazorpayOrderId   == "order_Test" &&
                    r.RazorpayPaymentId == "pay_Test"   &&
                    r.RazorpaySignature == "validSig"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateCheckoutController(paymentsApiMock);

        // Act
        var result = await controller.RazorpayVerify(
            new RazorpayVerifyBody("order_Test", "pay_Test", "validSig"));

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task RazorpayVerify_InvalidSignature_Returns200WithFailure()
    {
        // Arrange — server verifies signature and returns success:false
        var paymentsApiMock = new Mock<IPaymentsApiClient>();
        var expected = new VerifyRazorpayPaymentResponse(
            Success: false,
            Message: "Payment signature verification failed.",
            InternalOrderId: null,
            OrderNumber: null,
            OrderStatus: null,
            PaymentStatus: null);

        paymentsApiMock.Setup(p => p.VerifyRazorpayPaymentAsync(
                It.IsAny<string>(), It.IsAny<VerifyRazorpayPaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateCheckoutController(paymentsApiMock);

        // Act
        var result = await controller.RazorpayVerify(
            new RazorpayVerifyBody("order_X", "pay_X", "bad_signature"));

        // Assert
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = (VerifyRazorpayPaymentResponse)ok.Value!;
        body.Success.Should().BeFalse();
        body.Message.Should().Contain("signature");
    }

    [Fact]
    public async Task RazorpayVerify_MissingFields_Returns400()
    {
        // Arrange
        var paymentsApiMock = new Mock<IPaymentsApiClient>();
        var controller = CreateCheckoutController(paymentsApiMock);

        // Act — empty strings are invalid
        var result = await controller.RazorpayVerify(
            new RazorpayVerifyBody("", "pay_X", "sig_X"));

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        paymentsApiMock.Verify(p => p.VerifyRazorpayPaymentAsync(
            It.IsAny<string>(), It.IsAny<VerifyRazorpayPaymentRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

/// <summary>
/// Extended MockSession that supports both the cart session key and an integer address ID.
/// Used by the Razorpay proxy endpoint unit tests.
/// </summary>
internal class MockSessionFull : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();

    public MockSessionFull(string? cartSessionKey, int addressId)
    {
        if (!string.IsNullOrEmpty(cartSessionKey))
            _store["GOLISMart_CartSession"] = System.Text.Encoding.UTF8.GetBytes(cartSessionKey);

        if (addressId > 0)
        {
            // ASP.NET Core SessionExtensions.SetInt32 uses big-endian (BinaryPrimitives.WriteInt32BigEndian)
            var bytes = new byte[4];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes, addressId);
            _store["Checkout_SelectedAddressId"] = bytes;
        }
    }

    public bool IsAvailable => true;
    public string Id => "test-session-full";
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;

    public bool TryGetValue(string key, out byte[] value)
    {
        if (_store.TryGetValue(key, out var v)) { value = v; return true; }
        value = [];
        return false;
    }
}
