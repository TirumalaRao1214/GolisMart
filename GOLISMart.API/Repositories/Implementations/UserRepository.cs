using Dapper;
using GOLISMart.API.Data;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;

namespace GOLISMart.API.Repositories.Implementations;

public class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<UserRepository> _logger;
    private const int Timeout = 30;

    public UserRepository(IDbConnectionFactory db, ILogger<UserRepository> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<ApplicationUser?> GetByIdAsync(string userId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, UserName, NormalizedUserName, Email, NormalizedEmail,
                   EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
                   PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled,
                   LockoutEnd, LockoutEnabled, AccessFailedCount,
                   DisplayName, IsActive, CreatedAt, UpdatedAt
            FROM   AspNetUsers
            WHERE  Id = @Id AND IsActive = 1;
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<ApplicationUser>(
            new CommandDefinition(sql, new { Id = userId }, commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<ApplicationUser?> GetByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, UserName, NormalizedUserName, Email, NormalizedEmail,
                   EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
                   PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled,
                   LockoutEnd, LockoutEnabled, AccessFailedCount,
                   DisplayName, IsActive, CreatedAt, UpdatedAt
            FROM   AspNetUsers
            WHERE  NormalizedEmail = @NormalizedEmail AND IsActive = 1;
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<ApplicationUser>(
            new CommandDefinition(sql, new { NormalizedEmail = normalizedEmail }, commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<bool> ExistsByNormalizedEmailAsync(string normalizedEmail, CancellationToken ct = default)
    {
        const string sql = "SELECT CAST(1 AS BIT) FROM AspNetUsers WHERE NormalizedEmail = @NormalizedEmail;";
        using var conn = _db.CreateConnection();
        var result = await conn.QuerySingleOrDefaultAsync<bool?>(
            new CommandDefinition(sql, new { NormalizedEmail = normalizedEmail }, commandTimeout: Timeout, cancellationToken: ct));
        return result == true;
    }

    public async Task<string> CreateAsync(ApplicationUser user, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO AspNetUsers
                (Id, UserName, NormalizedUserName, Email, NormalizedEmail,
                 EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
                 PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled,
                 LockoutEnabled, AccessFailedCount,
                 DisplayName, IsActive, CreatedAt, UpdatedAt)
            VALUES
                (@Id, @UserName, @NormalizedUserName, @Email, @NormalizedEmail,
                 @EmailConfirmed, @PasswordHash, @SecurityStamp, @ConcurrencyStamp,
                 @PhoneNumber, @PhoneNumberConfirmed, @TwoFactorEnabled,
                 @LockoutEnabled, @AccessFailedCount,
                 @DisplayName, @IsActive, SYSUTCDATETIME(), SYSUTCDATETIME());
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            new CommandDefinition(sql, user, commandTimeout: Timeout, cancellationToken: ct));
        return user.Id;
    }

    public async Task UpdateAsync(ApplicationUser user, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE AspNetUsers
            SET    DisplayName        = @DisplayName,
                   PhoneNumber        = @PhoneNumber,
                   ConcurrencyStamp   = @ConcurrencyStamp,
                   UpdatedAt          = SYSUTCDATETIME()
            WHERE  Id = @Id;
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            new CommandDefinition(sql, new { user.DisplayName, user.PhoneNumber, user.ConcurrencyStamp, user.Id },
                commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task UpdatePasswordHashAsync(string userId, string passwordHash, string securityStamp, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE AspNetUsers
            SET    PasswordHash   = @PasswordHash,
                   SecurityStamp  = @SecurityStamp,
                   ConcurrencyStamp = @ConcurrencyStamp,
                   UpdatedAt      = SYSUTCDATETIME()
            WHERE  Id = @Id;
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            new CommandDefinition(sql,
                new { PasswordHash = passwordHash, SecurityStamp = securityStamp,
                      ConcurrencyStamp = Guid.NewGuid().ToString(), Id = userId },
                commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task IncrementAccessFailedCountAsync(string userId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE AspNetUsers
            SET    AccessFailedCount = AccessFailedCount + 1,
                   UpdatedAt         = SYSUTCDATETIME()
            WHERE  Id = @Id;
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = userId }, commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task ResetAccessFailedCountAsync(string userId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE AspNetUsers
            SET    AccessFailedCount = 0,
                   UpdatedAt         = SYSUTCDATETIME()
            WHERE  Id = @Id;
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = userId }, commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task SetLockoutEndAsync(string userId, DateTimeOffset? lockoutEnd, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE AspNetUsers
            SET    LockoutEnd = @LockoutEnd,
                   UpdatedAt  = SYSUTCDATETIME()
            WHERE  Id = @Id;
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new { LockoutEnd = lockoutEnd, Id = userId }, commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(string userId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT r.Name FROM AspNetRoles r
            JOIN AspNetUserRoles ur ON ur.RoleId = r.Id
            WHERE ur.UserId = @UserId;
            """;
        using var conn = _db.CreateConnection();
        var roles = await conn.QueryAsync<string>(
            new CommandDefinition(sql, new { UserId = userId }, commandTimeout: Timeout, cancellationToken: ct));
        return roles.ToList().AsReadOnly();
    }

    public async Task AssignRoleAsync(string userId, string roleId, CancellationToken ct = default)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM AspNetUserRoles WHERE UserId = @UserId AND RoleId = @RoleId)
                INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (@UserId, @RoleId);
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            new CommandDefinition(sql, new { UserId = userId, RoleId = roleId }, commandTimeout: Timeout, cancellationToken: ct));
    }
}
