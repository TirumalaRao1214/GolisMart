using System.Text;
using Dapper;
using GOLISMart.API.Data;
using GOLISMart.API.DTOs;
using GOLISMart.API.Repositories.Interfaces;

namespace GOLISMart.API.Repositories.Implementations;

public class AdminRepository : IAdminRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<AdminRepository> _logger;
    private const int Timeout = 30;

    // Valid admin order status transitions
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pending"]        = new[] { "Confirmed", "Cancelled", "Failed" },
            ["Confirmed"]      = new[] { "Processing", "Cancelled", "Failed" },
            ["Processing"]     = new[] { "OutForDelivery", "Cancelled" },
            ["OutForDelivery"] = new[] { "Delivered", "Failed" },
        };

    public AdminRepository(IDbConnectionFactory db, ILogger<AdminRepository> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                COUNT(*)                                                                                                                AS TotalOrders,
                SUM(CASE WHEN Status = 'Pending'        THEN 1 ELSE 0 END)                                                            AS PendingOrders,
                SUM(CASE WHEN Status = 'Confirmed'      THEN 1 ELSE 0 END)                                                            AS ConfirmedOrders,
                SUM(CASE WHEN Status = 'Processing'     THEN 1 ELSE 0 END)                                                            AS PreparingOrders,
                SUM(CASE WHEN Status = 'OutForDelivery' THEN 1 ELSE 0 END)                                                            AS OutForDeliveryOrders,
                SUM(CASE WHEN Status = 'Delivered'      THEN 1 ELSE 0 END)                                                            AS DeliveredOrders,
                ISNULL(SUM(CASE WHEN Status NOT IN ('Cancelled','Refunded') AND CAST(CreatedAt AS DATE) = CAST(GETUTCDATE() AS DATE) THEN TotalAmount ELSE 0 END), 0) AS TodaySales,
                ISNULL(SUM(CASE WHEN Status NOT IN ('Cancelled','Refunded') AND CreatedAt >= DATEADD(DAY, -7,  GETUTCDATE())          THEN TotalAmount ELSE 0 END), 0) AS WeekSales,
                ISNULL(SUM(CASE WHEN Status NOT IN ('Cancelled','Refunded') AND CreatedAt >= DATEADD(DAY, -30, GETUTCDATE())          THEN TotalAmount ELSE 0 END), 0) AS MonthSales
            FROM Orders;

            SELECT COUNT(*)
            FROM Inventory
            WHERE (QuantityOnHand - ReservedQuantity) <= LowStockThreshold AND QuantityOnHand > 0;

            SELECT COUNT(*) FROM Products WHERE IsActive = 1;

            SELECT TOP 5
                o.Id            AS OrderId,
                o.OrderNumber,
                u.Email         AS CustomerEmail,
                o.TotalAmount   AS Total,
                o.Status,
                o.CreatedAt
            FROM Orders o
            JOIN AspNetUsers u ON u.Id = o.UserId
            ORDER BY o.CreatedAt DESC;

            SELECT
                i.ProductId,
                p.Name          AS ProductName,
                p.SKU,
                i.QuantityOnHand,
                i.LowStockThreshold
            FROM Inventory i
            JOIN Products p ON p.Id = i.ProductId
            WHERE (i.QuantityOnHand - i.ReservedQuantity) <= i.LowStockThreshold
              AND i.QuantityOnHand > 0
            ORDER BY (i.QuantityOnHand - i.LowStockThreshold);
            """;

        using var conn = _db.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(sql, commandTimeout: Timeout, cancellationToken: ct));

        var summary        = await multi.ReadSingleAsync<OrderCountRow>();
        var lowStockCount  = await multi.ReadSingleAsync<int>();
        var activeProducts = await multi.ReadSingleAsync<int>();
        var recentOrders   = (await multi.ReadAsync<RecentOrderDto>()).ToList();
        var lowStockItems  = (await multi.ReadAsync<LowStockItemDto>()).ToList();

        return new DashboardSummaryDto(
            summary.TotalOrders,
            summary.PendingOrders,
            summary.ConfirmedOrders,
            summary.PreparingOrders,
            summary.OutForDeliveryOrders,
            summary.DeliveredOrders,
            summary.TodaySales,
            summary.WeekSales,
            summary.MonthSales,
            lowStockCount,
            activeProducts,
            recentOrders,
            lowStockItems);
    }

    // ── Orders ────────────────────────────────────────────────────────────────

    public async Task<(IReadOnlyList<AdminOrderListDto> Orders, int TotalCount)> GetOrdersAsync(
        string? search, string? status, string? paymentStatus,
        DateTime? fromDate, DateTime? toDate,
        int page, int pageSize, CancellationToken ct = default)
    {
        var where = new StringBuilder("WHERE 1=1");
        if (!string.IsNullOrWhiteSpace(search))
            where.Append(" AND (o.OrderNumber LIKE @Search OR u.Email LIKE @Search OR u.DisplayName LIKE @Search)");
        if (!string.IsNullOrWhiteSpace(status))
            where.Append(" AND o.Status = @Status");
        if (!string.IsNullOrWhiteSpace(paymentStatus))
            where.Append(" AND o.PaymentStatus = @PaymentStatus");
        if (fromDate.HasValue)
            where.Append(" AND o.CreatedAt >= @FromDate");
        if (toDate.HasValue)
            where.Append(" AND o.CreatedAt <= @ToDate");

        var sql = $"""
            SELECT COUNT(*) FROM Orders o JOIN AspNetUsers u ON u.Id = o.UserId {where};

            SELECT
                o.Id            AS OrderId,
                o.OrderNumber,
                u.Email         AS CustomerEmail,
                ISNULL(u.DisplayName, u.Email) AS CustomerName,
                o.TotalAmount,
                o.Status,
                o.PaymentMethod,
                o.PaymentStatus,
                o.CreatedAt,
                (SELECT COUNT(*) FROM OrderItems oi WHERE oi.OrderId = o.Id) AS ItemCount
            FROM Orders o
            JOIN AspNetUsers u ON u.Id = o.UserId
            {where}
            ORDER BY o.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var parameters = new
        {
            Search        = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%",
            Status        = status,
            PaymentStatus = paymentStatus,
            FromDate      = fromDate,
            ToDate        = toDate,
            Offset        = (page - 1) * pageSize,
            PageSize      = pageSize
        };

        using var conn  = _db.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, commandTimeout: Timeout, cancellationToken: ct));

        var total  = await multi.ReadSingleAsync<int>();
        var orders = (await multi.ReadAsync<AdminOrderListDto>()).ToList();

        return (orders, total);
    }

    public async Task<AdminOrderDetailDto?> GetOrderDetailAsync(int orderId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                o.Id            AS OrderId,
                o.OrderNumber,
                u.Email         AS CustomerEmail,
                ISNULL(u.DisplayName, u.Email) AS CustomerName,
                o.Status,
                o.PaymentMethod,
                o.PaymentStatus,
                TRIM(
                    o.ShipToAddressLine1
                    + CASE WHEN ISNULL(o.ShipToAddressLine2,'') <> '' THEN ', ' + o.ShipToAddressLine2 ELSE '' END
                    + ', ' + o.ShipToCity
                    + ' - ' + o.ShipToPincode
                    + ', ' + o.ShipToState
                ) AS DeliveryAddress,
                o.SubTotal,
                o.DeliveryFee,
                o.DiscountAmount,
                o.TotalAmount,
                o.Notes
            FROM Orders o
            JOIN AspNetUsers u ON u.Id = o.UserId
            WHERE o.Id = @OrderId;

            SELECT
                oi.ProductId,
                oi.ProductName,
                oi.ProductSKU,
                oi.UnitLabel,
                oi.Quantity,
                oi.UnitPrice,
                oi.OfferPrice,
                oi.EffectivePrice,
                oi.LineTotal
            FROM OrderItems oi
            WHERE oi.OrderId = @OrderId;

            SELECT Status, Notes, ChangedAt
            FROM OrderStatusHistory
            WHERE OrderId = @OrderId
            ORDER BY ChangedAt;
            """;

        using var conn  = _db.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(sql, new { OrderId = orderId }, commandTimeout: Timeout, cancellationToken: ct));

        var header = await multi.ReadSingleOrDefaultAsync<OrderDetailHeader>();
        if (header is null) return null;

        var items   = (await multi.ReadAsync<OrderItemSnapshot>()).ToList();
        var history = (await multi.ReadAsync<OrderStatusHistoryDto>()).ToList();

        return new AdminOrderDetailDto(
            header.OrderId,
            header.OrderNumber,
            header.CustomerEmail,
            header.CustomerName,
            header.Status,
            header.PaymentMethod,
            header.PaymentStatus,
            header.DeliveryAddress,
            items,
            header.SubTotal,
            header.DeliveryFee,
            header.DiscountAmount,
            header.TotalAmount,
            header.Notes,
            history);
    }

    public async Task<bool> UpdateOrderStatusAsync(
        int orderId, string newStatus, string adminUserId, string? notes, CancellationToken ct = default)
    {
        const string getCurrentSql = "SELECT Status FROM Orders WHERE Id = @OrderId;";

        using var conn = _db.CreateConnection();
        var currentStatus = await conn.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(getCurrentSql, new { OrderId = orderId }, commandTimeout: Timeout, cancellationToken: ct));

        if (currentStatus is null) return false;

        // Validate transition
        if (!AllowedTransitions.TryGetValue(currentStatus, out var allowed)
            || !allowed.Any(s => string.Equals(s, newStatus, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Invalid status transition {From} -> {To} for order {Id}", currentStatus, newStatus, orderId);
            return false;
        }

        using var tran = conn.BeginTransaction();
        try
        {
            const string updateSql = """
                UPDATE Orders SET Status = @NewStatus, UpdatedAt = SYSUTCDATETIME() WHERE Id = @OrderId;
                """;
            await conn.ExecuteAsync(
                new CommandDefinition(updateSql, new { NewStatus = newStatus, OrderId = orderId },
                    transaction: tran, commandTimeout: Timeout, cancellationToken: ct));

            const string historySql = """
                INSERT INTO OrderStatusHistory (OrderId, Status, Notes, ChangedAt, ChangedBy)
                VALUES (@OrderId, @Status, @Notes, SYSUTCDATETIME(), @ChangedBy);
                """;
            await conn.ExecuteAsync(
                new CommandDefinition(historySql,
                    new { OrderId = orderId, Status = newStatus, Notes = notes, ChangedBy = adminUserId },
                    transaction: tran, commandTimeout: Timeout, cancellationToken: ct));

            // Restore inventory if cancelling an order that had reserved stock
            if (string.Equals(newStatus, "Cancelled", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(currentStatus, "Pending", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(currentStatus, "Confirmed", StringComparison.OrdinalIgnoreCase)))
            {
                const string restoreSql = """
                    UPDATE i SET
                        i.QuantityOnHand    = i.QuantityOnHand    + oi.Quantity,
                        i.ReservedQuantity  = i.ReservedQuantity  - oi.Quantity,
                        i.UpdatedAt         = SYSUTCDATETIME()
                    FROM Inventory i
                    JOIN OrderItems oi ON oi.ProductId = i.ProductId
                    WHERE oi.OrderId = @OrderId;
                    """;
                await conn.ExecuteAsync(
                    new CommandDefinition(restoreSql, new { OrderId = orderId },
                        transaction: tran, commandTimeout: Timeout, cancellationToken: ct));
            }

            tran.Commit();
            return true;
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }

    // ── Products ──────────────────────────────────────────────────────────────

    public async Task<(IReadOnlyList<AdminProductDto> Products, int TotalCount)> GetProductsAsync(
        string? search, int? categoryId, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var where = new StringBuilder("WHERE 1=1");
        if (!string.IsNullOrWhiteSpace(search))
            where.Append(" AND (p.Name LIKE @Search OR p.SKU LIKE @Search)");
        if (categoryId.HasValue)
            where.Append(" AND p.CategoryId = @CategoryId");
        if (isActive.HasValue)
            where.Append(" AND p.IsActive = @IsActive");

        var sql = $"""
            SELECT COUNT(*) FROM Products p {where};

            SELECT
                p.Id, p.Name, p.SKU, p.Description,
                p.CategoryId, c.Name AS CategoryName,
                p.UnitCode, p.UnitLabel,
                p.Price, p.OfferPrice, p.IsFeatured, p.IsActive,
                p.ImagePath,
                ISNULL(i.QuantityOnHand, 0)    AS QuantityOnHand,
                ISNULL(i.ReservedQuantity, 0)  AS ReservedQuantity,
                ISNULL(i.LowStockThreshold, 0) AS LowStockThreshold,
                p.CreatedAt, p.UpdatedAt
            FROM Products p
            LEFT JOIN Categories c ON c.Id = p.CategoryId
            LEFT JOIN Inventory  i ON i.ProductId = p.Id
            {where}
            ORDER BY p.UpdatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var parameters = new
        {
            Search     = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%",
            CategoryId = categoryId,
            IsActive   = isActive,
            Offset     = (page - 1) * pageSize,
            PageSize   = pageSize
        };

        using var conn  = _db.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, commandTimeout: Timeout, cancellationToken: ct));

        var total    = await multi.ReadSingleAsync<int>();
        var products = (await multi.ReadAsync<AdminProductDto>()).ToList();

        return (products, total);
    }

    public async Task<AdminProductDto?> GetProductByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                p.Id, p.Name, p.SKU, p.Description,
                p.CategoryId, c.Name AS CategoryName,
                p.UnitCode, p.UnitLabel,
                p.Price, p.OfferPrice, p.IsFeatured, p.IsActive,
                p.ImagePath,
                ISNULL(i.QuantityOnHand, 0)    AS QuantityOnHand,
                ISNULL(i.ReservedQuantity, 0)  AS ReservedQuantity,
                ISNULL(i.LowStockThreshold, 0) AS LowStockThreshold,
                p.CreatedAt, p.UpdatedAt
            FROM Products p
            LEFT JOIN Categories c ON c.Id = p.CategoryId
            LEFT JOIN Inventory  i ON i.ProductId = p.Id
            WHERE p.Id = @Id;
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<AdminProductDto>(
            new CommandDefinition(sql, new { Id = id }, commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<int> CreateProductAsync(CreateProductRequest request, string? imagePath, CancellationToken ct = default)
    {
        const string insertProduct = """
            INSERT INTO Products
                (Name, SKU, Description, CategoryId, UnitCode, UnitLabel,
                 Price, OfferPrice, IsFeatured, IsActive, ImagePath,
                 CreatedAt, UpdatedAt)
            OUTPUT INSERTED.Id
            VALUES
                (@Name, @SKU, @Description, @CategoryId, @UnitCode, @UnitLabel,
                 @Price, @OfferPrice, @IsFeatured, 1, @ImagePath,
                 SYSUTCDATETIME(), SYSUTCDATETIME());
            """;
        const string insertInventory = """
            INSERT INTO Inventory (ProductId, QuantityOnHand, ReservedQuantity, LowStockThreshold, UpdatedAt)
            VALUES (@ProductId, @InitialStock, 0, @LowStockThreshold, SYSUTCDATETIME());
            """;

        using var conn = _db.CreateConnection();
        using var tran = conn.BeginTransaction();
        try
        {
            var productId = await conn.QuerySingleAsync<int>(
                new CommandDefinition(insertProduct,
                    new
                    {
                        request.Name, request.SKU, request.Description, request.CategoryId,
                        request.UnitCode, request.UnitLabel, request.Price, request.OfferPrice,
                        request.IsFeatured, ImagePath = imagePath
                    },
                    transaction: tran, commandTimeout: Timeout, cancellationToken: ct));

            await conn.ExecuteAsync(
                new CommandDefinition(insertInventory,
                    new { ProductId = productId, request.InitialStock, request.LowStockThreshold },
                    transaction: tran, commandTimeout: Timeout, cancellationToken: ct));

            tran.Commit();
            return productId;
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }

    public async Task<bool> UpdateProductAsync(int id, UpdateProductRequest request, string? newImagePath, CancellationToken ct = default)
    {
        var imageClause = newImagePath is not null ? ", ImagePath = @ImagePath" : string.Empty;
        var productSql = $"""
            UPDATE Products
            SET Name        = @Name,
                Description = @Description,
                CategoryId  = @CategoryId,
                UnitCode    = @UnitCode,
                UnitLabel   = @UnitLabel,
                Price       = @Price,
                OfferPrice  = @OfferPrice,
                IsFeatured  = @IsFeatured,
                IsActive    = @IsActive{imageClause},
                UpdatedAt   = SYSUTCDATETIME()
            WHERE Id = @Id;

            UPDATE Inventory
            SET LowStockThreshold = @LowStockThreshold,
                UpdatedAt         = SYSUTCDATETIME()
            WHERE ProductId = @Id;
            """;

        var parameters = new
        {
            Id                = id,
            request.Name,
            request.Description,
            request.CategoryId,
            request.UnitCode,
            request.UnitLabel,
            request.Price,
            request.OfferPrice,
            request.IsFeatured,
            request.IsActive,
            request.LowStockThreshold,
            ImagePath = newImagePath
        };

        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(productSql, parameters, commandTimeout: Timeout, cancellationToken: ct));

        return rows > 0;
    }

    public async Task<bool> DeactivateProductAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE Products SET IsActive = 0, UpdatedAt = SYSUTCDATETIME() WHERE Id = @Id;
            """;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, commandTimeout: Timeout, cancellationToken: ct));
        return rows > 0;
    }

    // ── Categories ────────────────────────────────────────────────────────────

    public async Task<(IReadOnlyList<AdminCategoryDto> Categories, int TotalCount)> GetCategoriesAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        const string sql = """
            SELECT COUNT(*) FROM Categories;

            SELECT
                c.Id, c.Name, c.Slug, c.Description,
                c.ImagePath, c.SortOrder, c.IsActive,
                (SELECT COUNT(*) FROM Products p WHERE p.CategoryId = c.Id AND p.IsActive = 1) AS ProductCount,
                c.UpdatedAt
            FROM Categories c
            ORDER BY c.SortOrder, c.Name
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var conn  = _db.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(sql,
                new { Offset = (page - 1) * pageSize, PageSize = pageSize },
                commandTimeout: Timeout, cancellationToken: ct));

        var total      = await multi.ReadSingleAsync<int>();
        var categories = (await multi.ReadAsync<AdminCategoryDto>()).ToList();

        return (categories, total);
    }

    public async Task<int> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO Categories (Name, Slug, Description, SortOrder, IsActive, CreatedAt, UpdatedAt)
            OUTPUT INSERTED.Id
            VALUES (@Name, @Slug, @Description, @SortOrder, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(
            new CommandDefinition(sql, request, commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<bool> UpdateCategoryAsync(int id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE Categories
            SET Name        = @Name,
                Description = @Description,
                SortOrder   = @SortOrder,
                IsActive    = @IsActive,
                UpdatedAt   = SYSUTCDATETIME()
            WHERE Id = @Id;
            """;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(sql,
                new { Id = id, request.Name, request.Description, request.SortOrder, request.IsActive },
                commandTimeout: Timeout, cancellationToken: ct));
        return rows > 0;
    }

    // ── Inventory ─────────────────────────────────────────────────────────────

    public async Task<(IReadOnlyList<InventoryItemDto> Items, int TotalCount)> GetInventoryAsync(
        string? search, bool? lowStock, int page, int pageSize, CancellationToken ct = default)
    {
        var where = new StringBuilder("WHERE p.IsActive = 1");
        if (!string.IsNullOrWhiteSpace(search))
            where.Append(" AND (p.Name LIKE @Search OR p.SKU LIKE @Search)");
        if (lowStock == true)
            where.Append(" AND (i.QuantityOnHand - i.ReservedQuantity) <= i.LowStockThreshold");

        var sql = $"""
            SELECT COUNT(*) FROM Inventory i JOIN Products p ON p.Id = i.ProductId {where};

            SELECT
                i.ProductId,
                p.Name              AS ProductName,
                p.SKU,
                i.QuantityOnHand,
                i.ReservedQuantity,
                i.LowStockThreshold,
                CAST(CASE WHEN (i.QuantityOnHand - i.ReservedQuantity) <= i.LowStockThreshold THEN 1 ELSE 0 END AS BIT) AS IsLowStock,
                i.UpdatedAt
            FROM Inventory i
            JOIN Products p ON p.Id = i.ProductId
            {where}
            ORDER BY IsLowStock DESC, p.Name
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var parameters = new
        {
            Search   = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%",
            Offset   = (page - 1) * pageSize,
            PageSize = pageSize
        };

        using var conn  = _db.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, commandTimeout: Timeout, cancellationToken: ct));

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<InventoryItemDto>()).ToList();

        return (items, total);
    }

    public async Task AdjustInventoryAsync(InventoryAdjustRequest request, string adminUserId, CancellationToken ct = default)
    {
        const string updateSql = """
            UPDATE Inventory
            SET QuantityOnHand = QuantityOnHand + @QuantityChange,
                UpdatedAt      = SYSUTCDATETIME()
            WHERE ProductId = @ProductId
              AND QuantityOnHand + @QuantityChange >= 0;
            """;
        // QuantityAfter is NOT NULL — fetch the new balance after update using OUTPUT
        const string transactionSql = """
            INSERT INTO InventoryTransactions
                (ProductId, TransactionType, QuantityChange, QuantityAfter, Notes, CreatedBy, CreatedAt)
            SELECT @ProductId, 'Adjustment', @QuantityChange,
                   QuantityOnHand,
                   @Reason, @AdminUserId, SYSUTCDATETIME()
            FROM   Inventory
            WHERE  ProductId = @ProductId;
            """;

        using var conn = _db.CreateConnection();
        using var tran = conn.BeginTransaction();
        try
        {
            var rows = await conn.ExecuteAsync(
                new CommandDefinition(updateSql,
                    new { request.ProductId, request.QuantityChange },
                    transaction: tran, commandTimeout: Timeout, cancellationToken: ct));

            if (rows == 0)
                throw new InvalidOperationException("Adjustment would result in negative inventory.");

            await conn.ExecuteAsync(
                new CommandDefinition(transactionSql,
                    new { request.ProductId, request.QuantityChange, request.Reason, AdminUserId = adminUserId },
                    transaction: tran, commandTimeout: Timeout, cancellationToken: ct));

            tran.Commit();
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<StoreSettingDto>> GetSettingsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT SettingKey AS [Key], SettingValue AS Value, DataType, Category, Description
            FROM StoreSettings
            ORDER BY Category, [Key];
            """;
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync<StoreSettingDto>(
            new CommandDefinition(sql, commandTimeout: Timeout, cancellationToken: ct));
        return rows.ToList().AsReadOnly();
    }

    public async Task UpdateSettingsAsync(UpdateSettingsRequest request, string adminUserId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE StoreSettings
            SET SettingValue = @Value,
                UpdatedAt    = SYSUTCDATETIME(),
                UpdatedBy    = @AdminUserId
            WHERE SettingKey = @Key;
            """;
        using var conn = _db.CreateConnection();
        foreach (var item in request.Settings)
        {
            await conn.ExecuteAsync(
                new CommandDefinition(sql,
                    new { item.Key, item.Value, AdminUserId = adminUserId },
                    commandTimeout: Timeout, cancellationToken: ct));
        }
    }

    // ── Helpers / Validation ──────────────────────────────────────────────────

    public async Task<bool> IsSkuAvailableAsync(string sku, int? excludeProductId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT CAST(CASE WHEN EXISTS (
                SELECT 1 FROM Products WHERE SKU = @SKU AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            ) THEN 0 ELSE 1 END AS BIT);
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<bool>(
            new CommandDefinition(sql, new { SKU = sku, ExcludeId = excludeProductId },
                commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<bool> IsCategorySlugAvailableAsync(string slug, int? excludeCategoryId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT CAST(CASE WHEN EXISTS (
                SELECT 1 FROM Categories WHERE Slug = @Slug AND (@ExcludeId IS NULL OR Id <> @ExcludeId)
            ) THEN 0 ELSE 1 END AS BIT);
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<bool>(
            new CommandDefinition(sql, new { Slug = slug, ExcludeId = excludeCategoryId },
                commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<bool> CategoryHasProductsAsync(int categoryId, CancellationToken ct = default)
    {
        const string sql = "SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM Products WHERE CategoryId = @CategoryId AND IsActive = 1) THEN 1 ELSE 0 END AS BIT);";
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<bool>(
            new CommandDefinition(sql, new { CategoryId = categoryId }, commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task LogAdminActionAsync(
        string adminUserId, string action, string? entityType, string? entityId, string? details,
        CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO AdminLogs (AdminUserId, Action, EntityType, EntityId, Details, CreatedAt)
            VALUES (@AdminUserId, @Action, @EntityType, @EntityId, @Details, SYSUTCDATETIME());
            """;
        try
        {
            using var conn = _db.CreateConnection();
            await conn.ExecuteAsync(
                new CommandDefinition(sql,
                    new { AdminUserId = adminUserId, Action = action, EntityType = entityType, EntityId = entityId, Details = details },
                    commandTimeout: Timeout, cancellationToken: ct));
        }
        catch (Exception ex)
        {
            // Audit log failure must never crash the request
            _logger.LogError(ex, "Failed to write admin audit log: {Action} by {UserId}", action, adminUserId);
        }
    }

    // ── Private projection types ──────────────────────────────────────────────

    private sealed record OrderCountRow(
        int TotalOrders, int PendingOrders, int ConfirmedOrders,
        int PreparingOrders, int OutForDeliveryOrders, int DeliveredOrders,
        decimal TodaySales, decimal WeekSales, decimal MonthSales);

    private sealed record OrderDetailHeader(
        int OrderId, string OrderNumber, string CustomerEmail, string CustomerName,
        string Status, string PaymentMethod, string PaymentStatus,
        string DeliveryAddress, decimal SubTotal, decimal DeliveryFee,
        decimal DiscountAmount, decimal TotalAmount, string? Notes);

    // ── Reports & Analytics Implementation ────────────────────────────────────

    private sealed record AnalyticsKPIRow(
        int TotalOrders, int CompletedOrders, int PendingOrders, int CancelledOrders,
        decimal GrossSales, decimal NetSales, decimal TotalDiscounts, decimal TotalDeliveryFees,
        decimal AverageOrderValue);

    private sealed record PaymentSummaryRow(
        int CodOrdersCount, decimal CodNetSales,
        int OnlineOrdersCount, decimal OnlineNetSales);

    private sealed record CustomerCountsRow(
        int NewCustomersCount, int RepeatCustomersCount);

    public async Task<AnalyticsDashboardDto> GetAnalyticsDashboardAsync(DateTime startDate, DateTime endDate, string? filterType, CancellationToken ct = default)
    {
        const string sql = """
            -- 1. KPI row
            SELECT
                COUNT(*) AS TotalOrders,
                ISNULL(SUM(CASE WHEN Status = 'Delivered' THEN 1 ELSE 0 END), 0) AS CompletedOrders,
                ISNULL(SUM(CASE WHEN Status = 'Pending' THEN 1 ELSE 0 END), 0) AS PendingOrders,
                ISNULL(SUM(CASE WHEN Status = 'Cancelled' THEN 1 ELSE 0 END), 0) AS CancelledOrders,
                ISNULL(SUM(CASE WHEN Status NOT IN ('Cancelled', 'Refunded') AND PaymentStatus != 'Failed' THEN SubTotal ELSE 0 END), 0) AS GrossSales,
                ISNULL(SUM(CASE WHEN Status NOT IN ('Cancelled', 'Refunded') AND PaymentStatus != 'Failed' THEN TotalAmount ELSE 0 END), 0) AS NetSales,
                ISNULL(SUM(CASE WHEN Status NOT IN ('Cancelled', 'Refunded') AND PaymentStatus != 'Failed' THEN (DiscountAmount + CouponDiscount) ELSE 0 END), 0) AS TotalDiscounts,
                ISNULL(SUM(CASE WHEN Status NOT IN ('Cancelled', 'Refunded') AND PaymentStatus != 'Failed' THEN DeliveryFee ELSE 0 END), 0) AS TotalDeliveryFees,
                CASE
                    WHEN COUNT(CASE WHEN Status NOT IN ('Cancelled', 'Refunded') AND PaymentStatus != 'Failed' THEN 1 END) > 0
                    THEN ISNULL(SUM(CASE WHEN Status NOT IN ('Cancelled', 'Refunded') AND PaymentStatus != 'Failed' THEN TotalAmount ELSE 0 END), 0) /
                         COUNT(CASE WHEN Status NOT IN ('Cancelled', 'Refunded') AND PaymentStatus != 'Failed' THEN 1 END)
                    ELSE 0
                END AS AverageOrderValue
            FROM Orders
            WHERE CreatedAt >= @StartDate AND CreatedAt <= @EndDate;

            -- 2. Payment Summary Row
            SELECT
                ISNULL(SUM(CASE WHEN PaymentMethod = 'COD' THEN 1 ELSE 0 END), 0) AS CodOrdersCount,
                ISNULL(SUM(CASE WHEN PaymentMethod = 'COD' THEN TotalAmount ELSE 0 END), 0) AS CodNetSales,
                ISNULL(SUM(CASE WHEN PaymentMethod != 'COD' THEN 1 ELSE 0 END), 0) AS OnlineOrdersCount,
                ISNULL(SUM(CASE WHEN PaymentMethod != 'COD' THEN TotalAmount ELSE 0 END), 0) AS OnlineNetSales
            FROM Orders
            WHERE CreatedAt >= @StartDate AND CreatedAt <= @EndDate
              AND Status NOT IN ('Cancelled', 'Refunded') AND PaymentStatus != 'Failed';

            -- 3. Stock counts (Low Stock, Out of Stock)
            SELECT COUNT(*) FROM Inventory i
            JOIN Products p ON p.Id = i.ProductId
            WHERE p.IsActive = 1 AND (i.QuantityOnHand - i.ReservedQuantity) <= i.LowStockThreshold AND i.QuantityOnHand > 0;

            SELECT COUNT(*) FROM Inventory i
            JOIN Products p ON p.Id = i.ProductId
            WHERE p.IsActive = 1 AND i.QuantityOnHand = 0;

            -- 4. New vs Repeat Customers Count
            SELECT
                ISNULL(SUM(CASE WHEN PrecedingOrdersCount = 0 THEN 1 ELSE 0 END), 0) AS NewCustomersCount,
                ISNULL(SUM(CASE WHEN PrecedingOrdersCount > 0 THEN 1 ELSE 0 END), 0) AS RepeatCustomersCount
            FROM (
                SELECT
                    o.UserId,
                    o.Id AS OrderId,
                    (
                        SELECT COUNT(*)
                        FROM Orders prev
                        WHERE prev.UserId = o.UserId
                          AND prev.CreatedAt < o.CreatedAt
                          AND prev.Status NOT IN ('Cancelled', 'Refunded')
                    ) AS PrecedingOrdersCount
                FROM Orders o
                WHERE o.CreatedAt >= @StartDate AND o.CreatedAt <= @EndDate
                  AND o.Status NOT IN ('Cancelled', 'Refunded')
            ) CustOrders;

            -- 5. Daily Sales Trends
            SELECT
                CAST(CreatedAt AS DATE) AS Date,
                ISNULL(SUM(TotalAmount), 0) AS NetSales,
                COUNT(*) AS OrderCount
            FROM Orders
            WHERE CreatedAt >= @StartDate AND CreatedAt <= @EndDate
              AND Status NOT IN ('Cancelled', 'Refunded') AND PaymentStatus != 'Failed'
            GROUP BY CAST(CreatedAt AS DATE)
            ORDER BY Date ASC;

            -- 6. Sales By Category
            SELECT
                c.Id AS CategoryId,
                c.Name AS CategoryName,
                ISNULL(SUM(oi.LineTotal), 0) AS NetSales,
                CAST(ISNULL(SUM(oi.Quantity), 0) AS INT) AS QuantitySold
            FROM OrderItems oi
            JOIN Orders o ON oi.OrderId = o.Id
            JOIN Products p ON oi.ProductId = p.Id
            JOIN Categories c ON p.CategoryId = c.Id
            WHERE o.CreatedAt >= @StartDate AND o.CreatedAt <= @EndDate
              AND o.Status NOT IN ('Cancelled', 'Refunded') AND o.PaymentStatus != 'Failed'
            GROUP BY c.Id, c.Name
            ORDER BY NetSales DESC;

            -- 7. Payment Method Distribution
            SELECT
                PaymentMethod,
                COUNT(*) AS OrderCount,
                ISNULL(SUM(TotalAmount), 0) AS NetSales
            FROM Orders
            WHERE CreatedAt >= @StartDate AND CreatedAt <= @EndDate
              AND Status NOT IN ('Cancelled', 'Refunded') AND PaymentStatus != 'Failed'
            GROUP BY PaymentMethod
            ORDER BY NetSales DESC;

            -- 8. Top Selling Products
            SELECT TOP 10
                oi.ProductId,
                p.Name AS ProductName,
                p.SKU,
                c.Name AS CategoryName,
                ISNULL(SUM(oi.Quantity), 0) AS QuantitySold,
                ISNULL(SUM(oi.LineTotal), 0) AS NetSales,
                CASE WHEN SUM(oi.Quantity) > 0 THEN SUM(oi.LineTotal) / SUM(oi.Quantity) ELSE 0 END AS AverageUnitPrice
            FROM OrderItems oi
            JOIN Orders o ON oi.OrderId = o.Id
            JOIN Products p ON oi.ProductId = p.Id
            JOIN Categories c ON p.CategoryId = c.Id
            WHERE o.CreatedAt >= @StartDate AND o.CreatedAt <= @EndDate
              AND o.Status NOT IN ('Cancelled', 'Refunded') AND o.PaymentStatus != 'Failed'
            GROUP BY oi.ProductId, p.Name, p.SKU, c.Name
            ORDER BY NetSales DESC, QuantitySold DESC;
            """;

        using var conn = _db.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(sql, new { StartDate = startDate, EndDate = endDate }, commandTimeout: Timeout, cancellationToken: ct));

        var kpis = await multi.ReadSingleAsync<AnalyticsKPIRow>();
        var payment = await multi.ReadSingleAsync<PaymentSummaryRow>();
        var lowStock = await multi.ReadSingleAsync<int>();
        var outOfStock = await multi.ReadSingleAsync<int>();
        var customers = await multi.ReadSingleAsync<CustomerCountsRow>();
        var trends = (await multi.ReadAsync<DailySalesTrendDto>()).ToList();
        var categories = (await multi.ReadAsync<CategorySalesDto>()).ToList();
        var distributions = (await multi.ReadAsync<PaymentMethodDistributionDto>()).ToList();
        var products = (await multi.ReadAsync<ProductSalesPerformanceDto>()).ToList();

        var filter = new ReportFilterDto(startDate, endDate, filterType);

        return new AnalyticsDashboardDto(
            filter,
            kpis.GrossSales,
            kpis.NetSales,
            kpis.TotalDiscounts,
            kpis.TotalDeliveryFees,
            kpis.TotalOrders,
            kpis.CompletedOrders,
            kpis.PendingOrders,
            kpis.CancelledOrders,
            kpis.AverageOrderValue,
            payment.CodOrdersCount,
            payment.CodNetSales,
            payment.OnlineOrdersCount,
            payment.OnlineNetSales,
            lowStock,
            outOfStock,
            customers.NewCustomersCount,
            customers.RepeatCustomersCount,
            trends,
            categories,
            distributions,
            products
        );
    }

    public async Task<IReadOnlyList<NoSalesProductDto>> GetNoSalesProductsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.Id AS ProductId, p.SKU, p.Name AS ProductName, c.Name AS CategoryName, p.Price, ISNULL(i.QuantityOnHand, 0) AS QuantityOnHand
            FROM Products p
            JOIN Categories c ON p.CategoryId = c.Id
            LEFT JOIN Inventory i ON p.Id = i.ProductId
            WHERE p.IsActive = 1
              AND p.Id NOT IN (
                  SELECT distinct oi.ProductId
                  FROM OrderItems oi
                  JOIN Orders o ON oi.OrderId = o.Id
                  WHERE o.CreatedAt >= @StartDate AND o.CreatedAt <= @EndDate
                    AND o.Status NOT IN ('Cancelled', 'Refunded')
                    AND o.PaymentStatus != 'Failed'
              )
            ORDER BY ProductName;
            """;
        using var conn = _db.CreateConnection();
        var items = await conn.QueryAsync<NoSalesProductDto>(
            new CommandDefinition(sql, new { StartDate = startDate, EndDate = endDate }, commandTimeout: Timeout, cancellationToken: ct));
        return items.ToList();
    }

    public async Task<IReadOnlyList<InventoryQuantityDto>> GetInventoryQuantitiesReportAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.Id AS ProductId, p.Name AS ProductName, p.SKU, c.Name AS CategoryName,
                   ISNULL(i.QuantityOnHand, 0) AS QuantityOnHand,
                   ISNULL(i.ReservedQuantity, 0) AS ReservedQuantity,
                   ISNULL(i.LowStockThreshold, 0) AS LowStockThreshold,
                   CAST(CASE WHEN (i.QuantityOnHand - i.ReservedQuantity) <= i.LowStockThreshold AND i.QuantityOnHand > 0 THEN 1 ELSE 0 END AS BIT) AS IsLowStock,
                   CAST(CASE WHEN i.QuantityOnHand = 0 THEN 1 ELSE 0 END AS BIT) AS IsOutOfStock
            FROM Products p
            JOIN Categories c ON p.CategoryId = c.Id
            LEFT JOIN Inventory i ON p.Id = i.ProductId
            ORDER BY c.Name, p.Name;
            """;
        using var conn = _db.CreateConnection();
        var items = await conn.QueryAsync<InventoryQuantityDto>(
            new CommandDefinition(sql, commandTimeout: Timeout, cancellationToken: ct));
        return items.ToList();
    }

    public async Task<IReadOnlyList<InventoryAdjustmentDto>> GetInventoryAdjustmentsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        const string sql = """
            SELECT t.Id, t.ProductId, p.Name AS ProductName, p.SKU, t.TransactionType,
                   t.QuantityChange, t.QuantityAfter, t.Notes, t.CreatedAt, t.CreatedBy
            FROM InventoryTransactions t
            JOIN Products p ON t.ProductId = p.Id
            WHERE t.TransactionType = 'Adjustment' AND t.CreatedAt >= @StartDate AND t.CreatedAt <= @EndDate
            ORDER BY t.CreatedAt DESC;
            """;
        using var conn = _db.CreateConnection();
        var items = await conn.QueryAsync<InventoryAdjustmentDto>(
            new CommandDefinition(sql, new { StartDate = startDate, EndDate = endDate }, commandTimeout: Timeout, cancellationToken: ct));
        return items.ToList();
    }

    public async Task<IReadOnlyList<OrderReportDto>> GetOrdersReportAsync(DateTime startDate, DateTime endDate, string? status, CancellationToken ct = default)
    {
        const string sql = """
            SELECT o.Id AS OrderId, o.OrderNumber, u.DisplayName AS CustomerName, u.Email AS CustomerEmail,
                   o.CreatedAt, o.Status, o.PaymentMethod, o.PaymentStatus, o.TotalAmount
            FROM Orders o
            JOIN AspNetUsers u ON o.UserId = u.Id
            WHERE o.CreatedAt >= @StartDate AND o.CreatedAt <= @EndDate
              AND (@Status IS NULL OR o.Status = @Status)
            ORDER BY o.CreatedAt DESC;
            """;
        using var conn = _db.CreateConnection();
        var items = await conn.QueryAsync<OrderReportDto>(
            new CommandDefinition(sql, new { StartDate = startDate, EndDate = endDate, Status = status }, commandTimeout: Timeout, cancellationToken: ct));
        return items.ToList();
    }

    public async Task<IReadOnlyList<PaymentFailureRefundDto>> GetPaymentFailuresRefundsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        const string sql = """
            SELECT o.Id AS OrderId, o.OrderNumber, o.TotalAmount, o.Status AS OrderStatus, o.PaymentStatus, o.PaymentMethod, o.CreatedAt,
                   p.GatewayName, p.Status AS PaymentStatusDetail, p.FailureReason
            FROM Orders o
            LEFT JOIN Payments p ON o.Id = p.OrderId
            WHERE (o.Status = 'Refunded' OR o.PaymentStatus IN ('Failed', 'Refunded') OR p.Status IN ('Failed', 'Refunded'))
              AND o.CreatedAt >= @StartDate AND o.CreatedAt <= @EndDate
            ORDER BY o.CreatedAt DESC;
            """;
        using var conn = _db.CreateConnection();
        var items = await conn.QueryAsync<PaymentFailureRefundDto>(
            new CommandDefinition(sql, new { StartDate = startDate, EndDate = endDate }, commandTimeout: Timeout, cancellationToken: ct));
        return items.ToList();
    }

    public async Task<IReadOnlyList<RepeatCustomerDto>> GetRepeatCustomersReportAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        const string sql = """
            SELECT u.Id AS CustomerId, u.DisplayName AS CustomerName, u.Email AS CustomerEmail,
                   COUNT(o.Id) AS TotalOrders,
                   SUM(o.TotalAmount) AS TotalSpent,
                   AVG(o.TotalAmount) AS AverageOrderValue,
                   MIN(o.CreatedAt) AS FirstOrderDate,
                   MAX(o.CreatedAt) AS LastOrderDate
            FROM AspNetUsers u
            JOIN Orders o ON u.Id = o.UserId
            WHERE o.Status NOT IN ('Cancelled', 'Refunded') AND o.PaymentStatus != 'Failed'
              AND o.CreatedAt >= @StartDate AND o.CreatedAt <= @EndDate
            GROUP BY u.Id, u.DisplayName, u.Email
            ORDER BY TotalOrders DESC, TotalSpent DESC;
            """;
        using var conn = _db.CreateConnection();
        var items = await conn.QueryAsync<RepeatCustomerDto>(
            new CommandDefinition(sql, new { StartDate = startDate, EndDate = endDate }, commandTimeout: Timeout, cancellationToken: ct));
        return items.ToList();
    }
}
