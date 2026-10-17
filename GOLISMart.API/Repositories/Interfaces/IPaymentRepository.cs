using GOLISMart.API.Models;

namespace GOLISMart.API.Repositories.Interfaces;

public interface IPaymentRepository
{
    /// <summary>Creates a new payment attempt record.</summary>
    Task<Payment> CreateAsync(Payment payment, CancellationToken ct = default);

    /// <summary>Returns the payment for the given internal order (most recent first).</summary>
    Task<Payment?> GetByOrderIdAsync(int orderId, CancellationToken ct = default);

    /// <summary>Returns the payment by its Razorpay gateway order ID.</summary>
    Task<Payment?> GetByGatewayOrderIdAsync(string gatewayOrderId, CancellationToken ct = default);

    /// <summary>Returns the payment by its Razorpay gateway payment ID.</summary>
    Task<Payment?> GetByGatewayPaymentIdAsync(string gatewayPaymentId, CancellationToken ct = default);

    /// <summary>
    /// Atomically marks a payment as Captured and updates the parent order's
    /// PaymentStatus to Paid.  Returns false if the payment is already Captured.
    /// </summary>
    Task<bool> CaptureAsync(
        int paymentId,
        string gatewayPaymentId,
        string? method,
        CancellationToken ct = default);

    /// <summary>Marks a payment as Failed.</summary>
    Task MarkFailedAsync(int paymentId, string? reason, CancellationToken ct = default);

    // ── Webhook events ───────────────────────────────────────────────────────

    /// <summary>
    /// Records a webhook event.  Returns null if a duplicate (same GatewayEventId)
    /// already exists (idempotency guard).
    /// </summary>
    Task<PaymentWebhookEvent?> RecordWebhookEventAsync(
        PaymentWebhookEvent evt,
        CancellationToken ct = default);

    /// <summary>Marks a previously-recorded webhook event as successfully processed.</summary>
    Task MarkWebhookEventProcessedAsync(int eventId, CancellationToken ct = default);

    /// <summary>Marks a webhook event as having a processing error.</summary>
    Task MarkWebhookEventErrorAsync(int eventId, string error, CancellationToken ct = default);
}
