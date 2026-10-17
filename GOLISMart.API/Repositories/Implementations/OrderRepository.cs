using System.Data;
using Dapper;
using GOLISMart.API.Data;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;

namespace GOLISMart.API.Repositories.Implementations;

public class OrderRepository : IOrderRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<OrderRepository> _logger;
    private const int Timeout = 60;

    // Status values that allow cancellation
    private static readonly HashSet<string> CancellableStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "Pending", "Confirmed" };

    public OrderRepository(IDbConnectionFactory db, ILogger<OrderRepository> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── CreateOrderAsync ───────────────────────────────────────────────────────
    // Performs the following inside a serializable transaction:
    //   1. Insert Order row.
    //   2. Insert OrderItem rows.
    //   3. Deduct QuantityOnHand for each item (with optimistic concurrency check).
    //   4. Append initial OrderStatusHistory record.
    //
    public async Task<Order> CreateOrderAsync(
        Order order,
        IReadOnlyList<OrderItem> items,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            // ── 1. Insert Order ────────────────────────────────────────────────
            const string insertOrder = """
                INSERT INTO Orders
                    (OrderNumber, UserId,
                     ShipToAddressLine1, ShipToAddressLine2,
                     ShipToCity, ShipToState, ShipToPincode,
                     SubTotal, DiscountAmount, DeliveryFee, TotalAmount,
                     CouponCode, CouponDiscount,
                     PaymentMethod, PaymentStatus,
                     Status, Notes, IdempotencyKey,
                     CreatedAt, UpdatedAt)
                OUTPUT INSERTED.Id, INSERTED.OrderNumber, INSERTED.CreatedAt, INSERTED.UpdatedAt
                VALUES
                    (@OrderNumber, @UserId,
                     @ShipToAddressLine1, @ShipToAddressLine2,
                     @ShipToCity, @ShipToState, @ShipToPincode,
                     @SubTotal, @DiscountAmount, @DeliveryFee, @TotalAmount,
                     @CouponCode, @CouponDiscount,
                     @PaymentMethod, @PaymentStatus,
                     @Status, @Notes, @IdempotencyKey,
                     SYSUTCDATETIME(), SYSUTCDATETIME());
                """;

            var created = await conn.QuerySingleAsync<(int Id, string OrderNumber, DateTime CreatedAt, DateTime UpdatedAt)>(
                new CommandDefinition(insertOrder, order, transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            order.Id        = created.Id;
            order.CreatedAt = created.CreatedAt;
            order.UpdatedAt = created.UpdatedAt;

            // ── 2. Insert OrderItems ───────────────────────────────────────────
            const string insertItem = """
                INSERT INTO OrderItems
                    (OrderId, ProductId, ProductName, ProductSKU, UnitLabel,
                     Quantity, UnitPrice, OfferPrice, EffectivePrice, LineTotal)
                VALUES
                    (@OrderId, @ProductId, @ProductName, @ProductSKU, @UnitLabel,
                     @Quantity, @UnitPrice, @OfferPrice, @EffectivePrice, @LineTotal);
                """;

            foreach (var item in items)
            {
                item.OrderId = order.Id;
                await conn.ExecuteAsync(new CommandDefinition(insertItem, item,
                    transaction: tx, commandTimeout: Timeout, cancellationToken: ct));
            }

            // ── 3. Deduct inventory for each item ─────────────────────────────
            //    We use UPDATE … WHERE QuantityOnHand >= @Qty as an optimistic
            //    concurrency guard. If no row is updated the stock was insufficient
            //    (race condition) and we roll back.
            const string deductInventory = """
                UPDATE Inventory
                SET    QuantityOnHand = QuantityOnHand - @Qty,
                       UpdatedAt      = SYSUTCDATETIME()
                WHERE  ProductId        = @ProductId
                  AND  QuantityOnHand  >= @Qty;
                """;

            const string insertInvTx = """
                INSERT INTO InventoryTransactions
                    (ProductId, TransactionType, QuantityChange, QuantityAfter,
                     ReferenceId, ReferenceType, Notes, CreatedAt)
                SELECT @ProductId, 'Sale', -@Qty,
                       QuantityOnHand,
                       @OrderId, 'Order', @Note, SYSUTCDATETIME()
                FROM   Inventory
                WHERE  ProductId = @ProductId;
                """;

            foreach (var item in items)
            {
                var deducted = await conn.ExecuteAsync(
                    new CommandDefinition(deductInventory,
                        new { ProductId = item.ProductId, Qty = item.Quantity },
                        transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

                if (deducted == 0)
                {
                    tx.Rollback();
                    throw new InvalidOperationException(
                        $"Insufficient stock for product {item.ProductId} ({item.ProductName}). " +
                        "The item may have sold out while you were checking out.");
                }

                await conn.ExecuteAsync(
                    new CommandDefinition(insertInvTx,
                        new { item.ProductId, Qty = item.Quantity, OrderId = order.Id,
                              Note = $"Order #{order.OrderNumber}" },
                        transaction: tx, commandTimeout: Timeout, cancellationToken: ct));
            }

            // ── 4. Initial status history record ─────────────────────────────
            const string insertHistory = """
                INSERT INTO OrderStatusHistory (OrderId, Status, Notes, ChangedBy, ChangedAt)
                VALUES (@OrderId, @Status, @Notes, @ChangedBy, SYSUTCDATETIME());
                """;

            await conn.ExecuteAsync(new CommandDefinition(insertHistory,
                new { OrderId = order.Id, Status = order.Status,
                      Notes = "Order placed", ChangedBy = order.UserId },
                transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            tx.Commit();
            _logger.LogInformation("Order {OrderNumber} created (Id={OrderId})", order.OrderNumber, order.Id);
            return order;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            try { tx.Rollback(); } catch { /* swallow rollback errors */ }
            _logger.LogError(ex, "CreateOrderAsync failed, transaction rolled back");
            throw;
        }
        catch
        {
            try { tx.Rollback(); } catch { /* swallow rollback errors */ }
            throw;
        }
    }

    // ── GetByIdAndUserIdAsync ──────────────────────────────────────────────────
    public async Task<Order?> GetByIdAndUserIdAsync(int orderId, string userId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, OrderNumber, UserId,
                   ShipToAddressLine1, ShipToAddressLine2,
                   ShipToCity, ShipToState, ShipToPincode,
                   SubTotal, DiscountAmount, DeliveryFee, TotalAmount,
                   CouponCode, CouponDiscount,
                   PaymentMethod, PaymentStatus,
                   Status, DeliverySlotId, ScheduledDelivery, Notes, IdempotencyKey,
                   CreatedAt, UpdatedAt
            FROM   Orders
            WHERE  Id = @OrderId AND UserId = @UserId;
            """;

        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Order>(
            new CommandDefinition(sql, new { OrderId = orderId, UserId = userId },
                commandTimeout: Timeout, cancellationToken: ct));
    }

    // ── GetDetailByIdAndUserIdAsync ────────────────────────────────────────────
    public async Task<(Order? Order, IReadOnlyList<OrderItem> Items)> GetDetailByIdAndUserIdAsync(
        int orderId, string userId, CancellationToken ct = default)
    {
        const string orderSql = """
            SELECT Id, OrderNumber, UserId,
                   ShipToAddressLine1, ShipToAddressLine2,
                   ShipToCity, ShipToState, ShipToPincode,
                   SubTotal, DiscountAmount, DeliveryFee, TotalAmount,
                   CouponCode, CouponDiscount,
                   PaymentMethod, PaymentStatus,
                   Status, DeliverySlotId, ScheduledDelivery, Notes, IdempotencyKey,
                   CreatedAt, UpdatedAt
            FROM   Orders
            WHERE  Id = @OrderId AND UserId = @UserId;
            """;

        const string itemsSql = """
            SELECT Id, OrderId, ProductId, ProductName, ProductSKU, UnitLabel,
                   Quantity, UnitPrice, OfferPrice, EffectivePrice, LineTotal
            FROM   OrderItems
            WHERE  OrderId = @OrderId;
            """;

        using var conn = _db.CreateConnection();
        var order = await conn.QuerySingleOrDefaultAsync<Order>(
            new CommandDefinition(orderSql, new { OrderId = orderId, UserId = userId },
                commandTimeout: Timeout, cancellationToken: ct));

        if (order is null)
            return (null, []);

        var items = await conn.QueryAsync<OrderItem>(
            new CommandDefinition(itemsSql, new { OrderId = orderId },
                commandTimeout: Timeout, cancellationToken: ct));

        return (order, items.AsList());
    }

    // ── GetStatusHistoryAsync ─────────────────────────────────────────────────
    public async Task<IReadOnlyList<OrderStatusHistory>> GetStatusHistoryAsync(
        int orderId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, OrderId, Status, Notes, ChangedBy, ChangedAt
            FROM   OrderStatusHistory
            WHERE  OrderId = @OrderId
            ORDER  BY ChangedAt DESC;
            """;

        using var conn = _db.CreateConnection();
        var result = await conn.QueryAsync<OrderStatusHistory>(
            new CommandDefinition(sql, new { OrderId = orderId },
                commandTimeout: Timeout, cancellationToken: ct));
        return result.AsList();
    }

    // ── GetByUserIdAsync ──────────────────────────────────────────────────────
    public async Task<(IReadOnlyList<Order> Orders, int TotalCount)> GetByUserIdAsync(
        string userId,
        int page,
        int pageSize,
        string? statusFilter,
        DateTime? dateFrom = null,
        DateTime? dateTo   = null,
        CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var where = "o.UserId = @UserId";
        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId);

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            where += " AND o.Status = @Status";
            parameters.Add("Status", statusFilter.Trim());
        }

        if (dateFrom.HasValue)
        {
            where += " AND o.CreatedAt >= @DateFrom";
            parameters.Add("DateFrom", dateFrom.Value.Date);
        }

        if (dateTo.HasValue)
        {
            where += " AND o.CreatedAt < @DateTo";
            parameters.Add("DateTo", dateTo.Value.Date.AddDays(1));  // inclusive of dateTo day
        }

        var countSql = $"SELECT COUNT(*) FROM Orders o WHERE {where};";

        var dataSql = $"""
            SELECT o.Id, o.OrderNumber, o.UserId,
                   o.ShipToAddressLine1, o.ShipToAddressLine2,
                   o.ShipToCity, o.ShipToState, o.ShipToPincode,
                   o.SubTotal, o.DiscountAmount, o.DeliveryFee, o.TotalAmount,
                   o.PaymentMethod, o.PaymentStatus,
                   o.Status, o.CreatedAt, o.UpdatedAt,
                   (SELECT COUNT(*) FROM OrderItems oi WHERE oi.OrderId = o.Id) AS ItemCount
            FROM   Orders o
            WHERE  {where}
            ORDER  BY o.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        using var conn = _db.CreateConnection();
        var totalCount = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, commandTimeout: Timeout, cancellationToken: ct));

        var orders = await conn.QueryAsync<Order>(
            new CommandDefinition(dataSql, parameters, commandTimeout: Timeout, cancellationToken: ct));

        return (orders.AsList(), totalCount);
    }

    // ── CancelOrderAsync ──────────────────────────────────────────────────────
    public async Task<bool> CancelOrderAsync(
        int orderId, string userId, string changedBy, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            // Lock the row and verify ownership + cancellable state
            const string lockSql = """
                SELECT Id, Status, UserId
                FROM   Orders WITH (UPDLOCK, ROWLOCK)
                WHERE  Id = @OrderId AND UserId = @UserId;
                """;

            var row = await conn.QuerySingleOrDefaultAsync<(int Id, string Status, string UserId)>(
                new CommandDefinition(lockSql, new { OrderId = orderId, UserId = userId },
                    transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            if (row.Id == 0)
            {
                tx.Rollback();
                return false;   // not found or wrong owner
            }

            if (!CancellableStatuses.Contains(row.Status))
            {
                tx.Rollback();
                return false;   // already past cancellable stage
            }

            // Update order status
            const string updateOrder = """
                UPDATE Orders
                SET    Status    = 'Cancelled',
                       UpdatedAt = SYSUTCDATETIME()
                WHERE  Id = @OrderId;
                """;

            await conn.ExecuteAsync(new CommandDefinition(updateOrder, new { OrderId = orderId },
                transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            // Restore inventory for each item
            const string restoreInventory = """
                UPDATE Inventory
                SET    QuantityOnHand = QuantityOnHand + oi.Quantity,
                       UpdatedAt      = SYSUTCDATETIME()
                FROM   Inventory i
                JOIN   OrderItems oi ON oi.ProductId = i.ProductId
                WHERE  oi.OrderId = @OrderId;
                """;

            await conn.ExecuteAsync(new CommandDefinition(restoreInventory, new { OrderId = orderId },
                transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            // Record inventory return transactions
            const string returnInvTx = """
                INSERT INTO InventoryTransactions
                    (ProductId, TransactionType, QuantityChange, QuantityAfter,
                     ReferenceId, ReferenceType, Notes, CreatedAt)
                SELECT oi.ProductId, 'Return', oi.Quantity,
                       i.QuantityOnHand,
                       @OrderId, 'Order', 'Cancelled — stock restored', SYSUTCDATETIME()
                FROM   OrderItems oi
                JOIN   Inventory i ON i.ProductId = oi.ProductId
                WHERE  oi.OrderId = @OrderId;
                """;

            await conn.ExecuteAsync(new CommandDefinition(returnInvTx, new { OrderId = orderId },
                transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            // Status history record
            const string insertHistory = """
                INSERT INTO OrderStatusHistory (OrderId, Status, Notes, ChangedBy, ChangedAt)
                VALUES (@OrderId, 'Cancelled', 'Order cancelled by customer', @ChangedBy, SYSUTCDATETIME());
                """;

            await conn.ExecuteAsync(new CommandDefinition(insertHistory,
                new { OrderId = orderId, ChangedBy = changedBy },
                transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            tx.Commit();
            _logger.LogInformation("Order {OrderId} cancelled by user {UserId}", orderId, userId);
            return true;
        }
        catch
        {
            try { tx.Rollback(); } catch { /* swallow */ }
            throw;
        }
    }

    // ── VoidOrderAsync ────────────────────────────────────────────────────────
    // Called when the payment gateway call fails immediately after order creation.
    // Restores inventory and marks the order Cancelled so it does not accumulate
    // as a dangling Pending row with deducted stock.
    public async Task VoidOrderAsync(int orderId, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            // Only void if still Pending and has no payment record
            const string checkSql = """
                SELECT Id, Status FROM Orders
                WHERE  Id = @OrderId AND Status = 'Pending';
                """;

            var exists = await conn.QuerySingleOrDefaultAsync<int?>(
                new CommandDefinition(checkSql, new { OrderId = orderId },
                    transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            if (exists is null)
            {
                tx.Rollback();
                return; // already progressed past Pending — leave it alone
            }

            // Restore inventory
            const string restoreInv = """
                UPDATE Inventory
                SET    QuantityOnHand = QuantityOnHand + oi.Quantity,
                       UpdatedAt      = SYSUTCDATETIME()
                FROM   Inventory i
                JOIN   OrderItems oi ON oi.ProductId = i.ProductId
                WHERE  oi.OrderId = @OrderId;
                """;

            await conn.ExecuteAsync(new CommandDefinition(restoreInv, new { OrderId = orderId },
                transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            // Cancel the order
            const string cancelSql = """
                UPDATE Orders
                SET    Status     = 'Cancelled',
                       UpdatedAt  = SYSUTCDATETIME()
                WHERE  Id = @OrderId;
                """;

            await conn.ExecuteAsync(new CommandDefinition(cancelSql, new { OrderId = orderId },
                transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            // Status history
            const string historySql = """
                INSERT INTO OrderStatusHistory (OrderId, Status, Notes, ChangedBy, ChangedAt)
                VALUES (@OrderId, 'Cancelled', 'Voided — payment gateway unavailable', 'system', SYSUTCDATETIME());
                """;

            await conn.ExecuteAsync(new CommandDefinition(historySql, new { OrderId = orderId },
                transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            tx.Commit();
            _logger.LogInformation("Order {OrderId} voided (gateway failure)", orderId);
        }
        catch
        {
            try { tx.Rollback(); } catch { /* swallow */ }
            throw;
        }
    }

    // ── GetByIdempotencyKeyAsync ──────────────────────────────────────────────
    public async Task<Order?> GetByIdempotencyKeyAsync(
        string idempotencyKey, string userId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, OrderNumber, UserId,
                   ShipToAddressLine1, ShipToAddressLine2,
                   ShipToCity, ShipToState, ShipToPincode,
                   SubTotal, DiscountAmount, DeliveryFee, TotalAmount,
                   PaymentMethod, PaymentStatus,
                   Status, CreatedAt, UpdatedAt
            FROM   Orders
            WHERE  IdempotencyKey = @Key AND UserId = @UserId;
            """;

        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Order>(
            new CommandDefinition(sql, new { Key = idempotencyKey, UserId = userId },
                commandTimeout: Timeout, cancellationToken: ct));
    }
}
