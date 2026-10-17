namespace GOLISMart.Web.Services.Interfaces;

// ── Razorpay payment DTOs (mirrored from GOLISMart.API — no project reference) ──

public record CreateRazorpayOrderRequest(
    string SessionKey,
    int AddressId,
    string? Notes,
    string? IdempotencyKey);

public record CreateRazorpayOrderResponse(
    int InternalOrderId,
    string InternalOrderNumber,
    string RazorpayOrderId,
    long AmountPaise,
    string Currency,
    string KeyId,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone);

public record VerifyRazorpayPaymentRequest(
    string RazorpayOrderId,
    string RazorpayPaymentId,
    string RazorpaySignature);

public record VerifyRazorpayPaymentResponse(
    bool Success,
    string Message,
    int? InternalOrderId,
    string? OrderNumber,
    string? OrderStatus,
    string? PaymentStatus);

// ── Interface ─────────────────────────────────────────────────────────────────

public interface IPaymentsApiClient
{
    /// <summary>
    /// Calls POST /api/v1/payments/razorpay/create-order on the Web API.
    /// The token and sessionKey are supplied by the MVC layer — never from the browser.
    /// </summary>
    Task<CreateRazorpayOrderResponse?> CreateRazorpayOrderAsync(
        string token,
        CreateRazorpayOrderRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Calls POST /api/v1/payments/razorpay/verify on the Web API.
    /// </summary>
    Task<VerifyRazorpayPaymentResponse?> VerifyRazorpayPaymentAsync(
        string token,
        VerifyRazorpayPaymentRequest request,
        CancellationToken ct = default);
}
