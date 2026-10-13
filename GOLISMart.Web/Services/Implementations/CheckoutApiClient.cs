using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GOLISMart.Web.Services.Interfaces;

namespace GOLISMart.Web.Services.Implementations;

public class CheckoutApiClient : ICheckoutApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<CheckoutApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CheckoutApiClient(HttpClient http, ILogger<CheckoutApiClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<CheckoutValidationResponse?> ValidateAsync(
        string token,
        string sessionKey,
        int addressId,
        CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "api/v1/checkout/validate");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = JsonContent.Create(new CheckoutValidationRequest(sessionKey, addressId));
            var response = await _http.SendAsync(req, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = string.Empty;
                try { body = await response.Content.ReadAsStringAsync(ct); } catch { /* ignore read errors */ }
                _logger.LogWarning(
                    "Checkout validate returned {StatusCode} for session {SessionKey}, address {AddressId}. Response: {Body}",
                    (int)response.StatusCode, sessionKey, addressId,
                    body.Length > 500 ? body[..500] : body);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<CheckoutValidationResponse>(JsonOptions, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Checkout validation request timed out (session {SessionKey}, address {AddressId})",
                sessionKey, addressId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkout validation request failed (session {SessionKey}, address {AddressId})",
                sessionKey, addressId);
            return null;
        }
    }
}
