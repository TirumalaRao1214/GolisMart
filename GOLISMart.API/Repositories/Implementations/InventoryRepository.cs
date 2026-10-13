using Dapper;
using GOLISMart.API.Data;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;

namespace GOLISMart.API.Repositories.Implementations;

public class InventoryRepository : IInventoryRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<InventoryRepository> _logger;
    private const int QueryTimeout = 30;

    public InventoryRepository(IDbConnectionFactory connectionFactory, ILogger<InventoryRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<InventoryStatus?> GetByProductIdAsync(int productId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ProductId, QuantityOnHand, ReservedQuantity, LowStockThreshold
            FROM   Inventory
            WHERE  ProductId = @ProductId;
            """;

        using var conn = _connectionFactory.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<InventoryStatus>(
            new CommandDefinition(sql, new { ProductId = productId }, commandTimeout: QueryTimeout, cancellationToken: ct));
    }

    public async Task<IReadOnlyDictionary<int, InventoryStatus>> GetByProductIdsAsync(IEnumerable<int> productIds, CancellationToken ct = default)
    {
        var idList = productIds.ToList();
        if (idList.Count == 0) return new Dictionary<int, InventoryStatus>();

        const string sql = """
            SELECT ProductId, QuantityOnHand, ReservedQuantity, LowStockThreshold
            FROM   Inventory
            WHERE  ProductId IN @Ids;
            """;

        using var conn = _connectionFactory.CreateConnection();
        var rows = await conn.QueryAsync<InventoryStatus>(
            new CommandDefinition(sql, new { Ids = idList }, commandTimeout: QueryTimeout, cancellationToken: ct));
        return rows.ToDictionary(r => r.ProductId);
    }

    public async Task<IReadOnlyList<InventoryStatus>> GetLowStockItemsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT ProductId, QuantityOnHand, ReservedQuantity, LowStockThreshold
            FROM   Inventory
            WHERE  (QuantityOnHand - ReservedQuantity) <= LowStockThreshold
              AND  QuantityOnHand > 0
            ORDER  BY (QuantityOnHand - ReservedQuantity) ASC;
            """;

        using var conn = _connectionFactory.CreateConnection();
        var result = await conn.QueryAsync<InventoryStatus>(
            new CommandDefinition(sql, commandTimeout: QueryTimeout, cancellationToken: ct));
        return result.AsList();
    }
}
