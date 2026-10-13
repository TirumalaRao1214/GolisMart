using System.Net.Http.Json;
using System.Text.Json;
using GOLISMart.Web.Services.Interfaces;

namespace GOLISMart.Web.Services.Implementations;

public class DeliveryApiClient : IDeliveryApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<DeliveryApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DeliveryApiClient(HttpClient http, ILogger<DeliveryApiClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<ServiceabilityResponse?> CheckServiceabilityAsync(
        string pinCode, CancellationToken ct = default)
    {
        try
        {
            var url      = $"api/v1/delivery/serviceability?pinCode={Uri.EscapeDataString(pinCode)}";
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<ServiceabilityResponse>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check serviceability for PIN {PinCode}", pinCode);
            return null;
        }
    }

    public async Task<IReadOnlyList<DeliveryAreaDto>> GetAreasAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync("api/v1/delivery/areas", ct);
            if (!response.IsSuccessStatusCode) return [];
            return await response.Content.ReadFromJsonAsync<List<DeliveryAreaDto>>(JsonOptions, ct) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch delivery areas");
            return [];
        }
    }
}
