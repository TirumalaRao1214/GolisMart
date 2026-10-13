using GOLISMart.API.DTOs;

namespace GOLISMart.API.Services.Interfaces;

/// <summary>
/// Converts GPS coordinates into a normalized postal address.
/// Implementations should be swappable; callers depend only on this contract.
/// </summary>
public interface IGeocodingService
{
    /// <summary>
    /// Reverse-geocode a GPS coordinate pair.
    /// </summary>
    /// <param name="latitude">Latitude in decimal degrees (-90 to 90).</param>
    /// <param name="longitude">Longitude in decimal degrees (-180 to 180).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ReverseGeocodeResponse"/> with <c>Success = true</c> and a
    /// populated <see cref="GeocodeAddressDto"/> on success, or
    /// <c>Success = false</c> and a safe error message on failure.
    /// </returns>
    Task<ReverseGeocodeResponse> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default);
}
