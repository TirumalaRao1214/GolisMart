using GOLISMart.API.Models;

namespace GOLISMart.API.Repositories.Interfaces;

public interface IInventoryRepository
{
    Task<InventoryStatus?> GetByProductIdAsync(int productId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<int, InventoryStatus>> GetByProductIdsAsync(IEnumerable<int> productIds, CancellationToken ct = default);
    Task<IReadOnlyList<InventoryStatus>> GetLowStockItemsAsync(CancellationToken ct = default);
}
