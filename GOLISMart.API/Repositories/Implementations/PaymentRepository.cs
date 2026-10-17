using System.Data;
using Dapper;
using GOLISMart.API.Data;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;

namespace GOLISMart.API.Repositories.Implementations;

public class PaymentRepository : IPaymentRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<PaymentRepository> _logger;
    private const int Timeout = 30;

    public PaymentRepository(IDbConnectionFactory db, ILogger<PaymentRepository> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── CreateAsync ────────────────────────────────────────────────────────────

    public async Task<Payment> CreateAsync(Payment payment, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO Payments
                (OrderId, GatewayName, GatewayPaymentId, GatewayOrderId,
                 Amount, Currency, Status, Method, FailureReason,
                 CreatedAt, UpdatedAt)
            OUTPUT INSERTED.Id, INSERTED.CreatedAt, INSERTED.UpdatedAt
            VALUES
                (@OrderId, @GatewayName, @GatewayPaymentId, @GatewayOrderId,
                 @Amount, @Currency, @Status, @Method, @FailureReason,
                 SYSUTCDATETIME(), SYSUTCDATETIME());
            """;

        using var conn = _db.CreateConnection();
        var result = await conn.QuerySingleAsync<(int Id, DateTime CreatedAt, DateTime UpdatedAt)>(
            new CommandDefinition(sql, payment, commandTimeout: Timeout, cancellationToken: ct));

        payment.Id        = result.Id;
        payment.CreatedAt = result.CreatedAt;
        payment.UpdatedAt = result.UpdatedAt;
        return payment;
    }

    // ── GetByOrderIdAsync ──────────────────────────────────────────────────────

    public async Task<Payment?> GetByOrderIdAsync(int orderId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1 Id, OrderId, GatewayName, GatewayPaymentId, GatewayOrderId,
                   Amount, Currency, Status, Method, FailureReason, CreatedAt, UpdatedAt
            FROM   Payments
            WHERE  OrderId = @OrderId
            ORDER  BY CreatedAt DESC;
            """;

        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Payment>(
            new CommandDefinition(sql, new { OrderId = orderId },
                commandTimeout: Timeout, cancellationToken: ct));
    }

    // ── GetByGatewayOrderIdAsync ───────────────────────────────────────────────

    public async Task<Payment?> GetByGatewayOrderIdAsync(string gatewayOrderId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1 Id, OrderId, GatewayName, GatewayPaymentId, GatewayOrderId,
                   Amount, Currency, Status, Method, FailureReason, CreatedAt, UpdatedAt
            FROM   Payments
            WHERE  GatewayOrderId = @GatewayOrderId
            ORDER  BY CreatedAt DESC;
            """;

        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Payment>(
            new CommandDefinition(sql, new { GatewayOrderId = gatewayOrderId },
                commandTimeout: Timeout, cancellationToken: ct));
    }

    // ── GetByGatewayPaymentIdAsync ─────────────────────────────────────────────

    public async Task<Payment?> GetByGatewayPaymentIdAsync(string gatewayPaymentId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1 Id, OrderId, GatewayName, GatewayPaymentId, GatewayOrderId,
                   Amount, Currency, Status, Method, FailureReason, CreatedAt, UpdatedAt
            FROM   Payments
            WHERE  GatewayPaymentId = @GatewayPaymentId;
            """;

        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Payment>(
            new CommandDefinition(sql, new { GatewayPaymentId = gatewayPaymentId },
                commandTimeout: Timeout, cancellationToken: ct));
    }

    // ── CaptureAsync ───────────────────────────────────────────────────────────
    // Atomic: marks payment Captured + marks order PaymentStatus Paid.
    // Returns false if already Captured (idempotent).

    public async Task<bool> CaptureAsync(
        int paymentId,
        string gatewayPaymentId,
        string? method,
        CancellationToken ct = default)
    {
        using var conn = _db.CreateConnection();
        conn.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            // Check current status with lock
            const string checkSql = """
                SELECT Id, Status, OrderId
                FROM   Payments WITH (UPDLOCK, ROWLOCK)
                WHERE  Id = @PaymentId;
                """;

            var row = await conn.QuerySingleOrDefaultAsync<(int Id, string Status, int OrderId)>(
                new CommandDefinition(checkSql, new { PaymentId = paymentId },
                    transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            if (row.Id == 0)
            {
                tx.Rollback();
                return false;
            }

            // Already captured — idempotent, treat as success
            if (string.Equals(row.Status, "Captured", StringComparison.OrdinalIgnoreCase))
            {
                tx.Rollback();
                return false; // caller detects duplicate
            }

            const string updatePayment = """
                UPDATE Payments
                SET    Status           = 'Captured',
                       GatewayPaymentId = @GatewayPaymentId,
                       Method           = @Method,
                       UpdatedAt        = SYSUTCDATETIME()
                WHERE  Id = @PaymentId;
                """;

            await conn.ExecuteAsync(new CommandDefinition(updatePayment,
                new { PaymentId = paymentId, GatewayPaymentId = gatewayPaymentId, Method = method },
                transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            const string updateOrder = """
                UPDATE Orders
                SET    PaymentStatus = 'Paid',
                       UpdatedAt     = SYSUTCDATETIME()
                WHERE  Id = @OrderId
                  AND  PaymentStatus != 'Paid';
                """;

            await conn.ExecuteAsync(new CommandDefinition(updateOrder,
                new { row.OrderId },
                transaction: tx, commandTimeout: Timeout, cancellationToken: ct));

            tx.Commit();
            _logger.LogInformation("Payment {PaymentId} captured for order {OrderId}", paymentId, row.OrderId);
            return true;
        }
        catch
        {
            try { tx.Rollback(); } catch { /* swallow */ }
            throw;
        }
    }

    // ── MarkFailedAsync ────────────────────────────────────────────────────────

    public async Task MarkFailedAsync(int paymentId, string? reason, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE Payments
            SET    Status        = 'Failed',
                   FailureReason = @Reason,
                   UpdatedAt     = SYSUTCDATETIME()
            WHERE  Id = @PaymentId AND Status NOT IN ('Captured', 'Refunded');
            """;

        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql,
            new { PaymentId = paymentId, Reason = reason },
            commandTimeout: Timeout, cancellationToken: ct));
    }

    // ── RecordWebhookEventAsync ────────────────────────────────────────────────

    public async Task<PaymentWebhookEvent?> RecordWebhookEventAsync(
        PaymentWebhookEvent evt,
        CancellationToken ct = default)
    {
        // Idempotency: if GatewayEventId already exists, return null
        const string checkSql = """
            SELECT Id FROM PaymentWebhookEvents
            WHERE GatewayEventId = @GatewayEventId;
            """;

        const string insertSql = """
            INSERT INTO PaymentWebhookEvents
                (PaymentId, GatewayName, EventType, GatewayEventId,
                 RawPayload, ProcessedAt, ProcessingError, ReceivedAt)
            OUTPUT INSERTED.Id, INSERTED.ReceivedAt
            VALUES
                (@PaymentId, @GatewayName, @EventType, @GatewayEventId,
                 @RawPayload, NULL, NULL, SYSUTCDATETIME());
            """;

        using var conn = _db.CreateConnection();

        if (!string.IsNullOrEmpty(evt.GatewayEventId))
        {
            var existing = await conn.QuerySingleOrDefaultAsync<int?>(
                new CommandDefinition(checkSql, new { evt.GatewayEventId },
                    commandTimeout: Timeout, cancellationToken: ct));

            if (existing.HasValue)
            {
                _logger.LogInformation(
                    "Duplicate webhook event {EventId} — skipping.", evt.GatewayEventId);
                return null;
            }
        }

        var result = await conn.QuerySingleAsync<(int Id, DateTime ReceivedAt)>(
            new CommandDefinition(insertSql, evt, commandTimeout: Timeout, cancellationToken: ct));

        evt.Id         = result.Id;
        evt.ReceivedAt = result.ReceivedAt;
        return evt;
    }

    // ── MarkWebhookEventProcessedAsync ────────────────────────────────────────

    public async Task MarkWebhookEventProcessedAsync(int eventId, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE PaymentWebhookEvents
            SET    ProcessedAt     = SYSUTCDATETIME(),
                   ProcessingError = NULL
            WHERE  Id = @EventId;
            """;

        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql, new { EventId = eventId },
            commandTimeout: Timeout, cancellationToken: ct));
    }

    // ── MarkWebhookEventErrorAsync ─────────────────────────────────────────────

    public async Task MarkWebhookEventErrorAsync(int eventId, string error, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE PaymentWebhookEvents
            SET    ProcessingError = @Error,
                   ProcessedAt     = SYSUTCDATETIME()
            WHERE  Id = @EventId;
            """;

        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(new CommandDefinition(sql,
            new { EventId = eventId, Error = error },
            commandTimeout: Timeout, cancellationToken: ct));
    }
}
