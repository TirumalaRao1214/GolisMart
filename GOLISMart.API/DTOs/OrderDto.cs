namespace GOLISMart.API.DTOs;

// ── Place Order ──────────────────────────────────────────────────────────────

/// <summary>
/// Request body for placing a new order.
/// The customer identity and cart are resolved server-side.
/// </summary>
public record PlaceOrderRequest(
    string SessionKey,
    int AddressId,
    string PaymentMethod,   // COD, UPI, Card, etc.
    string? Notes,
    string? IdempotencyKey  // client-generated UUID to prevent duplicate submissions
);

// ── Order confirmation (returned immediately after placement) ────────────────

public record PlaceOrderResponse(
    int OrderId,
    string OrderNumber,
    DateTime OrderDate,
    string Status,
    string PaymentMethod,
    string PaymentStatus,
    OrderAddressSnapshot DeliveryAddress,
    IReadOnlyList<OrderItemSnapshot> Items,
    decimal SubTotal,
    decimal DeliveryFee,
    decimal DiscountAmount,
    decimal TotalAmount,
    string? EstimatedDeliveryWindow
);

// ── Order summary (list view) ────────────────────────────────────────────────

public record OrderSummaryDto(
    int OrderId,
    string OrderNumber,
    DateTime OrderDate,
    int ItemCount,
    decimal TotalAmount,
    string Status,
    string PaymentMethod,
    string PaymentStatus
);

// ── Order detail ─────────────────────────────────────────────────────────────

public record OrderDetailDto(
    int OrderId,
    string OrderNumber,
    DateTime OrderDate,
    string Status,
    string PaymentMethod,
    string PaymentStatus,
    OrderAddressSnapshot DeliveryAddress,
    IReadOnlyList<OrderItemSnapshot> Items,
    decimal SubTotal,
    decimal DeliveryFee,
    decimal DiscountAmount,
    decimal TotalAmount,
    string? Notes,
    IReadOnlyList<OrderStatusHistoryDto> StatusHistory,
    bool CanCancel
);

// ── Shared sub-types ─────────────────────────────────────────────────────────

public record OrderAddressSnapshot(
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PinCode
);

public record OrderItemSnapshot(
    int ProductId,
    string ProductName,
    string ProductSKU,
    string UnitLabel,
    decimal Quantity,
    decimal UnitPrice,
    decimal? OfferPrice,
    decimal EffectivePrice,
    decimal LineTotal
);

public record OrderStatusHistoryDto(
    string Status,
    string? Notes,
    DateTime ChangedAt
);

// ── Cancel order ─────────────────────────────────────────────────────────────

public record CancelOrderResponse(
    bool Success,
    string Message,
    string? NewStatus
);

// ── Paged orders response ────────────────────────────────────────────────────

public record OrdersPagedResponse(
    IReadOnlyList<OrderSummaryDto> Orders,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);
