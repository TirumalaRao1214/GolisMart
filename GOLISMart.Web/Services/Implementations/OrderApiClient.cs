using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GOLISMart.Web.Services.Interfaces;

namespace GOLISMart.Web.Services.Implementations;

public class OrderApiClient : IOrderApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<OrderApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OrderApiClient(HttpClient http, ILogger<OrderApiClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<PlaceOrderResponse?> PlaceOrderAsync(
        string token,
        PlaceOrderRequest request,
        CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/orders");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent.Create(request);

            var response = await _http.SendAsync(req, ct);

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity ||
                response.StatusCode == HttpStatusCode.BadRequest)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogInformation("PlaceOrder returned {Status}: {Body}",
                    (int)response.StatusCode, body.Length > 500 ? body[..500] : body);
                // Re-throw the error message to the controller
                throw new InvalidOperationException(ExtractErrorMessage(body));
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("PlaceOrder returned {Status}: {Body}",
                    (int)response.StatusCode, body.Length > 500 ? body[..500] : body);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<PlaceOrderResponse>(JsonOptions, ct);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "PlaceOrder request timed out");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PlaceOrder request failed");
            return null;
        }
    }

    public async Task<OrdersPagedResponse?> GetMyOrdersAsync(
        string token,
        int page = 1,
        int pageSize = 10,
        string? status = null,
        DateTime? dateFrom = null,
        DateTime? dateTo   = null,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"api/v1/orders?page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(status))
                url += $"&status={Uri.EscapeDataString(status)}";
            if (dateFrom.HasValue)
                url += $"&dateFrom={Uri.EscapeDataString(dateFrom.Value.ToString("yyyy-MM-dd"))}";
            if (dateTo.HasValue)
                url += $"&dateTo={Uri.EscapeDataString(dateTo.Value.ToString("yyyy-MM-dd"))}";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<OrdersPagedResponse>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMyOrders request failed");
            return null;
        }
    }

    public async Task<OrderDetailDto?> GetOrderDetailAsync(
        string token,
        int orderId,
        CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"api/v1/orders/{orderId}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _http.SendAsync(req, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadFromJsonAsync<OrderDetailDto>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetOrderDetail request failed for orderId={OrderId}", orderId);
            return null;
        }
    }

    public async Task<CancelOrderResponse?> CancelOrderAsync(
        string token,
        int orderId,
        CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"api/v1/orders/{orderId}/cancel");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent.Create(new { });  // empty body

            var response = await _http.SendAsync(req, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                return new CancelOrderResponse(false, ExtractErrorMessage(body), null);
            }

            return await response.Content.ReadFromJsonAsync<CancelOrderResponse>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CancelOrder request failed for orderId={OrderId}", orderId);
            return new CancelOrderResponse(false, "An unexpected error occurred. Please try again.", null);
        }
    }

    private static string ExtractErrorMessage(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("error", out var err))
                return err.GetString() ?? "An error occurred.";
        }
        catch { /* ignore */ }
        return "An error occurred.";
    }
}
