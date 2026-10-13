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
        var json = System.Text.Json.JsonSerializer.Serialize(okResult.Value);
        json.Should().Contain("\"isAdmin\":false");
    }
}
