namespace GOLISMart.API.DTOs;

// ── Razorpay order creation ──────────────────────────────────────────────────

/// <summary>Request to create a Razorpay order (from the checkout flow).</summary>
public record CreateRazorpayOrderRequest(
    string SessionKey,
    int AddressId,
    string? Notes,
    string? IdempotencyKey
);

/// <summary>Response returned to the browser — never includes KeySecret.</summary>
public record CreateRazorpayOrderResponse(
    int InternalOrderId,
    string InternalOrderNumber,
    string RazorpayOrderId,   // order_XXXXXXXX
    long AmountPaise,         // amount in paise
    string Currency,          // INR
    string KeyId,             // public key only
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone
);

// ── Payment verification ─────────────────────────────────────────────────────

/// <summary>Browser sends this after the Razorpay SDK fires the payment.success callback.</summary>
public record VerifyRazorpayPaymentRequest(
    string RazorpayOrderId,
    string RazorpayPaymentId,
    string RazorpaySignature
);

public record VerifyRazorpayPaymentResponse(
    bool Success,
    string Message,
    int? InternalOrderId,
    string? OrderNumber,
    string? OrderStatus,
    string? PaymentStatus
);

// ── Razorpay REST API internal response models ───────────────────────────────

/// <summary>Razorpay Orders API response (only fields we use).</summary>
public class RazorpayOrderApiResponse
{
    public string? Id { get; set; }
    public string? Entity { get; set; }
    public long Amount { get; set; }
    public long AmountPaid { get; set; }
    public long AmountDue { get; set; }
    public string? Currency { get; set; }
    public string? Receipt { get; set; }
    public string? Status { get; set; }
}

/// <summary>Razorpay Payments API response (only fields we use for capture verification).</summary>
public class RazorpayPaymentApiResponse
{
    public string? Id { get; set; }
    public string? Entity { get; set; }
    public string? OrderId { get; set; }
    public long Amount { get; set; }
    public string? Currency { get; set; }
    public string? Status { get; set; }
    public string? Method { get; set; }
    public string? Email { get; set; }
    public string? Contact { get; set; }
    public bool Captured { get; set; }
}
