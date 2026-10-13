using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GOLISMart.API.DTOs;
using GOLISMart.API.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace GOLISMart.API.Services.Implementations;

/// <summary>
/// Reverse-geocoding via the OpenStreetMap Nominatim public API.
///
/// LIMITATIONS — Nominatim Public Usage Policy:
///   • Maximum 1 request/second. This implementation issues at most one
///     request per cache miss; the endpoint is also guarded by a rate limiter.
///   • Results are cached for 5 minutes (caching is explicitly encouraged).
///   • Attribution must be shown wherever the data is displayed.
///   • Not suitable for sustained high-volume production traffic.
///   • To replace this implementation: register a different IGeocodingService.
/// </summary>
public class NominatimGeocodingService : IGeocodingService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NominatimGeocodingService> _logger;

    // Cache geocode results for 5 minutes (Nominatim permits and encourages caching).
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    // Coordinate precision used as cache key — ~11 m resolution at the equator.
    private const int CacheDecimalPlaces = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public NominatimGeocodingService(
        HttpClient http,
        IMemoryCache cache,
        ILogger<NominatimGeocodingService> logger)
    {
        _http   = http;
        _cache  = cache;
        _logger = logger;
    }

    public async Task<ReverseGeocodeResponse> ReverseGeocodeAsync(
        double latitude, double longitude, CancellationToken ct = default)
    {
        // Round coordinates for cache key to avoid storing near-identical entries
        var latRounded = Math.Round(latitude,  CacheDecimalPlaces);
        var lonRounded = Math.Round(longitude, CacheDecimalPlaces);
        var cacheKey   = $"geocode:{latRounded:F4}:{lonRounded:F4}";

        if (_cache.TryGetValue(cacheKey, out ReverseGeocodeResponse? cached) && cached is not null)
            return cached;

        var result = await FetchFromProviderAsync(latitude, longitude, ct);

        // Only cache successful responses with usable address data
        if (result.Success)
            _cache.Set(cacheKey, result, CacheTtl);

        return result;
    }

    private async Task<ReverseGeocodeResponse> FetchFromProviderAsync(
        double latitude, double longitude, CancellationToken ct)
    {
        var url = $"reverse?format=jsonv2&lat={latitude:G17}&lon={longitude:G17}&addressdetails=1";

        try
        {
            using var response = await _http.GetAsync(url, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _logger.LogWarning("Nominatim rate limit reached for coordinates ({Lat}, {Lon})", latitude, longitude);
                return new ReverseGeocodeResponse(false, null, "Location service is temporarily busy. Please try again shortly.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Nominatim returned {Status} for coordinates ({Lat}, {Lon})",
                    (int)response.StatusCode, latitude, longitude);
                return new ReverseGeocodeResponse(false, null, "Location service returned an unexpected response.");
            }

            NominatimReverseResponse? nominatim;
            try
            {
                nominatim = await response.Content.ReadFromJsonAsync<NominatimReverseResponse>(JsonOptions, ct);
            }
            catch (JsonException)
            {
                _logger.LogWarning("Nominatim returned malformed JSON for ({Lat}, {Lon})", latitude, longitude);
                return new ReverseGeocodeResponse(false, null, "Location service returned an unreadable response.");
            }

            if (nominatim is null)
                return new ReverseGeocodeResponse(false, null, "No address data was returned.");

            if (!string.IsNullOrWhiteSpace(nominatim.Error))
            {
                // Nominatim error string (e.g. "Unable to geocode") — do not surface internally
                _logger.LogInformation("Nominatim reported: {NominatimError} for ({Lat}, {Lon})", nominatim.Error, latitude, longitude);
                return new ReverseGeocodeResponse(false, null, "No address was found for the given coordinates.");
            }

            var address = MapToDto(nominatim, latitude, longitude);
            return new ReverseGeocodeResponse(true, address);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout from the HttpClient, not from the caller's token
            _logger.LogWarning("Nominatim request timed out for ({Lat}, {Lon})", latitude, longitude);
            return new ReverseGeocodeResponse(false, null, "Location lookup timed out. Please try again.");
        }
        catch (OperationCanceledException)
        {
            // Propagate caller cancellation
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error contacting Nominatim for ({Lat}, {Lon})", latitude, longitude);
            return new ReverseGeocodeResponse(false, null, "Location service is currently unavailable. Please enter your address manually.");
        }
    }

    // ── Field mapping ─────────────────────────────────────────────────────────
    // Maps Nominatim address components to GOLIS Mart address fields.
    // Only fills a field if the provider returns a value; does not guess.

    private static GeocodeAddressDto MapToDto(NominatimReverseResponse nominatim, double lat, double lon)
    {
        var a = nominatim.Address;

        return new GeocodeAddressDto
        {
            HouseNumber = a?.HouseNumber ?? string.Empty,

            // Street: prefer road, fall back through pedestrian / residential / footway
            Street = FirstNonEmpty(a?.Road, a?.Pedestrian, a?.Residential, a?.Footway),

            // Locality: suburb > neighbourhood > quarter
            Locality = FirstNonEmpty(a?.Suburb, a?.Neighbourhood, a?.Quarter),

            // Landmark: intentionally left blank — not reliably available from Nominatim
            Landmark = null,

            // City: city > town > village > municipality
            City = FirstNonEmpty(a?.City, a?.Town, a?.Village, a?.Municipality),

            // District: county > state_district
            District = FirstNonEmpty(a?.County, a?.StateDistrict),

            State = a?.State ?? string.Empty,

            // Postcode — validate it looks like a 6-digit Indian PIN before storing
            PinCode = IsValidIndianPin(a?.Postcode) ? a!.Postcode! : string.Empty,

            Country = a?.Country ?? string.Empty,

            Latitude  = lat,
            Longitude = lon,

            FormattedAddress = nominatim.DisplayName ?? string.Empty
        };
    }

    private static string FirstNonEmpty(params string?[] candidates)
    {
        foreach (var c in candidates)
            if (!string.IsNullOrWhiteSpace(c)) return c.Trim();
        return string.Empty;
    }

    private static bool IsValidIndianPin(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin)) return false;
        pin = pin.Trim();
        return pin.Length == 6 && pin.All(char.IsDigit);
    }
}
