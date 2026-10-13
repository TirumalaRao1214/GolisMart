using GOLISMart.API.Models;

namespace GOLISMart.API.Repositories.Interfaces;

public interface IAddressRepository
{
    Task<IReadOnlyList<UserAddress>> GetByUserIdAsync(string userId, CancellationToken ct = default);
    Task<UserAddress?> GetByIdAndUserIdAsync(int id, string userId, CancellationToken ct = default);
    Task<int> CreateAsync(UserAddress address, CancellationToken ct = default);
    Task UpdateAsync(UserAddress address, CancellationToken ct = default);
    Task SoftDeleteAsync(int id, string userId, CancellationToken ct = default);
    Task SetDefaultAsync(int id, string userId, CancellationToken ct = default);
}
