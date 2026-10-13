using System.Net.Http.Json;
using System.Text.Json;
using GOLISMart.Web.Services.Interfaces;

namespace GOLISMart.Web.Services.Implementations;

public class LocationApiClient : ILocationApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<LocationApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public LocationApiClient(HttpClient http, ILogger<LocationApiClient> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<ReverseGeocodeResponse?> ReverseGeocodeAsync(
        double latitude, double longitude, CancellationToken ct = default)
    {
        try
        {
            var url = $"api/v1/location/reverse-geocode?latitude={latitude}&longitude={longitude}";
            var response = await _http.GetAsync(url, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                return new ReverseGeocodeResponse(false, null, "Rate limit reached. Please wait a moment and try again.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ReverseGeocodeResponse(false, null, "Location service is currently unavailable.");
            }

            return await response.Content.ReadFromJsonAsync<ReverseGeocodeResponse>(JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reverse geocode {Lat}, {Lon}", latitude, longitude);
            return new ReverseGeocodeResponse(false, null, "Failed to connect to location service.");
        }
    }
}
