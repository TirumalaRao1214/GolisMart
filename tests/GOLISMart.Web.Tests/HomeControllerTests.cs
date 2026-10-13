using FluentAssertions;
using GOLISMart.Web.Controllers;
using GOLISMart.Web.Models;
using GOLISMart.Web.Services;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GOLISMart.Web.Tests;

/// <summary>
/// Unit tests for HomeController — no HTTP calls, no database.
/// </summary>
public class HomeControllerTests
{
    private readonly Mock<ICatalogApiClient> _api = new();

    private HomeController CreateController() =>
        new(_api.Object, NullLogger<HomeController>.Instance);

    [Fact]
    public async Task Index_ApiAvailable_ReturnsViewWithModel()
    {
        SetupSuccessfulApiCalls();

        var controller = CreateController();
        var result = await controller.Index(default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<HomeViewModel>().Subject;
        model.ApiAvailable.Should().BeTrue();
        model.Categories.Should().HaveCount(2);
        model.FeaturedProducts.Should().HaveCount(1);
    }

    [Fact]
    public async Task Index_ApiThrows_ReturnsViewWithApiUnavailable()
    {
        _api.Setup(a => a.GetCategoriesAsync(default))
            .ThrowsAsync(new HttpRequestException("connection refused"));

        var controller = CreateController();
        var result = await controller.Index(default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<HomeViewModel>().Subject;
        model.ApiAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Index_EmptyCollections_ReturnsSafeViewModel()
    {
        _api.Setup(a => a.GetCategoriesAsync(default))
            .ReturnsAsync(new List<CategoryDto>());
        _api.Setup(a => a.GetFeaturedProductsAsync(8, default))
            .ReturnsAsync(new List<ProductSummaryDto>());
        _api.Setup(a => a.GetProductsByCategorySlugAsync(It.IsAny<string>(), It.IsAny<int>(), default))
            .ReturnsAsync(new List<ProductSummaryDto>());

        var controller = CreateController();
        var result = await controller.Index(default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<HomeViewModel>().Subject;
        model.ApiAvailable.Should().BeTrue();
        model.Categories.Should().BeEmpty();
        model.FeaturedProducts.Should().BeEmpty();
    }

    private void SetupSuccessfulApiCalls()
    {
        var cats = new List<CategoryDto>
        {
            new(1, "Vegetables", "vegetables", null, null, 1, 5),
            new(2, "Fruits",     "fruits",     null, null, 2, 3)
        };
        var featured = new List<ProductSummaryDto>
        {
            new(1, "SKU-001", "Tomato", "tomato", "1 kg", 35m, null, true, true, false, null, "Vegetables", "vegetables")
        };

        _api.Setup(a => a.GetCategoriesAsync(default)).ReturnsAsync(cats);
        _api.Setup(a => a.GetFeaturedProductsAsync(8, default)).ReturnsAsync(featured);
        _api.Setup(a => a.GetProductsByCategorySlugAsync("fresh-vegetables", 8, default))
            .ReturnsAsync(featured);
        _api.Setup(a => a.GetProductsByCategorySlugAsync("fresh-fruits", 8, default))
            .ReturnsAsync(new List<ProductSummaryDto>());
        _api.Setup(a => a.GetProductsByCategorySlugAsync("dairy-and-eggs", 6, default))
            .ReturnsAsync(new List<ProductSummaryDto>());
    }
}
