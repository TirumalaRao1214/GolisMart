using System.Net.Http.Json;
using GOLISMart.Web.Services.Interfaces;

namespace GOLISMart.Web.Services.Implementations;

public class CartApiClient : ICartApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<CartApiClient> _logger;
    private const string SessionKeyHeader = "X-Session-Key";

    public CartApiClient(HttpClient http, ILogger<CartApiClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<CartDto?> GetCartAsync(string sessionKey, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "api/v1/cart");
            req.Headers.Add(SessionKeyHeader, sessionKey);
            var response = await _http.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CartDto>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cart");
            return null;
        }
    }

    public async Task<CartDto?> AddItemAsync(string sessionKey, int productId, decimal quantity, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/cart/items");
            req.Headers.Add(SessionKeyHeader, sessionKey);
            req.Content = JsonContent.Create(new AddToCartRequest(productId, quantity));
            var response = await _http.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CartDto>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add item {ProductId} to cart", productId);
            return null;
        }
    }

    public async Task<CartDto?> UpdateItemAsync(string sessionKey, int productId, decimal quantity, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, $"api/v1/cart/items/{productId}");
            req.Headers.Add(SessionKeyHeader, sessionKey);
            req.Content = JsonContent.Create(new UpdateCartItemRequest(quantity));
            var response = await _http.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CartDto>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update item {ProductId} in cart", productId);
            return null;
        }
    }

    public async Task<CartDto?> RemoveItemAsync(string sessionKey, int productId, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, $"api/v1/cart/items/{productId}");
            req.Headers.Add(SessionKeyHeader, sessionKey);
            var response = await _http.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CartDto>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove item {ProductId} from cart", productId);
            return null;
        }
    }

    public async Task<CartDto?> ClearCartAsync(string sessionKey, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, "api/v1/cart");
            req.Headers.Add(SessionKeyHeader, sessionKey);
            var response = await _http.SendAsync(req, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CartDto>(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear cart");
            return null;
        }
    }
}
