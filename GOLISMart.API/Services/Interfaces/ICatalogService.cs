using GOLISMart.API.DTOs;

namespace GOLISMart.API.Services.Interfaces;

public interface ICatalogService
{
    Task<IReadOnlyList<CategoryDto>> GetActiveCategoriesAsync(CancellationToken ct = default);
    Task<CategoryDto?> GetCategoryBySlugAsync(string slug, CancellationToken ct = default);
    Task<PagedResult<ProductSummaryDto>> GetProductsAsync(ProductQueryParams query, CancellationToken ct = default);
    Task<ProductDetailDto?> GetProductBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<ProductSummaryDto>> GetFeaturedProductsAsync(int maxCount = 8, CancellationToken ct = default);
    Task<IReadOnlyList<ProductSummaryDto>> GetRelatedProductsAsync(string slug, int maxCount = 6, CancellationToken ct = default);
    Task<IReadOnlyList<ProductSummaryDto>> GetProductsByCategoryAsync(string categorySlug, int maxCount = 50, CancellationToken ct = default);
}
