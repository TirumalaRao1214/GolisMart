using System.Text;
using Dapper;
using GOLISMart.API.Data;
using GOLISMart.API.DTOs;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;

namespace GOLISMart.API.Repositories.Implementations;

public class DeliveryManagementRepository : IDeliveryManagementRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<DeliveryManagementRepository> _logger;
    private const int Timeout = 30;

    // Valid status transitions that a delivery person is allowed to perform
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DeliveryTransitions =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Confirmed"]      = new[] { "PickedUp" },
            ["Processing"]     = new[] { "PickedUp" },
            ["PickedUp"]       = new[] { "OutForDelivery", "DeliveryFailed" },
            ["OutForDelivery"] = new[] { "Delivered", "DeliveryFailed" },
        };

    // Statuses eligible for assignment (order must be in one of these to be assigned)
    private static readonly HashSet<string> AssignableStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Confirmed", "Processing"
        };

    public DeliveryManagementRepository(IDbConnectionFactory db, ILogger<DeliveryManagementRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Delivery Personnel CRUD ───────────────────────────────────────────────

    public async Task<(IReadOnlyList<DeliveryPersonnelDto> Items, int TotalCount)> GetPersonnelAsync(
        string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var where = new StringBuilder("WHERE 1=1");
        if (!string.IsNullOrWhiteSpace(search))
            where.Append(" AND (dp.FullName LIKE @Search OR dp.ContactNumber LIKE @Search)");
        if (isActive.HasValue)
            where.Append(" AND dp.IsActive = @IsActive");

        var sql = $"""
            SELECT COUNT(*) FROM DeliveryPersonnel dp {where};

            SELECT
                dp.Id,
                dp.UserId,
                dp.FullName,
                dp.ContactNumber,
                dp.IsActive,
                dp.Notes,
                dp.CreatedAt,
                dp.UpdatedAt,
                (SELECT COUNT(*) FROM DeliveryAssignments da
                 WHERE da.DeliveryPersonnelId = dp.Id AND da.IsActive = 1) AS ActiveAssignmentCount
            FROM DeliveryPersonnel dp
            {where}
            ORDER BY dp.FullName
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var parameters = new
        {
            Search   = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%",
            IsActive = isActive,
            Offset   = (page - 1) * pageSize,
            PageSize = pageSize
        };

        using var conn  = _db.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, commandTimeout: Timeout, cancellationToken: ct));

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<DeliveryPersonnelDto>()).ToList();
        return (items, total);
    }

    public async Task<DeliveryPersonnelDto?> GetPersonnelByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                dp.Id, dp.UserId, dp.FullName, dp.ContactNumber,
                dp.IsActive, dp.Notes, dp.CreatedAt, dp.UpdatedAt,
                (SELECT COUNT(*) FROM DeliveryAssignments da
                 WHERE da.DeliveryPersonnelId = dp.Id AND da.IsActive = 1) AS ActiveAssignmentCount
            FROM DeliveryPersonnel dp
            WHERE dp.Id = @Id;
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<DeliveryPersonnelDto>(
            new CommandDefinition(sql, new { Id = id }, commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<DeliveryPersonnel?> GetPersonnelByUserIdAsync(string userId, CancellationToken ct = default)
    {
        const string sql = "SELECT * FROM DeliveryPersonnel WHERE UserId = @UserId;";
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<DeliveryPersonnel>(
            new CommandDefinition(sql, new { UserId = userId }, commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<int> CreatePersonnelAsync(CreateDeliveryPersonnelRequest request, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO DeliveryPersonnel (FullName, ContactNumber, UserId, Notes, IsActive, CreatedAt, UpdatedAt)
            OUTPUT INSERTED.Id
            VALUES (@FullName, @ContactNumber, @UserId, @Notes, 1, SYSUTCDATETIME(), SYSUTCDATETIME());
            """;
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(
            new CommandDefinition(sql,
                new { request.FullName, request.ContactNumber, request.UserId, request.Notes },
                commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<bool> UpdatePersonnelAsync(int id, UpdateDeliveryPersonnelRequest request, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE DeliveryPersonnel
            SET FullName      = @FullName,
                ContactNumber = @ContactNumber,
                UserId        = @UserId,
                Notes         = @Notes,
                IsActive      = @IsActive,
                UpdatedAt     = SYSUTCDATETIME()
            WHERE Id = @Id;
            """;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(sql,
                new { Id = id, request.FullName, request.ContactNumber, request.UserId, request.Notes, request.IsActive },
                commandTimeout: Timeout, cancellationToken: ct));
        return rows > 0;
    }

    public async Task<int> GetActiveAssignmentCountAsync(int personnelId, CancellationToken ct = default)
    {
        const string sql = "SELECT COUNT(*) FROM DeliveryAssignments WHERE DeliveryPersonnelId = @PersonnelId AND IsActive = 1;";
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<int>(
            new CommandDefinition(sql, new { PersonnelId = personnelId }, commandTimeout: Timeout, cancellationToken: ct));
    }

    // ── Delivery Assignment ───────────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> AssignOrderAsync(
        int orderId, int personnelId, string adminUserId, string? notes, CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // Validate order status
        var currentStatus = await conn.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT Status FROM Orders WHERE Id = @OrderId;",
                new { OrderId = orderId }, commandTimeout: Timeout, cancellationToken: ct));

        if (currentStatus is null)
            return (false, "Order not found.");

        if (!AssignableStatuses.Contains(currentStatus))
            return (false, $"Order cannot be assigned in status '{currentStatus}'. Must be Confirmed or Processing.");

        // Check personnel is active
        var personnelActive = await conn.QuerySingleOrDefaultAsync<bool?>(
            new CommandDefinition("SELECT IsActive FROM DeliveryPersonnel WHERE Id = @Id;",
                new { Id = personnelId }, commandTimeout: Timeout, cancellationToken: ct));

        if (personnelActive is null)
            return (false, "Delivery personnel not found.");
        if (!personnelActive.Value)
            return (false, "Delivery personnel is inactive.");

        using var tran = conn.BeginTransaction();
        try
        {
            // Deactivate any existing active assignment for this order
            const string unassignSql = """
                UPDATE DeliveryAssignments
                SET IsActive        = 0,
                    UnassignedAt    = SYSUTCDATETIME(),
                    UnassignedBy    = @AdminUserId,
                    UnassignedReason = 'Reassigned'
                WHERE OrderId = @OrderId AND IsActive = 1;
                """;
            await conn.ExecuteAsync(
                new CommandDefinition(unassignSql, new { OrderId = orderId, AdminUserId = adminUserId },
                    transaction: tran, commandTimeout: Timeout, cancellationToken: ct));

            // Create new active assignment
            const string assignSql = """
                INSERT INTO DeliveryAssignments
                    (OrderId, DeliveryPersonnelId, AssignedBy, AssignedAt, IsActive)
                VALUES (@OrderId, @PersonnelId, @AdminUserId, SYSUTCDATETIME(), 1);
                """;
            await conn.ExecuteAsync(
                new CommandDefinition(assignSql,
                    new { OrderId = orderId, PersonnelId = personnelId, AdminUserId = adminUserId },
                    transaction: tran, commandTimeout: Timeout, cancellationToken: ct));

            // Add to order status history
            const string historySql = """
                INSERT INTO OrderStatusHistory (OrderId, Status, Notes, ChangedAt, ChangedBy)
                SELECT @OrderId, Status,
                       COALESCE(@Notes, 'Delivery personnel assigned'),
                       SYSUTCDATETIME(), @AdminUserId
                FROM Orders WHERE Id = @OrderId;
                """;
            await conn.ExecuteAsync(
                new CommandDefinition(historySql,
                    new { OrderId = orderId, Notes = notes, AdminUserId = adminUserId },
                    transaction: tran, commandTimeout: Timeout, cancellationToken: ct));

            tran.Commit();
            return (true, null);
        }
        catch (Exception ex)
        {
            tran.Rollback();
            _logger.LogError(ex, "AssignOrder failed for orderId={OrderId}, personnelId={PersonnelId}", orderId, personnelId);
            return (false, "Assignment failed due to an internal error.");
        }
    }

    public async Task<(bool Success, string? Error)> UnassignOrderAsync(
        int orderId, string adminUserId, string reason, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE DeliveryAssignments
            SET IsActive         = 0,
                UnassignedAt     = SYSUTCDATETIME(),
                UnassignedBy     = @AdminUserId,
                UnassignedReason = @Reason
            WHERE OrderId = @OrderId AND IsActive = 1;
            """;
        using var conn = _db.CreateConnection();
        var rows = await conn.ExecuteAsync(
            new CommandDefinition(sql,
                new { OrderId = orderId, AdminUserId = adminUserId, Reason = reason },
                commandTimeout: Timeout, cancellationToken: ct));
        return rows > 0
            ? (true, null)
            : (false, "No active assignment found for this order.");
    }

    // ── Admin delivery list ───────────────────────────────────────────────────

    public async Task<(IReadOnlyList<AdminDeliveryOrderListDto> Orders, int TotalCount)> GetDeliveryOrdersAsync(
        string? search, string? status, DateTime? from, DateTime? to,
        string? pincode, int? personnelId,
        int page, int pageSize, CancellationToken ct = default)
    {
        var where = new StringBuilder("WHERE 1=1");
        if (!string.IsNullOrWhiteSpace(search))
            where.Append(" AND (o.OrderNumber LIKE @Search OR u.Email LIKE @Search OR u.DisplayName LIKE @Search)");
        if (!string.IsNullOrWhiteSpace(status))
            where.Append(" AND o.Status = @Status");
        if (from.HasValue)
            where.Append(" AND o.CreatedAt >= @From");
        if (to.HasValue)
            where.Append(" AND o.CreatedAt <= @To");
        if (!string.IsNullOrWhiteSpace(pincode))
            where.Append(" AND o.ShipToPincode = @Pincode");
        if (personnelId.HasValue)
            where.Append(" AND da.DeliveryPersonnelId = @PersonnelId AND da.IsActive = 1");

        var sql = $"""
            SELECT COUNT(*)
            FROM Orders o
            JOIN AspNetUsers u ON u.Id = o.UserId
            LEFT JOIN DeliveryAssignments da ON da.OrderId = o.Id AND da.IsActive = 1
            {where};

            SELECT
                o.Id            AS OrderId,
                o.OrderNumber,
                o.CreatedAt     AS OrderDate,
                ISNULL(u.DisplayName, u.Email) AS CustomerName,
                u.Email         AS CustomerEmail,
                ISNULL(u.PhoneNumber, '')       AS CustomerPhone,
                TRIM(o.ShipToAddressLine1
                    + CASE WHEN ISNULL(o.ShipToAddressLine2,'') <> '' THEN ', ' + o.ShipToAddressLine2 ELSE '' END
                    + ', ' + o.ShipToCity + ' - ' + o.ShipToPincode
                    + ', ' + o.ShipToState) AS DeliveryAddress,
                o.ShipToPincode,
                o.TotalAmount,
                o.PaymentMethod,
                o.PaymentStatus,
                o.Status,
                da.DeliveryPersonnelId AS AssignedPersonnelId,
                dp.FullName         AS AssignedPersonnelName,
                da.AssignedAt,
                (SELECT COUNT(*) FROM OrderItems oi WHERE oi.OrderId = o.Id) AS ItemCount
            FROM Orders o
            JOIN AspNetUsers u ON u.Id = o.UserId
            LEFT JOIN DeliveryAssignments da ON da.OrderId = o.Id AND da.IsActive = 1
            LEFT JOIN DeliveryPersonnel dp  ON dp.Id = da.DeliveryPersonnelId
            {where}
            ORDER BY o.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var parameters = new
        {
            Search      = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%",
            Status      = status,
            From        = from,
            To          = to,
            Pincode     = pincode,
            PersonnelId = personnelId,
            Offset      = (page - 1) * pageSize,
            PageSize    = pageSize
        };

        using var conn  = _db.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(sql, parameters, commandTimeout: Timeout, cancellationToken: ct));

        var total  = await multi.ReadSingleAsync<int>();
        var orders = (await multi.ReadAsync<AdminDeliveryOrderListDto>()).ToList();
        return (orders, total);
    }

    public async Task<AdminDeliveryOrderDetailDto?> GetDeliveryOrderDetailAsync(int orderId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                o.Id            AS OrderId,
                o.OrderNumber,
                o.CreatedAt     AS OrderDate,
                ISNULL(u.DisplayName, u.Email) AS CustomerName,
                u.Email         AS CustomerEmail,
                ISNULL(u.PhoneNumber, '')       AS CustomerPhone,
                o.ShipToAddressLine1,
                o.ShipToAddressLine2,
                o.ShipToCity,
                o.ShipToState,
                o.ShipToPincode,
                o.SubTotal, o.DeliveryFee, o.DiscountAmount, o.TotalAmount,
                o.PaymentMethod, o.PaymentStatus, o.Status, o.Notes,
                da.DeliveryPersonnelId AS AssignedPersonnelId,
                dp.FullName            AS AssignedPersonnelName,
                dp.ContactNumber       AS AssignedPersonnelPhone,
                da.AssignedAt
            FROM Orders o
            JOIN AspNetUsers u ON u.Id = o.UserId
            LEFT JOIN DeliveryAssignments da ON da.OrderId = o.Id AND da.IsActive = 1
            LEFT JOIN DeliveryPersonnel dp  ON dp.Id = da.DeliveryPersonnelId
            WHERE o.Id = @OrderId;

            SELECT oi.ProductId, oi.ProductName, oi.ProductSKU, oi.UnitLabel,
                   oi.Quantity, oi.UnitPrice, oi.OfferPrice, oi.EffectivePrice, oi.LineTotal
            FROM OrderItems oi WHERE oi.OrderId = @OrderId;

            SELECT Status, Notes, ChangedAt FROM OrderStatusHistory
            WHERE OrderId = @OrderId ORDER BY ChangedAt;

            SELECT
                da.DeliveryPersonnelId,
                dp.FullName     AS PersonnelName,
                da.AssignedAt,
                da.UnassignedAt,
                da.UnassignedReason,
                da.IsActive
            FROM DeliveryAssignments da
            JOIN DeliveryPersonnel dp ON dp.Id = da.DeliveryPersonnelId
            WHERE da.OrderId = @OrderId
            ORDER BY da.AssignedAt DESC;
            """;

        using var conn  = _db.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(sql, new { OrderId = orderId }, commandTimeout: Timeout, cancellationToken: ct));

        var header = await multi.ReadSingleOrDefaultAsync<OrderDetailHeaderRow>();
        if (header is null) return null;

        var items      = (await multi.ReadAsync<OrderItemSnapshot>()).ToList();
        var history    = (await multi.ReadAsync<OrderStatusHistoryDto>()).ToList();
        var assignments = (await multi.ReadAsync<DeliveryAssignmentHistoryDto>()).ToList();

        return new AdminDeliveryOrderDetailDto(
            header.OrderId, header.OrderNumber, header.OrderDate,
            header.CustomerName, header.CustomerEmail, header.CustomerPhone,
            header.ShipToAddressLine1, header.ShipToAddressLine2,
            header.ShipToCity, header.ShipToState, header.ShipToPincode,
            header.SubTotal, header.DeliveryFee, header.DiscountAmount, header.TotalAmount,
            header.PaymentMethod, header.PaymentStatus, header.Status, header.Notes,
            header.AssignedPersonnelId, header.AssignedPersonnelName, header.AssignedPersonnelPhone,
            header.AssignedAt,
            items, history, assignments);
    }

    // ── Delivery person's own assignments ─────────────────────────────────────

    public async Task<IReadOnlyList<DeliveryAssignmentDto>> GetMyAssignmentsAsync(
        int personnelId, string? statusFilter, CancellationToken ct = default)
    {
        var where = new StringBuilder("WHERE da.DeliveryPersonnelId = @PersonnelId AND da.IsActive = 1");
        if (!string.IsNullOrWhiteSpace(statusFilter))
            where.Append(" AND o.Status = @Status");

        var sql = $"""
            SELECT
                o.Id            AS OrderId,
                o.OrderNumber,
                o.CreatedAt     AS OrderDate,
                ISNULL(u.DisplayName, u.Email) AS CustomerName,
                ISNULL(u.PhoneNumber, '')       AS CustomerPhone,
                o.ShipToAddressLine1,
                o.ShipToAddressLine2,
                o.ShipToCity,
                o.ShipToPincode,
                (SELECT STRING_AGG(CAST(oi.Quantity AS NVARCHAR(20)) + ' × ' + oi.ProductName, ', ')
                 FROM OrderItems oi WHERE oi.OrderId = o.Id) AS ItemSummary,
                CAST(CASE WHEN o.PaymentMethod = 'COD' THEN 1 ELSE 0 END AS BIT) AS IsCod,
                o.TotalAmount,
                o.PaymentStatus,
                o.Status        AS OrderStatus,
                o.Notes         AS DeliveryNotes,
                da.AssignedAt
            FROM DeliveryAssignments da
            JOIN Orders o        ON o.Id = da.OrderId
            JOIN AspNetUsers u   ON u.Id = o.UserId
            {where}
            ORDER BY da.AssignedAt DESC;
            """;

        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync<AssignmentRow>(
            new CommandDefinition(sql,
                new { PersonnelId = personnelId, Status = statusFilter },
                commandTimeout: Timeout, cancellationToken: ct));

        // Fetch status histories for each order in one query
        var rowList = rows.ToList();
        if (!rowList.Any()) return [];

        var orderIds = rowList.Select(r => r.OrderId).Distinct().ToList();
        var historySql = """
            SELECT OrderId, Status, Notes, ChangedAt
            FROM OrderStatusHistory
            WHERE OrderId IN @OrderIds
            ORDER BY OrderId, ChangedAt;
            """;
        var histories = (await conn.QueryAsync<OrderHistoryRow>(
            new CommandDefinition(historySql, new { OrderIds = orderIds },
                commandTimeout: Timeout, cancellationToken: ct))).ToList();

        return rowList.Select(r => new DeliveryAssignmentDto(
            r.OrderId, r.OrderNumber, r.OrderDate,
            r.CustomerName, r.CustomerPhone,
            r.ShipToAddressLine1, r.ShipToAddressLine2,
            r.ShipToCity, r.ShipToPincode,
            r.ItemSummary ?? string.Empty,
            r.IsCod, r.TotalAmount, r.PaymentStatus, r.OrderStatus,
            r.DeliveryNotes, r.AssignedAt,
            histories.Where(h => h.OrderId == r.OrderId)
                     .Select(h => new OrderStatusHistoryDto(h.Status, h.Notes, h.ChangedAt))
                     .ToList()
        )).ToList();
    }

    public async Task<DeliveryAssignmentDto?> GetMyAssignmentDetailAsync(
        int orderId, int personnelId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                o.Id            AS OrderId,
                o.OrderNumber,
                o.CreatedAt     AS OrderDate,
                ISNULL(u.DisplayName, u.Email) AS CustomerName,
                ISNULL(u.PhoneNumber, '')       AS CustomerPhone,
                o.ShipToAddressLine1,
                o.ShipToAddressLine2,
                o.ShipToCity,
                o.ShipToPincode,
                (SELECT STRING_AGG(CAST(oi.Quantity AS NVARCHAR(20)) + ' × ' + oi.ProductName, ', ')
                 FROM OrderItems oi WHERE oi.OrderId = o.Id) AS ItemSummary,
                CAST(CASE WHEN o.PaymentMethod = 'COD' THEN 1 ELSE 0 END AS BIT) AS IsCod,
                o.TotalAmount,
                o.PaymentStatus,
                o.Status        AS OrderStatus,
                o.Notes         AS DeliveryNotes,
                da.AssignedAt
            FROM DeliveryAssignments da
            JOIN Orders o      ON o.Id = da.OrderId
            JOIN AspNetUsers u ON u.Id = o.UserId
            WHERE da.OrderId = @OrderId
              AND da.DeliveryPersonnelId = @PersonnelId
              AND da.IsActive = 1;
            """;
        const string historySql = """
            SELECT Status, Notes, ChangedAt
            FROM OrderStatusHistory
            WHERE OrderId = @OrderId ORDER BY ChangedAt;
            """;

        using var conn = _db.CreateConnection();
        var row = await conn.QuerySingleOrDefaultAsync<AssignmentRow>(
            new CommandDefinition(sql, new { OrderId = orderId, PersonnelId = personnelId },
                commandTimeout: Timeout, cancellationToken: ct));
        if (row is null) return null;

        var history = (await conn.QueryAsync<OrderStatusHistoryDto>(
            new CommandDefinition(historySql, new { OrderId = orderId },
                commandTimeout: Timeout, cancellationToken: ct))).ToList();

        return new DeliveryAssignmentDto(
            row.OrderId, row.OrderNumber, row.OrderDate,
            row.CustomerName, row.CustomerPhone,
            row.ShipToAddressLine1, row.ShipToAddressLine2,
            row.ShipToCity, row.ShipToPincode,
            row.ItemSummary ?? string.Empty,
            row.IsCod, row.TotalAmount, row.PaymentStatus, row.OrderStatus,
            row.DeliveryNotes, row.AssignedAt, history);
    }

    public async Task<(bool Success, string? Error)> UpdateDeliveryStatusAsync(
        int orderId, int personnelId, string userId,
        string newStatus, string? notes,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();

        // Verify this person is the active assignee
        const string checkSql = """
            SELECT o.Status FROM Orders o
            JOIN DeliveryAssignments da ON da.OrderId = o.Id AND da.IsActive = 1
            WHERE o.Id = @OrderId AND da.DeliveryPersonnelId = @PersonnelId;
            """;
        var currentStatus = await conn.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(checkSql, new { OrderId = orderId, PersonnelId = personnelId },
                commandTimeout: Timeout, cancellationToken: ct));

        if (currentStatus is null)
            return (false, "Order not found or not assigned to you.");

        if (!DeliveryTransitions.TryGetValue(currentStatus, out var allowed)
            || !allowed.Any(s => string.Equals(s, newStatus, StringComparison.OrdinalIgnoreCase)))
        {
            return (false, $"Transition from '{currentStatus}' to '{newStatus}' is not permitted.");
        }

        // Prevent duplicate Delivered / DeliveryFailed
        if (string.Equals(newStatus, "Delivered", StringComparison.OrdinalIgnoreCase)
            || string.Equals(newStatus, "DeliveryFailed", StringComparison.OrdinalIgnoreCase))
        {
            const string dupCheck = """
                SELECT COUNT(*) FROM OrderStatusHistory
                WHERE OrderId = @OrderId AND Status = @NewStatus;
                """;
            var dupCount = await conn.QuerySingleAsync<int>(
                new CommandDefinition(dupCheck, new { OrderId = orderId, NewStatus = newStatus },
                    commandTimeout: Timeout, cancellationToken: ct));
            if (dupCount > 0)
                return (false, $"Order already has status '{newStatus}'.");
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
                    new { OrderId = orderId, Status = newStatus, Notes = notes, ChangedBy = userId },
                    transaction: tran, commandTimeout: Timeout, cancellationToken: ct));

            // If delivered or failed, close the active assignment
            if (string.Equals(newStatus, "Delivered", StringComparison.OrdinalIgnoreCase)
                || string.Equals(newStatus, "DeliveryFailed", StringComparison.OrdinalIgnoreCase))
            {
                const string closeSql = """
                    UPDATE DeliveryAssignments
                    SET IsActive        = 0,
                        UnassignedAt    = SYSUTCDATETIME(),
                        UnassignedBy    = @UserId,
                        UnassignedReason = @NewStatus
                    WHERE OrderId = @OrderId AND IsActive = 1;
                    """;
                await conn.ExecuteAsync(
                    new CommandDefinition(closeSql,
                        new { OrderId = orderId, UserId = userId, NewStatus = newStatus },
                        transaction: tran, commandTimeout: Timeout, cancellationToken: ct));
            }

            tran.Commit();
            return (true, null);
        }
        catch (Exception ex)
        {
            tran.Rollback();
            _logger.LogError(ex, "UpdateDeliveryStatus failed orderId={OrderId}", orderId);
            return (false, "Status update failed due to an internal error.");
        }
    }

    // ── Customer tracking ─────────────────────────────────────────────────────

    public async Task<DeliveryTrackingDto?> GetTrackingForOrderAsync(
        int orderId, string userId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT o.Id AS OrderId, o.OrderNumber, o.Status,
                   dp.FullName AS AssignedPersonnelName
            FROM Orders o
            LEFT JOIN DeliveryAssignments da ON da.OrderId = o.Id AND da.IsActive = 1
            LEFT JOIN DeliveryPersonnel dp  ON dp.Id = da.DeliveryPersonnelId
            WHERE o.Id = @OrderId AND o.UserId = @UserId;

            SELECT Status, Notes, ChangedAt FROM OrderStatusHistory
            WHERE OrderId = @OrderId ORDER BY ChangedAt;
            """;

        using var conn  = _db.CreateConnection();
        using var multi = await conn.QueryMultipleAsync(
            new CommandDefinition(sql, new { OrderId = orderId, UserId = userId },
                commandTimeout: Timeout, cancellationToken: ct));

        var header = await multi.ReadSingleOrDefaultAsync<TrackingHeaderRow>();
        if (header is null) return null;

        var timeline = (await multi.ReadAsync<OrderStatusHistoryDto>()).ToList();

        // Only show delivery person name when order is in delivery-active states
        var showPersonnel = header.Status is "PickedUp" or "OutForDelivery";

        return new DeliveryTrackingDto(
            header.OrderId, header.OrderNumber, header.Status,
            showPersonnel ? header.AssignedPersonnelName : null,
            timeline);
    }

    // ── Notifications ─────────────────────────────────────────────────────────

    public async Task<long?> TryQueueNotificationAsync(
        int orderId, string eventType, string recipientUserId,
        string? recipientEmail, string? recipientPhone,
        string channel, CancellationToken ct = default)
    {
        // Idempotency key = orderId:eventType:channel
        var idempotencyKey = $"{orderId}:{eventType}:{channel}";

        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM NotificationEvents WHERE IdempotencyKey = @IdempotencyKey)
            BEGIN
                INSERT INTO NotificationEvents
                    (OrderId, EventType, IdempotencyKey, RecipientUserId, RecipientEmail, RecipientPhone,
                     Channel, Status, QueuedAt)
                OUTPUT INSERTED.Id
                VALUES
                    (@OrderId, @EventType, @IdempotencyKey, @RecipientUserId, @RecipientEmail, @RecipientPhone,
                     @Channel, 'Queued', SYSUTCDATETIME());
            END
            ELSE
                SELECT NULL;
            """;
        using var conn = _db.CreateConnection();
        var id = await conn.QuerySingleOrDefaultAsync<long?>(
            new CommandDefinition(sql,
                new { OrderId = orderId, EventType = eventType, IdempotencyKey = idempotencyKey,
                      RecipientUserId = recipientUserId, RecipientEmail = recipientEmail,
                      RecipientPhone = recipientPhone, Channel = channel },
                commandTimeout: Timeout, cancellationToken: ct));
        return id;
    }

    public async Task MarkNotificationSentAsync(long notificationId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE NotificationEvents
            SET Status = 'Sent', SentAt = SYSUTCDATETIME()
            WHERE Id = @Id;
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = notificationId },
            commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task MarkNotificationFailedAsync(long notificationId, string reason, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE NotificationEvents
            SET Status = 'Failed', FailureReason = @Reason
            WHERE Id = @Id;
            """;
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Id = notificationId, Reason = reason },
            commandTimeout: Timeout, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<InAppNotificationDto>> GetInAppNotificationsAsync(
        string userId, int limit, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT TOP {Math.Min(limit, 50)}
                ne.Id,
                ne.OrderId,
                o.OrderNumber,
                ne.EventType,
                CASE ne.EventType
                    WHEN 'OrderConfirmed'      THEN 'Your order has been confirmed.'
                    WHEN 'PreparationStarted'  THEN 'Your order is being prepared.'
                    WHEN 'PickedUp'            THEN 'Your order has been picked up by the delivery person.'
                    WHEN 'OutForDelivery'      THEN 'Your order is out for delivery!'
                    WHEN 'Delivered'           THEN 'Your order has been delivered. Enjoy!'
                    WHEN 'DeliveryFailed'      THEN 'Delivery attempt failed. We will reschedule shortly.'
                    WHEN 'Rescheduled'         THEN 'Your delivery has been rescheduled.'
                    ELSE ne.EventType
                END AS Message,
                CAST(CASE WHEN ne.Status = 'Sent' THEN 1 ELSE 0 END AS BIT) AS IsRead,
                ne.QueuedAt
            FROM NotificationEvents ne
            JOIN Orders o ON o.Id = ne.OrderId
            WHERE ne.RecipientUserId = @UserId
              AND ne.Channel = 'InApp'
            ORDER BY ne.QueuedAt DESC;
            """;
        using var conn = _db.CreateConnection();
        var rows = await conn.QueryAsync<InAppNotificationDto>(
            new CommandDefinition(sql, new { UserId = userId },
                commandTimeout: Timeout, cancellationToken: ct));
        return rows.ToList().AsReadOnly();
    }

    // ── Private row types ─────────────────────────────────────────────────────

    private sealed record OrderDetailHeaderRow(
        int OrderId, string OrderNumber, DateTime OrderDate,
        string CustomerName, string CustomerEmail, string CustomerPhone,
        string ShipToAddressLine1, string? ShipToAddressLine2,
        string ShipToCity, string ShipToState, string ShipToPincode,
        decimal SubTotal, decimal DeliveryFee, decimal DiscountAmount, decimal TotalAmount,
        string PaymentMethod, string PaymentStatus, string Status, string? Notes,
        int? AssignedPersonnelId, string? AssignedPersonnelName, string? AssignedPersonnelPhone,
        DateTime? AssignedAt);

    private sealed record AssignmentRow(
        int OrderId, string OrderNumber, DateTime OrderDate,
        string CustomerName, string CustomerPhone,
        string ShipToAddressLine1, string? ShipToAddressLine2,
        string ShipToCity, string ShipToPincode,
        string? ItemSummary, bool IsCod, decimal TotalAmount,
        string PaymentStatus, string OrderStatus, string? DeliveryNotes,
        DateTime AssignedAt);

    private sealed record OrderHistoryRow(
        int OrderId, string Status, string? Notes, DateTime ChangedAt);

    private sealed record TrackingHeaderRow(
        int OrderId, string OrderNumber, string Status,
        string? AssignedPersonnelName);
}
