using FluentAssertions;
using GOLISMart.API.DTOs;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GOLISMart.API.Tests;

/// <summary>
/// Unit tests for CatalogService — no database required.
/// </summary>
public class CatalogServiceTests
{
    private readonly Mock<ICategoryRepository> _catRepo = new();
    private readonly Mock<IProductRepository>  _prodRepo = new();
    private readonly Mock<IInventoryRepository> _invRepo = new();

    private CatalogService CreateService() =>
        new(_catRepo.Object, _prodRepo.Object, _invRepo.Object,
            NullLogger<CatalogService>.Instance);

    // ── Categories ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveCategoriesAsync_ReturnsMappedDtos()
    {
        var categories = new List<(Category, int)>
        {
            (new Category { Id = 1, Name = "Fresh Veg", Slug = "fresh-veg", SortOrder = 1, IsActive = true }, 5),
            (new Category { Id = 2, Name = "Fruits",    Slug = "fruits",    SortOrder = 2, IsActive = true }, 3)
        };
        _catRepo.Setup(r => r.GetCategoriesWithProductCountAsync(default))
                .ReturnsAsync(categories);

        var service = CreateService();
        var result  = await service.GetActiveCategoriesAsync();

        result.Should().HaveCount(2);
        result[0].Slug.Should().Be("fresh-veg");
        result[0].ProductCount.Should().Be(5);
        result[1].ProductCount.Should().Be(3);
    }

    [Fact]
    public async Task GetCategoryBySlugAsync_ReturnsNull_WhenNotFound()
    {
        _catRepo.Setup(r => r.GetBySlugAsync("not-found", default))
                .ReturnsAsync((Category?)null);

        var service = CreateService();
        var result  = await service.GetCategoryBySlugAsync("not-found");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCategoryBySlugAsync_ReturnsMappedDto_WhenFound()
    {
        var cat = new Category { Id = 1, Name = "Spices", Slug = "spices", SortOrder = 7, IsActive = true };
        _catRepo.Setup(r => r.GetBySlugAsync("spices", default)).ReturnsAsync(cat);

        var service = CreateService();
        var result  = await service.GetCategoryBySlugAsync("spices");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Spices");
        result.Slug.Should().Be("spices");
    }

    // ── Products ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProductsAsync_ReturnsPaginatedDtos()
    {
        var products = new List<Product>
        {
            BuildProduct(1, "Tomato", "tomato", 35m, null, true),
            BuildProduct(2, "Onion",  "onion",  40m, 35m, false)
        };
        var paged = new PagedResult<Product>(products, 1, 20, 2, 1);
        var inv = new Dictionary<int, InventoryStatus>
        {
            [1] = new() { ProductId = 1, QuantityOnHand = 50, ReservedQuantity = 0, LowStockThreshold = 5 },
            [2] = new() { ProductId = 2, QuantityOnHand = 0,  ReservedQuantity = 0, LowStockThreshold = 5 }
        };

        _prodRepo.Setup(r => r.GetActiveProductsAsync(It.IsAny<ProductQueryParams>(), default))
                 .ReturnsAsync(paged);
        _invRepo.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<int>>(), default))
                .ReturnsAsync(inv);

        var service = CreateService();
        var result  = await service.GetProductsAsync(new ProductQueryParams());

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items[0].IsInStock.Should().BeTrue();
        result.Items[1].IsInStock.Should().BeFalse();
    }

    [Fact]
    public async Task GetProductBySlugAsync_ReturnsNull_WhenNotFound()
    {
        _prodRepo.Setup(r => r.GetBySlugAsync("missing", default))
                 .ReturnsAsync((Product?)null);

        var service = CreateService();
        var result  = await service.GetProductBySlugAsync("missing");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProductBySlugAsync_IncludesImages_WhenPresent()
    {
        var product = BuildProduct(1, "Basmati Rice", "basmati-rice", 399m, 349m, true);
        var images  = new List<ProductImage>
        {
            new() { Id = 1, ProductId = 1, ImagePath = "/img/rice.jpg", IsPrimary = true, SortOrder = 0 }
        };
        _prodRepo.Setup(r => r.GetBySlugAsync("basmati-rice", default)).ReturnsAsync(product);
        _prodRepo.Setup(r => r.GetImagesByProductIdAsync(1, default)).ReturnsAsync(images);
        _invRepo.Setup(r => r.GetByProductIdAsync(1, default))
                .ReturnsAsync(new InventoryStatus { ProductId = 1, QuantityOnHand = 40, ReservedQuantity = 0, LowStockThreshold = 5 });

        var service = CreateService();
        var result  = await service.GetProductBySlugAsync("basmati-rice");

        result.Should().NotBeNull();
        result!.Images.Should().HaveCount(1);
        result.Images[0].ImagePath.Should().Be("/img/rice.jpg");
        result.IsInStock.Should().BeTrue();
    }

    [Fact]
    public async Task GetFeaturedProductsAsync_ClampsCount()
    {
        _prodRepo.Setup(r => r.GetFeaturedProductsAsync(8, default))
                 .ReturnsAsync(new List<Product>());
        _invRepo.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<int>>(), default))
                .ReturnsAsync(new Dictionary<int, InventoryStatus>());

        var service = CreateService();
        var result  = await service.GetFeaturedProductsAsync(8);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRelatedProductsAsync_ReturnsEmpty_WhenProductNotFound()
    {
        _prodRepo.Setup(r => r.GetBySlugAsync("ghost", default))
                 .ReturnsAsync((Product?)null);

        var service = CreateService();
        var result  = await service.GetRelatedProductsAsync("ghost");

        result.Should().BeEmpty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Product BuildProduct(int id, string name, string slug, decimal price, decimal? offer, bool featured) =>
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
            IsFeatured   = featured,
            IsActive     = true
        };
}
