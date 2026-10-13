using GOLISMart.API.DTOs;
using GOLISMart.API.Models;

namespace GOLISMart.API.Repositories.Interfaces;

public interface IProductRepository
{
    Task<PagedResult<Product>> GetActiveProductsAsync(ProductQueryParams query, CancellationToken ct = default);
    Task<Product?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Product?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<ProductImage>> GetImagesByProductIdAsync(int productId, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetFeaturedProductsAsync(int maxCount = 8, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetRelatedProductsAsync(int productId, int categoryId, int maxCount = 6, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetByCategoryIdAsync(int categoryId, int maxCount = 50, CancellationToken ct = default);
}
