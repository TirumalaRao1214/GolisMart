namespace GOLISMart.API.DTOs;

// ── Delivery Personnel ────────────────────────────────────────────────────────

public record DeliveryPersonnelDto(
    int     Id,
    string? UserId,
    string  FullName,
    string  ContactNumber,
    bool    IsActive,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int     ActiveAssignmentCount);

public record CreateDeliveryPersonnelRequest(
    string  FullName,
    string  ContactNumber,
    string? UserId,
    string? Notes);

public record UpdateDeliveryPersonnelRequest(
    string  FullName,
    string  ContactNumber,
    string? UserId,
    string? Notes,
    bool    IsActive);

// ── Delivery Assignment ───────────────────────────────────────────────────────

public record AssignDeliveryRequest(
    int     DeliveryPersonnelId,
    string? Notes);

public record UnassignDeliveryRequest(
    string  Reason);

/// <summary>Admin view of a delivery order — extends order list with delivery info.</summary>
public record AdminDeliveryOrderListDto(
    int     OrderId,
    string  OrderNumber,
    DateTime OrderDate,
    string  CustomerName,
    string  CustomerEmail,
    string  CustomerPhone,
    string  DeliveryAddress,
    string  ShipToPincode,
    decimal TotalAmount,
    string  PaymentMethod,
    string  PaymentStatus,
    string  Status,
    int?    AssignedPersonnelId,
    string? AssignedPersonnelName,
    DateTime? AssignedAt,
    int     ItemCount);

public record AdminDeliveryOrderDetailDto(
    int     OrderId,
    string  OrderNumber,
    DateTime OrderDate,
    string  CustomerName,
    string  CustomerEmail,
    string  CustomerPhone,
    string  ShipToAddressLine1,
    string? ShipToAddressLine2,
    string  ShipToCity,
    string  ShipToState,
    string  ShipToPincode,
    decimal SubTotal,
    decimal DeliveryFee,
    decimal DiscountAmount,
    decimal TotalAmount,
    string  PaymentMethod,
    string  PaymentStatus,
    string  Status,
    string? Notes,
    int?    AssignedPersonnelId,
    string? AssignedPersonnelName,
    string? AssignedPersonnelPhone,
    DateTime? AssignedAt,
    List<OrderItemSnapshot> Items,
    List<OrderStatusHistoryDto> StatusHistory,
    List<DeliveryAssignmentHistoryDto> AssignmentHistory);

public record DeliveryAssignmentHistoryDto(
    int     DeliveryPersonnelId,
    string  PersonnelName,
    DateTime AssignedAt,
    DateTime? UnassignedAt,
    string? UnassignedReason,
    bool    IsActive);

// ── Delivery Personnel Dashboard ──────────────────────────────────────────────

/// <summary>Delivery person's view of an assigned order.</summary>
public record DeliveryAssignmentDto(
    int     OrderId,
    string  OrderNumber,
    DateTime OrderDate,
    string  CustomerName,
    string  CustomerPhone,
    string  ShipToAddressLine1,
    string? ShipToAddressLine2,
    string  ShipToCity,
    string  ShipToPincode,
    string  ItemSummary,
    bool    IsCod,
    decimal TotalAmount,
    string  PaymentStatus,
    string  OrderStatus,
    string? DeliveryNotes,
    DateTime AssignedAt,
    IReadOnlyList<OrderStatusHistoryDto> StatusHistory);

/// <summary>Status transition request from a delivery person.</summary>
public record UpdateDeliveryStatusRequest(
    string  NewStatus,
    string? Notes);

/// <summary>Confirmation request for Delivered status (requires explicit confirmation).</summary>
public record ConfirmDeliveredRequest(
    bool    Confirmed,
    string? Notes);

// ── Customer Tracking ─────────────────────────────────────────────────────────

/// <summary>Customer-facing delivery tracking enriched with delivery info.</summary>
public record DeliveryTrackingDto(
    int     OrderId,
    string  OrderNumber,
    string  Status,
    string? AssignedPersonnelName,
    IReadOnlyList<OrderStatusHistoryDto> Timeline);

// ── Notifications (in-app inbox) ──────────────────────────────────────────────

public record InAppNotificationDto(
    long    Id,
    int     OrderId,
    string  OrderNumber,
    string  EventType,
    string  Message,
    bool    IsRead,
    DateTime QueuedAt);
