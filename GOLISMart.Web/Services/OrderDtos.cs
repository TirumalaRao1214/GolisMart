namespace GOLISMart.Web.Services;

// ── Order DTOs (mirrored from GOLISMart.API — no project reference) ───────────

public record PlaceOrderRequest(
    string SessionKey,
    int AddressId,
    string PaymentMethod,
    string? Notes,
    string? IdempotencyKey);

public record PlaceOrderResponse(
    int OrderId,
    string OrderNumber,
    DateTime OrderDate,
    string Status,
    string PaymentMethod,
    string PaymentStatus,
    OrderAddressSnapshot DeliveryAddress,
    List<OrderItemSnapshot> Items,
    decimal SubTotal,
    decimal DeliveryFee,
    decimal DiscountAmount,
    decimal TotalAmount,
    string? EstimatedDeliveryWindow);

public record OrderSummaryDto(
    int OrderId,
    string OrderNumber,
    DateTime OrderDate,
    int ItemCount,
    decimal TotalAmount,
    string Status,
    string PaymentMethod,
    string PaymentStatus);

public record OrderDetailDto(
    int OrderId,
    string OrderNumber,
    DateTime OrderDate,
    string Status,
    string PaymentMethod,
    string PaymentStatus,
    OrderAddressSnapshot DeliveryAddress,
    List<OrderItemSnapshot> Items,
    decimal SubTotal,
    decimal DeliveryFee,
    decimal DiscountAmount,
    decimal TotalAmount,
    string? Notes,
    List<OrderStatusHistoryDto> StatusHistory,
    bool CanCancel);

public record OrderAddressSnapshot(
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PinCode);

public record OrderItemSnapshot(
    int ProductId,
    string ProductName,
    string ProductSKU,
    string UnitLabel,
    decimal Quantity,
    decimal UnitPrice,
    decimal? OfferPrice,
    decimal EffectivePrice,
    decimal LineTotal);

public record OrderStatusHistoryDto(
    string Status,
    string? Notes,
    DateTime ChangedAt);

public record CancelOrderResponse(
    bool Success,
    string Message,
    string? NewStatus);

public record OrdersPagedResponse(
    List<OrderSummaryDto> Orders,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
