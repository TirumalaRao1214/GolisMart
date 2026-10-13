using Dapper;
using GOLISMart.API.Data;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;

namespace GOLISMart.API.Repositories.Implementations;

public class DeliveryRepository : IDeliveryRepository
{
    private readonly IDbConnectionFactory _db;
    private const int Timeout = 30;

    public DeliveryRepository(IDbConnectionFactory db) => _db = db;

    public async Task<DeliveryArea?> GetByPinCodeAsync(string pinCode, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1
                   Id, PinCode, AreaName, IsActive,
                   EstimatedDeliveryWindow, MinOrderValue, DeliveryFee, FreeDeliveryAbove,
                   CreatedAt, UpdatedAt
            FROM   DeliveryAreas
            WHERE  PinCode   = @PinCode
              AND  IsActive  = 1;
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<DeliveryArea>(
            new CommandDefinition(sql, new { PinCode = pinCode }, commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<DeliveryArea>> GetAllActiveAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, PinCode, AreaName, IsActive,
                   EstimatedDeliveryWindow, MinOrderValue, DeliveryFee, FreeDeliveryAbove,
                   CreatedAt, UpdatedAt
            FROM   DeliveryAreas
            WHERE  IsActive = 1
            ORDER  BY AreaName;
            """;
        using var conn = _db.CreateConnection();
        var result = await conn.QueryAsync<DeliveryArea>(
            new CommandDefinition(sql, commandTimeout: Timeout, cancellationToken: ct));
        return result.AsList();
    }
}
