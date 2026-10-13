namespace GOLISMart.API.Models;

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    // Delivery address snapshot
    public string ShipToAddressLine1 { get; set; } = string.Empty;
    public string? ShipToAddressLine2 { get; set; }
    public string ShipToCity { get; set; } = string.Empty;
    public string ShipToState { get; set; } = string.Empty;
    public string ShipToPincode { get; set; } = string.Empty;

    // Financials
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal TotalAmount { get; set; }

    // Coupon
    public string? CouponCode { get; set; }
    public decimal CouponDiscount { get; set; }

    // Payment
    public string PaymentMethod { get; set; } = "COD";
    public string PaymentStatus { get; set; } = "Pending";

    // Status
    public string Status { get; set; } = "Pending";
    public int? DeliverySlotId { get; set; }
    public DateTime? ScheduledDelivery { get; set; }
    public string? Notes { get; set; }
    public string? IdempotencyKey { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Set by list queries only — not stored in Orders table.</summary>
    public int ItemCount { get; set; }
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }

    // Purchase-time snapshots
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public string UnitLabel { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? OfferPrice { get; set; }
    public decimal EffectivePrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class OrderStatusHistory
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
}
