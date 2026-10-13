using GOLISMart.API.Models;

namespace GOLISMart.API.Repositories.Interfaces;

public interface IUserRepository
{
    Task<ApplicationUser?> GetByIdAsync(string userId, CancellationToken ct = default);
    Task<ApplicationUser?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<string> CreateAsync(ApplicationUser user, CancellationToken ct = default);
    Task UpdateAsync(ApplicationUser user, CancellationToken ct = default);
    Task UpdatePasswordHashAsync(string userId, string passwordHash, string securityStamp, CancellationToken ct = default);
    Task IncrementAccessFailedCountAsync(string userId, CancellationToken ct = default);
    Task ResetAccessFailedCountAsync(string userId, CancellationToken ct = default);
    Task SetLockoutEndAsync(string userId, DateTimeOffset? lockoutEnd, CancellationToken ct = default);
    Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetRolesAsync(string userId, CancellationToken ct = default);
    Task AssignRoleAsync(string userId, string roleId, CancellationToken ct = default);
}
