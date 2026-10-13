using System.Text.Json.Serialization;

namespace GOLISMart.API.DTOs;

// ── Public API response ───────────────────────────────────────────────────────

/// <summary>
/// Normalized address returned to the caller after reverse geocoding.
/// Only fields supported by the provider are populated; unsupported fields
/// are left as empty strings or null so the customer must complete them.
/// </summary>
public record GeocodeAddressDto
{
    /// <summary>House or flat number, if the provider returns it.</summary>
    public string HouseNumber { get; init; } = string.Empty;

    /// <summary>Street / road name.</summary>
    public string Street { get; init; } = string.Empty;

    /// <summary>Suburb, neighbourhood, or quarter.</summary>
    public string Locality { get; init; } = string.Empty;

    /// <summary>Landmark — left blank unless a clear value is available.</summary>
    public string? Landmark { get; init; }

    /// <summary>City, town, or village.</summary>
    public string City { get; init; } = string.Empty;

    /// <summary>District or county.</summary>
    public string District { get; init; } = string.Empty;

    /// <summary>State or province.</summary>
    public string State { get; init; } = string.Empty;

    /// <summary>Postal code (PIN code in India).</summary>
    public string PinCode { get; init; } = string.Empty;

    /// <summary>Country name.</summary>
    public string Country { get; init; } = string.Empty;

    /// <summary>Actual GPS latitude passed in by the caller (not hardcoded).</summary>
    public double Latitude { get; init; }

    /// <summary>Actual GPS longitude passed in by the caller (not hardcoded).</summary>
    public double Longitude { get; init; }

    /// <summary>Provider's full formatted address string, when available.</summary>
    public string FormattedAddress { get; init; } = string.Empty;
}

/// <summary>Envelope returned by GET /api/v1/location/reverse-geocode.</summary>
public record ReverseGeocodeResponse(bool Success, GeocodeAddressDto? Address, string? Error = null);

// ── Nominatim provider response shapes (internal) ────────────────────────────
// These are NOT exposed to API consumers.

internal sealed class NominatimReverseResponse
{
    [JsonPropertyName("place_id")]
    public long PlaceId { get; init; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("address")]
    public NominatimAddress? Address { get; init; }

    /// <summary>Non-null when Nominatim returns an error (e.g. unable to geocode).</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

internal sealed class NominatimAddress
{
    // House / building
    [JsonPropertyName("house_number")]
    public string? HouseNumber { get; init; }

    // Street
    [JsonPropertyName("road")]
    public string? Road { get; init; }

    [JsonPropertyName("pedestrian")]
    public string? Pedestrian { get; init; }

    [JsonPropertyName("residential")]
    public string? Residential { get; init; }

    [JsonPropertyName("footway")]
    public string? Footway { get; init; }

    // Locality
    [JsonPropertyName("suburb")]
    public string? Suburb { get; init; }

    [JsonPropertyName("neighbourhood")]
    public string? Neighbourhood { get; init; }

    [JsonPropertyName("quarter")]
    public string? Quarter { get; init; }

    // City / town / village
    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("town")]
    public string? Town { get; init; }

    [JsonPropertyName("village")]
    public string? Village { get; init; }

    [JsonPropertyName("municipality")]
    public string? Municipality { get; init; }

    // District
    [JsonPropertyName("county")]
    public string? County { get; init; }

    [JsonPropertyName("state_district")]
    public string? StateDistrict { get; init; }

    // State
    [JsonPropertyName("state")]
    public string? State { get; init; }

    // PIN
    [JsonPropertyName("postcode")]
    public string? Postcode { get; init; }

    // Country
    [JsonPropertyName("country")]
    public string? Country { get; init; }
}
