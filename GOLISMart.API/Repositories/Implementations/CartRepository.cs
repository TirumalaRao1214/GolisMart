using Dapper;
using GOLISMart.API.Data;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;

namespace GOLISMart.API.Repositories.Implementations;

public class CartRepository : ICartRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<CartRepository> _logger;
    private const int Timeout = 30;

    public CartRepository(IDbConnectionFactory db, ILogger<CartRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Cart?> GetBySessionKeyAsync(string sessionKey, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, UserId, SessionKey, CreatedAt, UpdatedAt, ExpiresAt
            FROM   Carts
            WHERE  SessionKey = @SessionKey
              AND  (ExpiresAt IS NULL OR ExpiresAt > SYSUTCDATETIME());
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Cart>(
            new CommandDefinition(sql, new { SessionKey = sessionKey }, commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<Cart> GetOrCreateBySessionKeyAsync(string sessionKey, CancellationToken ct = default)
    {
        var existing = await GetBySessionKeyAsync(sessionKey, ct);
        if (existing is not null) return existing;

        const string insert = """
            INSERT INTO Carts (SessionKey, CreatedAt, UpdatedAt, ExpiresAt)
            OUTPUT INSERTED.Id, INSERTED.UserId, INSERTED.SessionKey,
                   INSERTED.CreatedAt, INSERTED.UpdatedAt, INSERTED.ExpiresAt
            VALUES (@SessionKey, SYSUTCDATETIME(), SYSUTCDATETIME(),
                    DATEADD(HOUR, 24, SYSUTCDATETIME()));
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<Cart>(
            new CommandDefinition(insert, new { SessionKey = sessionKey }, commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<CartItem>> GetItemsAsync(int cartId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, CartId, ProductId, Quantity, UnitPrice, CreatedAt, UpdatedAt
            FROM   CartItems
            WHERE  CartId = @CartId
            ORDER  BY CreatedAt;
            """;
        using var conn = _db.CreateConnection();
        var result = await conn.QueryAsync<CartItem>(
            new CommandDefinition(sql, new { CartId = cartId }, commandTimeout: Timeout, cancellationToken: ct));
        return result.AsList();
    }

    public async Task UpsertItemAsync(int cartId, int productId, decimal quantity, decimal unitPrice, CancellationToken ct = default)
    {
        const string sql = """
            MERGE CartItems AS target
            USING (SELECT @CartId AS CartId, @ProductId AS ProductId) AS source
            ON target.CartId = source.CartId AND target.ProductId = source.ProductId
            WHEN MATCHED THEN
                UPDATE SET Quantity  = Quantity + @Quantity,
                           UnitPrice = @UnitPrice,
                           UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (CartId, ProductId, Quantity, UnitPrice, CreatedAt, UpdatedAt)
                VALUES (@CartId, @ProductId, @Quantity, @UnitPrice, SYSUTCDATETIME(), SYSUTCDATETIME());
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql,
            new { CartId = cartId, ProductId = productId, Quantity = quantity, UnitPrice = unitPrice },
            commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task UpdateItemQuantityAsync(int cartId, int productId, decimal quantity, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE CartItems
            SET    Quantity  = @Quantity,
                   UpdatedAt = SYSUTCDATETIME()
            WHERE  CartId = @CartId AND ProductId = @ProductId;
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql,
            new { CartId = cartId, ProductId = productId, Quantity = quantity },
            commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<bool> RemoveItemAsync(int cartId, int productId, CancellationToken ct = default)
    {
        const string sql = """
            DELETE FROM CartItems
            WHERE  CartId = @CartId AND ProductId = @ProductId;
            """;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(new CommandDefinition(sql,
            new { CartId = cartId, ProductId = productId },
            commandTimeout: Timeout, cancellationToken: ct));
        return rows > 0;
    }

    public async Task ClearCartAsync(int cartId, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM CartItems WHERE CartId = @CartId;";
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new { CartId = cartId },
            commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task TouchCartAsync(int cartId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE Carts
            SET    UpdatedAt = SYSUTCDATETIME(),
                   ExpiresAt = DATEADD(HOUR, 24, SYSUTCDATETIME())
            WHERE  Id = @CartId;
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new { CartId = cartId },
            commandTimeout: Timeout, cancellationToken: ct));
    }
}
