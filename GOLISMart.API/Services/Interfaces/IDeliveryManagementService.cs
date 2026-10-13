using GOLISMart.API.DTOs;

namespace GOLISMart.API.Services.Interfaces;

public interface IDeliveryManagementService
{
    // ── Delivery Personnel ────────────────────────────────────────────────────
    Task<(IReadOnlyList<DeliveryPersonnelDto> Items, int TotalCount)> GetPersonnelAsync(
        string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default);

    Task<DeliveryPersonnelDto?> GetPersonnelByIdAsync(int id, CancellationToken ct = default);

    Task<(bool Success, string? Error, int Id)> CreatePersonnelAsync(
        CreateDeliveryPersonnelRequest request, string adminId, CancellationToken ct = default);

    Task<(bool Success, string? Error)> UpdatePersonnelAsync(
        int id, UpdateDeliveryPersonnelRequest request, string adminId, CancellationToken ct = default);

    // ── Delivery Assignment (admin) ───────────────────────────────────────────
    Task<(bool Success, string? Error)> AssignOrderAsync(
        int orderId, int personnelId, string adminId, string? notes, CancellationToken ct = default);

    Task<(bool Success, string? Error)> UnassignOrderAsync(
        int orderId, string adminId, string reason, CancellationToken ct = default);

    // ── Admin delivery list ───────────────────────────────────────────────────
    Task<(IReadOnlyList<AdminDeliveryOrderListDto> Orders, int TotalCount)> GetDeliveryOrdersAsync(
        string? search, string? status, DateTime? from, DateTime? to,
        string? pincode, int? personnelId,
        int page, int pageSize, CancellationToken ct = default);

    Task<AdminDeliveryOrderDetailDto?> GetDeliveryOrderDetailAsync(int orderId, CancellationToken ct = default);

    // ── Delivery person's dashboard ───────────────────────────────────────────
    Task<IReadOnlyList<DeliveryAssignmentDto>> GetMyAssignmentsAsync(
        string userId, string? statusFilter, CancellationToken ct = default);

    Task<DeliveryAssignmentDto?> GetMyAssignmentDetailAsync(
        int orderId, string userId, CancellationToken ct = default);

    Task<(bool Success, string? Error)> UpdateDeliveryStatusAsync(
        int orderId, string userId, string newStatus, string? notes, CancellationToken ct = default);

    // ── Customer tracking ─────────────────────────────────────────────────────
    Task<DeliveryTrackingDto?> GetTrackingForOrderAsync(
        int orderId, string userId, CancellationToken ct = default);

    // ── In-app notifications ──────────────────────────────────────────────────
    Task<IReadOnlyList<InAppNotificationDto>> GetInAppNotificationsAsync(
        string userId, int limit = 20, CancellationToken ct = default);

    /// <summary>Fires all configured notification channels for an order event. Never throws.</summary>
    Task FireNotificationsAsync(
        int orderId, string orderNumber, string eventType,
        string recipientUserId, string? recipientEmail, string? recipientPhone,
        CancellationToken ct = default);
}
