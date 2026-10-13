using System.Data;
using Dapper;
using GOLISMart.API.Data;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;

namespace GOLISMart.API.Repositories.Implementations;

public class AddressRepository : IAddressRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<AddressRepository> _logger;
    private const int Timeout = 30;

    public AddressRepository(IDbConnectionFactory db, ILogger<AddressRepository> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserAddress>> GetByUserIdAsync(string userId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, UserId, RecipientName, RecipientMobile, AddressLabel,
                   HouseNumber, BuildingStreet, Locality, Landmark,
                   City, State, Pincode, DeliveryInstructions,
                   IsDefault, IsDeleted, CreatedAt, UpdatedAt
            FROM   UserAddresses
            WHERE  UserId = @UserId AND IsDeleted = 0
            ORDER  BY IsDefault DESC, CreatedAt DESC;
            """;
        using var conn = _db.CreateConnection();
        var result = await conn.QueryAsync<UserAddress>(
            new CommandDefinition(sql, new { UserId = userId }, commandTimeout: Timeout, cancellationToken: ct));
        return result.AsList();
    }

    public async Task<UserAddress?> GetByIdAndUserIdAsync(int id, string userId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, UserId, RecipientName, RecipientMobile, AddressLabel,
                   HouseNumber, BuildingStreet, Locality, Landmark,
                   City, State, Pincode, DeliveryInstructions,
                   IsDefault, IsDeleted, CreatedAt, UpdatedAt
            FROM   UserAddresses
            WHERE  Id = @Id AND UserId = @UserId AND IsDeleted = 0;
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<UserAddress>(
            new CommandDefinition(sql, new { Id = id, UserId = userId }, commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<int> CreateAsync(UserAddress address, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            if (address.IsDefault)
            {
                const string clearDefault = """
                    UPDATE UserAddresses SET IsDefault = 0
                    WHERE  UserId = @UserId AND IsDeleted = 0;
                    """;
                await conn.ExecuteAsync(new CommandDefinition(clearDefault, new { address.UserId },
                    transaction: tx, commandTimeout: Timeout, cancellationToken: ct));
            }

            const string insert = """
                INSERT INTO UserAddresses
                    (UserId, RecipientName, RecipientMobile, AddressLabel,
                     HouseNumber, BuildingStreet, Locality, Landmark,
                     City, State, Pincode, DeliveryInstructions,
                     IsDefault, IsDeleted, CreatedAt, UpdatedAt)
                OUTPUT INSERTED.Id
                VALUES
                    (@UserId, @RecipientName, @RecipientMobile, @AddressLabel,
                     @HouseNumber, @BuildingStreet, @Locality, @Landmark,
                     @City, @State, @Pincode, @DeliveryInstructions,
                     @IsDefault, 0, SYSUTCDATETIME(), SYSUTCDATETIME());
                """;
            var newId = await conn.QuerySingleAsync<int>(
                new CommandDefinition(insert, address, transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            // If this is the first address for the user, make it default automatically
            if (!address.IsDefault)
            {
                const string countSql = "SELECT COUNT(*) FROM UserAddresses WHERE UserId = @UserId AND IsDeleted = 0;";
                var count = await conn.QuerySingleAsync<int>(
                    new CommandDefinition(countSql, new { address.UserId }, transaction: tx, commandTimeout: Timeout, cancellationToken: ct));
                if (count == 1)
                {
                    const string makeDefault = "UPDATE UserAddresses SET IsDefault = 1 WHERE Id = @Id;";
                    await conn.ExecuteAsync(new CommandDefinition(makeDefault, new { Id = newId },
                        transaction: tx, commandTimeout: Timeout, cancellationToken: ct));
                }
            }

            tx.Commit();
            return newId;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task UpdateAsync(UserAddress address, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            if (address.IsDefault)
            {
                const string clearDefault = """
                    UPDATE UserAddresses SET IsDefault = 0
                    WHERE  UserId = @UserId AND IsDeleted = 0 AND Id <> @Id;
                    """;
                await conn.ExecuteAsync(new CommandDefinition(clearDefault, new { address.UserId, address.Id },
                    transaction: tx, commandTimeout: Timeout, cancellationToken: ct));
            }

            const string sql = """
                UPDATE UserAddresses
                SET    RecipientName        = @RecipientName,
                       RecipientMobile      = @RecipientMobile,
                       AddressLabel         = @AddressLabel,
                       HouseNumber          = @HouseNumber,
                       BuildingStreet       = @BuildingStreet,
                       Locality             = @Locality,
                       Landmark             = @Landmark,
                       City                 = @City,
                       State                = @State,
                       Pincode              = @Pincode,
                       DeliveryInstructions = @DeliveryInstructions,
                       IsDefault            = @IsDefault,
                       UpdatedAt            = SYSUTCDATETIME()
                WHERE  Id = @Id AND UserId = @UserId AND IsDeleted = 0;
                """;
            await conn.ExecuteAsync(new CommandDefinition(sql, address, transaction: tx, commandTimeout: Timeout, cancellationToken: ct));
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task SoftDeleteAsync(int id, string userId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE UserAddresses
            SET    IsDeleted  = 1,
                   IsDefault  = 0,
                   UpdatedAt  = SYSUTCDATETIME()
            WHERE  Id = @Id AND UserId = @UserId;
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id, UserId = userId }, commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task SetDefaultAsync(int id, string userId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            const string clearDefault = """
                UPDATE UserAddresses SET IsDefault = 0
                WHERE  UserId = @UserId AND IsDeleted = 0;
                """;
            await conn.ExecuteAsync(new CommandDefinition(clearDefault, new { UserId = userId },
                transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            const string setDefault = """
                UPDATE UserAddresses SET IsDefault = 1
                WHERE  Id = @Id AND UserId = @UserId AND IsDeleted = 0;
                """;
            await conn.ExecuteAsync(new CommandDefinition(setDefault, new { Id = id, UserId = userId },
                transaction: tx, commandTimeout: Timeout, cancellationToken: ct));
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
