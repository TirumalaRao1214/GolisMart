using GOLISMart.API.DTOs;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Interfaces;

namespace GOLISMart.API.Services.Implementations;

public class CatalogService : ICatalogService
{
    private readonly ICategoryRepository _categoryRepo;
    private readonly IProductRepository _productRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly ILogger<CatalogService> _logger;

    public CatalogService(
        ICategoryRepository categoryRepo,
        IProductRepository productRepo,
        IInventoryRepository inventoryRepo,
        ILogger<CatalogService> logger)
    {
        _categoryRepo  = categoryRepo;
        _productRepo   = productRepo;
        _inventoryRepo = inventoryRepo;
        _logger        = logger;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetActiveCategoriesAsync(CancellationToken ct = default)
    {
        var rows = await _categoryRepo.GetCategoriesWithProductCountAsync(ct);
        return rows.Select(r => MapCategory(r.Category, r.ProductCount)).ToList();
    }

    public async Task<CategoryDto?> GetCategoryBySlugAsync(string slug, CancellationToken ct = default)
    {
        var category = await _categoryRepo.GetBySlugAsync(slug, ct);
        if (category is null) return null;
        return MapCategory(category, 0);
    }

    public async Task<PagedResult<ProductSummaryDto>> GetProductsAsync(ProductQueryParams query, CancellationToken ct = default)
    {
        var paged = await _productRepo.GetActiveProductsAsync(query, ct);

        // Bulk-fetch inventory for the current page
        var productIds = paged.Items.Select(p => p.Id).ToList();
        var inventoryMap = await _inventoryRepo.GetByProductIdsAsync(productIds, ct);

        var dtos = paged.Items
            .Select(p => MapProductSummary(p, inventoryMap.GetValueOrDefault(p.Id)))
            .ToList();

        return new PagedResult<ProductSummaryDto>(dtos, paged.Page, paged.PageSize, paged.TotalCount, paged.TotalPages);
    }

    public async Task<ProductDetailDto?> GetProductBySlugAsync(string slug, CancellationToken ct = default)
    {
        var product = await _productRepo.GetBySlugAsync(slug, ct);
        if (product is null) return null;

        var inventory = await _inventoryRepo.GetByProductIdAsync(product.Id, ct);
        var images    = await _productRepo.GetImagesByProductIdAsync(product.Id, ct);

        // If no images in ProductImages table, fall back to main ImagePath
        List<ProductImageDto> imageDtos;
        if (images.Count > 0)
        {
            imageDtos = images.Select(i => new ProductImageDto(i.Id, i.ImagePath, i.AltText, i.SortOrder, i.IsPrimary)).ToList();
        }
        else if (!string.IsNullOrEmpty(product.ImagePath))
        {
            imageDtos = [new ProductImageDto(0, product.ImagePath, product.Name, 0, true)];
        }
        else
        {
            imageDtos = [];
        }

        var available = inventory?.AvailableQuantity ?? 0m;
        var inStock   = available > 0;
        var lowStock  = inStock && inventory?.IsLowStock == true;

        return new ProductDetailDto(
            product.Id, product.SKU, product.Name, product.Slug, product.Description,
            product.UnitCode, product.UnitLabel, product.Price, product.OfferPrice,
            product.IsFeatured, inStock, lowStock, available,
            product.ImagePath, product.CategoryId, product.CategoryName, product.CategorySlug,
            imageDtos);
    }

    public async Task<IReadOnlyList<ProductSummaryDto>> GetFeaturedProductsAsync(int maxCount = 8, CancellationToken ct = default)
    {
        var products = await _productRepo.GetFeaturedProductsAsync(maxCount, ct);
        var ids = products.Select(p => p.Id).ToList();
        var inventoryMap = await _inventoryRepo.GetByProductIdsAsync(ids, ct);
        return products.Select(p => MapProductSummary(p, inventoryMap.GetValueOrDefault(p.Id))).ToList();
    }

    public async Task<IReadOnlyList<ProductSummaryDto>> GetRelatedProductsAsync(string slug, int maxCount = 6, CancellationToken ct = default)
    {
        var product = await _productRepo.GetBySlugAsync(slug, ct);
        if (product is null) return [];

        var related = await _productRepo.GetRelatedProductsAsync(product.Id, product.CategoryId, maxCount, ct);
        var ids = related.Select(p => p.Id).ToList();
        var inventoryMap = await _inventoryRepo.GetByProductIdsAsync(ids, ct);
        return related.Select(p => MapProductSummary(p, inventoryMap.GetValueOrDefault(p.Id))).ToList();
    }

    public async Task<IReadOnlyList<ProductSummaryDto>> GetProductsByCategoryAsync(string categorySlug, int maxCount = 50, CancellationToken ct = default)
    {
        var category = await _categoryRepo.GetBySlugAsync(categorySlug, ct);
        if (category is null) return [];

        var products = await _productRepo.GetByCategoryIdAsync(category.Id, maxCount, ct);
        var ids = products.Select(p => p.Id).ToList();
        var inventoryMap = await _inventoryRepo.GetByProductIdsAsync(ids, ct);
        return products.Select(p => MapProductSummary(p, inventoryMap.GetValueOrDefault(p.Id))).ToList();
    }

    // -----------------------------------------------------------------------
    // Mapping helpers
    // -----------------------------------------------------------------------

    private static CategoryDto MapCategory(Category c, int productCount) =>
        new(c.Id, c.Name, c.Slug, c.Description, c.ImagePath, c.SortOrder, productCount);

    private static ProductSummaryDto MapProductSummary(Product p, InventoryStatus? inv) =>
        new(p.Id, p.SKU, p.Name, p.Slug, p.UnitLabel, p.Price, p.OfferPrice,
            p.IsFeatured,
            IsInStock: inv?.IsInStock ?? false,
            IsLowStock: inv?.IsLowStock ?? false,
            p.ImagePath, p.CategoryName, p.CategorySlug);
}
