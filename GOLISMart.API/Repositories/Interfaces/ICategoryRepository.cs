using GOLISMart.API.DTOs;
using GOLISMart.API.Models;

namespace GOLISMart.API.Repositories.Interfaces;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetActiveCategoriesAsync(CancellationToken ct = default);
    Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Category?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<(Category Category, int ProductCount)>> GetCategoriesWithProductCountAsync(CancellationToken ct = default);
}
