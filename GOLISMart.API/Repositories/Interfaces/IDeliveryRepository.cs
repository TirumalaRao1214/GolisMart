using GOLISMart.API.Models;

namespace GOLISMart.API.Repositories.Interfaces;

public interface IDeliveryRepository
{
    Task<DeliveryArea?> GetByPinCodeAsync(string pinCode, CancellationToken ct = default);
    Task<IReadOnlyList<DeliveryArea>> GetAllActiveAsync(CancellationToken ct = default);
}
