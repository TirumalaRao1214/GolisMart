using GOLISMart.API.Models;

namespace GOLISMart.API.Repositories.Interfaces;

public interface ICartRepository
{
    Task<Cart?> GetBySessionKeyAsync(string sessionKey, CancellationToken ct = default);
    Task<Cart> GetOrCreateBySessionKeyAsync(string sessionKey, CancellationToken ct = default);
    Task<IReadOnlyList<CartItem>> GetItemsAsync(int cartId, CancellationToken ct = default);
    Task UpsertItemAsync(int cartId, int productId, decimal quantity, decimal unitPrice, CancellationToken ct = default);
    Task UpdateItemQuantityAsync(int cartId, int productId, decimal quantity, CancellationToken ct = default);
    Task<bool> RemoveItemAsync(int cartId, int productId, CancellationToken ct = default);
    Task ClearCartAsync(int cartId, CancellationToken ct = default);
    Task TouchCartAsync(int cartId, CancellationToken ct = default);
}
