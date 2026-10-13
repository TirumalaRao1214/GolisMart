using GOLISMart.API.DTOs;

namespace GOLISMart.API.Services.Interfaces;

public interface IDeliveryService
{
    Task<ServiceabilityResponse> CheckServiceabilityAsync(string pinCode, CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryAreaDto>> GetActiveAreasAsync(CancellationToken ct = default);
}
