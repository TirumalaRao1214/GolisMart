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
/// Unit tests for CatalogController — no HTTP calls, no database.
/// </summary>
public class CatalogControllerTests
{
    private readonly Mock<ICatalogApiClient> _api = new();

    private CatalogController CreateController() =>
        new(_api.Object, NullLogger<CatalogController>.Instance);

    [Fact]
    public async Task Index_ReturnsViewWithProducts()
    {
        var paged = new PagedResult<ProductSummaryDto>(
            new List<ProductSummaryDto>
            {
                new(1, "SKU-001", "Tomato", "tomato", "1 kg", 35m, null, false, true, false, null, "Vegetables", "vegetables")
            }, 1, 20, 1, 1);

        _api.Setup(a => a.GetCategoriesAsync(default))
            .ReturnsAsync(new List<CategoryDto>());
        _api.Setup(a => a.GetProductsAsync(1, 20, null, null, "name", "asc", null, default))
            .ReturnsAsync(paged);

        var controller = CreateController();
        var result = await controller.Index(null, null, "name", "asc", 1, null, default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<ProductListViewModel>().Subject;
        model.Products!.Items.Should().HaveCount(1);
        model.ApiAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Index_SearchQuery_PreservesSearchTerm()
    {
        var paged = new PagedResult<ProductSummaryDto>(new List<ProductSummaryDto>(), 1, 20, 0, 0);
        _api.Setup(a => a.GetCategoriesAsync(default))
            .ReturnsAsync(new List<CategoryDto>());
        _api.Setup(a => a.GetProductsAsync(1, 20, "dal", null, "name", "asc", null, default))
            .ReturnsAsync(paged);

        var controller = CreateController();
        var result = await controller.Index("dal", null, "name", "asc", 1, null, default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<ProductListViewModel>().Subject;
        model.SearchQuery.Should().Be("dal");
    }

    [Fact]
    public async Task Index_CategoryFilter_PreservesSelectedCategory()
    {
        var paged = new PagedResult<ProductSummaryDto>(new List<ProductSummaryDto>(), 1, 20, 0, 0);
        _api.Setup(a => a.GetCategoriesAsync(default))
            .ReturnsAsync(new List<CategoryDto>());
        _api.Setup(a => a.GetProductsAsync(1, 20, null, "dal-and-pulses", "name", "asc", null, default))
            .ReturnsAsync(paged);

        var controller = CreateController();
        var result = await controller.Index(null, "dal-and-pulses", "name", "asc", 1, null, default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<ProductListViewModel>().Subject;
        model.SelectedCategory.Should().Be("dal-and-pulses");
    }

    [Fact]
    public async Task Index_ApiThrows_ReturnsApiUnavailableModel()
    {
        _api.Setup(a => a.GetCategoriesAsync(default))
            .ThrowsAsync(new HttpRequestException("timeout"));

        var controller = CreateController();
        var result = await controller.Index(null, null, "name", "asc", 1, null, default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<ProductListViewModel>().Subject;
        model.ApiAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Search_EmptyQuery_ReturnsEmptyResults()
    {
        var controller = CreateController();
        var result = await controller.Search(null, 1, default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<SearchViewModel>().Subject;
        model.Results.Should().BeNull();
        model.Query.Should().BeNull();
    }

    [Fact]
    public async Task Search_WithQuery_CallsApi()
    {
        var paged = new PagedResult<ProductSummaryDto>(new List<ProductSummaryDto>(), 1, 20, 0, 0);
        _api.Setup(a => a.GetProductsAsync(1, 20, "rice", null, "name", "asc", null, default))
            .ReturnsAsync(paged);

        var controller = CreateController();
        var result = await controller.Search("rice", 1, default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<SearchViewModel>().Subject;
        model.Query.Should().Be("rice");
        model.Results.Should().NotBeNull();
        _api.Verify(a => a.GetProductsAsync(1, 20, "rice", null, "name", "asc", null, default), Times.Once);
    }
}
