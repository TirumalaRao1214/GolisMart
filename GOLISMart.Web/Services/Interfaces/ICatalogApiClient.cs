namespace GOLISMart.Web.Services.Interfaces;

public interface ICatalogApiClient
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct = default);
    Task<CategoryDto?> GetCategoryBySlugAsync(string slug, CancellationToken ct = default);
    Task<PagedResult<ProductSummaryDto>?> GetProductsAsync(
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? category = null,
        string? sortBy = "name",
        string? sortOrder = "asc",
        bool? inStock = null,
        CancellationToken ct = default);
    Task<ProductDetailDto?> GetProductBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<ProductSummaryDto>> GetFeaturedProductsAsync(int max = 8, CancellationToken ct = default);
    Task<IReadOnlyList<ProductSummaryDto>> GetRelatedProductsAsync(string slug, int max = 6, CancellationToken ct = default);
    Task<IReadOnlyList<ProductSummaryDto>> GetProductsByCategorySlugAsync(string categorySlug, int max = 8, CancellationToken ct = default);
}
