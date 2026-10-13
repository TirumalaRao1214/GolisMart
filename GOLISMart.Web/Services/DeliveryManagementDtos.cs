namespace GOLISMart.Web.Services;

// ---------------------------------------------------------------------------
// Delivery Management DTOs — mirror of GOLISMart.API.DTOs.DeliveryManagementDto
// ---------------------------------------------------------------------------

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

public record AdminCreatePersonnelRequest(
    string  FullName,
    string  ContactNumber,
    string? UserId,
    string? Notes);

public record AdminUpdatePersonnelRequest(
    string  FullName,
    string  ContactNumber,
    string? UserId,
    string? Notes,
    bool    IsActive);

// ── Delivery Assignment ───────────────────────────────────────────────────────

public record AdminAssignDeliveryRequest(int DeliveryPersonnelId, string? Notes);
public record AdminUnassignDeliveryRequest(string Reason);

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

// ── Delivery Person Dashboard ─────────────────────────────────────────────────

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
    List<OrderStatusHistoryDto> StatusHistory);

public record UpdateDeliveryStatusRequest(string NewStatus, string? Notes);
public record ConfirmDeliveredRequest(bool Confirmed, string? Notes);

// ── Customer Tracking ─────────────────────────────────────────────────────────

public record DeliveryTrackingDto(
    int     OrderId,
    string  OrderNumber,
    string  Status,
    string? AssignedPersonnelName,
    List<OrderStatusHistoryDto> Timeline);

// ── In-app Notifications ──────────────────────────────────────────────────────

public record InAppNotificationDto(
    long    Id,
    int     OrderId,
    string  OrderNumber,
    string  EventType,
    string  Message,
    bool    IsRead,
    DateTime QueuedAt);
