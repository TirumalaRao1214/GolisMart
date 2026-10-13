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
/// Unit tests for ProductController — no HTTP calls, no database.
/// </summary>
public class ProductControllerTests
{
    private readonly Mock<ICatalogApiClient> _api = new();

    private ProductController CreateController() =>
        new(_api.Object, NullLogger<ProductController>.Instance);

    [Fact]
    public async Task Detail_ReturnsProduct_WhenFound()
    {
        var product = new ProductDetailDto(
            1, "SKU-001", "Fresh Tomatoes", "fresh-tomatoes",
            "Great tomatoes.", "kg", "1 kg",
            35m, null, false, true, false, 50m,
            "/images/tomato.jpg", 1, "Fresh Vegetables", "fresh-vegetables",
            new List<ProductImageDto>());

        _api.Setup(a => a.GetProductBySlugAsync("fresh-tomatoes", default))
            .ReturnsAsync(product);
        _api.Setup(a => a.GetRelatedProductsAsync("fresh-tomatoes", 6, default))
            .ReturnsAsync(new List<ProductSummaryDto>());

        var controller = CreateController();
        var result = await controller.Detail("fresh-tomatoes", default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<ProductDetailViewModel>().Subject;
        model.Product.Should().NotBeNull();
        model.Product!.Name.Should().Be("Fresh Tomatoes");
        model.NotFound.Should().BeFalse();
        model.ApiAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Detail_ReturnsNotFound_WhenProductMissing()
    {
        _api.Setup(a => a.GetProductBySlugAsync("ghost-product", default))
            .ReturnsAsync((ProductDetailDto?)null);
        _api.Setup(a => a.GetRelatedProductsAsync("ghost-product", 6, default))
            .ReturnsAsync(new List<ProductSummaryDto>());

        var controller = CreateController();
        var result = await controller.Detail("ghost-product", default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<ProductDetailViewModel>().Subject;
        model.NotFound.Should().BeTrue();
    }

    [Fact]
    public async Task Detail_ApiThrows_ReturnsApiUnavailableModel()
    {
        _api.Setup(a => a.GetProductBySlugAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new HttpRequestException("timeout"));

        var controller = CreateController();
        var result = await controller.Detail("any-slug", default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<ProductDetailViewModel>().Subject;
        model.ApiAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Detail_RelatedProducts_AreIncluded()
    {
        var product = new ProductDetailDto(
            1, "SKU-001", "Onion", "onion", null, "kg", "1 kg",
            40m, 35m, false, true, false, 80m, null,
            1, "Fresh Vegetables", "fresh-vegetables",
            new List<ProductImageDto>());

        var related = new List<ProductSummaryDto>
        {
            new(2, "SKU-002", "Tomato", "tomato", "1 kg", 35m, null, false, true, false, null, "Fresh Vegetables", "fresh-vegetables")
        };

        _api.Setup(a => a.GetProductBySlugAsync("onion", default)).ReturnsAsync(product);
        _api.Setup(a => a.GetRelatedProductsAsync("onion", 6, default)).ReturnsAsync(related);

        var controller = CreateController();
        var result = await controller.Detail("onion", default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<ProductDetailViewModel>().Subject;
        model.RelatedProducts.Should().HaveCount(1);
        model.RelatedProducts[0].Name.Should().Be("Tomato");
    }
}
