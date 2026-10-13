namespace GOLISMart.Web.Services.Interfaces;

public interface ILocationApiClient
{
    Task<ReverseGeocodeResponse?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default);
}
