namespace GOLISMart.Web.Services.Interfaces;

public interface ICartApiClient
{
    Task<CartDto?> GetCartAsync(string sessionKey, CancellationToken ct = default);
    Task<CartDto?> AddItemAsync(string sessionKey, int productId, decimal quantity, CancellationToken ct = default);
    Task<CartDto?> UpdateItemAsync(string sessionKey, int productId, decimal quantity, CancellationToken ct = default);
    Task<CartDto?> RemoveItemAsync(string sessionKey, int productId, CancellationToken ct = default);
    Task<CartDto?> ClearCartAsync(string sessionKey, CancellationToken ct = default);
}
