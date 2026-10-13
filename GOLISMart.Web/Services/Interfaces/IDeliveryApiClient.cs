namespace GOLISMart.Web.Services.Interfaces;

public interface IDeliveryApiClient
{
    Task<ServiceabilityResponse?> CheckServiceabilityAsync(string pinCode, CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryAreaDto>> GetAreasAsync(CancellationToken ct = default);
}
