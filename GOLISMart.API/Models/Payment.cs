namespace GOLISMart.API.Models;

/// <summary>Maps to the Payments table.</summary>
public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string GatewayName { get; set; } = "Razorpay";
    public string? GatewayPaymentId { get; set; }
    public string? GatewayOrderId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Status { get; set; } = "Initiated";  // Initiated, Pending, Captured, Failed, Refunded
    public string? Method { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Maps to the PaymentWebhookEvents table.</summary>
public class PaymentWebhookEvent
{
    public int Id { get; set; }
    public int? PaymentId { get; set; }
    public string GatewayName { get; set; } = "Razorpay";
    public string EventType { get; set; } = string.Empty;
    public string GatewayEventId { get; set; } = string.Empty;
    public string RawPayload { get; set; } = string.Empty;
    public DateTime? ProcessedAt { get; set; }
    public string? ProcessingError { get; set; }
    public DateTime ReceivedAt { get; set; }
}
