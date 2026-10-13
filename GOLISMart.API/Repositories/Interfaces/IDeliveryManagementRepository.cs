using GOLISMart.API.DTOs;
using GOLISMart.API.Models;

namespace GOLISMart.API.Repositories.Interfaces;

public interface IDeliveryManagementRepository
{
    // ── Delivery Personnel CRUD ───────────────────────────────────────────────
    Task<(IReadOnlyList<DeliveryPersonnelDto> Items, int TotalCount)> GetPersonnelAsync(
        string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default);

    Task<DeliveryPersonnelDto?> GetPersonnelByIdAsync(int id, CancellationToken ct = default);

    Task<DeliveryPersonnel?> GetPersonnelByUserIdAsync(string userId, CancellationToken ct = default);

    Task<int> CreatePersonnelAsync(CreateDeliveryPersonnelRequest request, CancellationToken ct = default);

    Task<bool> UpdatePersonnelAsync(int id, UpdateDeliveryPersonnelRequest request, CancellationToken ct = default);

    /// <summary>Returns active assignment count for the personnel record.</summary>
    Task<int> GetActiveAssignmentCountAsync(int personnelId, CancellationToken ct = default);

    // ── Delivery Assignment ───────────────────────────────────────────────────

    /// <summary>Assigns an eligible order to a delivery person. Returns (success, error).</summary>
    Task<(bool Success, string? Error)> AssignOrderAsync(
        int orderId, int personnelId, string adminUserId, string? notes, CancellationToken ct = default);

    /// <summary>Unassigns the current delivery person from an order.</summary>
    Task<(bool Success, string? Error)> UnassignOrderAsync(
        int orderId, string adminUserId, string reason, CancellationToken ct = default);

    // ── Admin delivery list ───────────────────────────────────────────────────

    Task<(IReadOnlyList<AdminDeliveryOrderListDto> Orders, int TotalCount)> GetDeliveryOrdersAsync(
        string? search, string? status, DateTime? from, DateTime? to,
        string? pincode, int? personnelId,
        int page, int pageSize, CancellationToken ct = default);

    Task<AdminDeliveryOrderDetailDto?> GetDeliveryOrderDetailAsync(int orderId, CancellationToken ct = default);

    // ── Delivery person's own assignments ─────────────────────────────────────

    Task<IReadOnlyList<DeliveryAssignmentDto>> GetMyAssignmentsAsync(
        int personnelId, string? statusFilter, CancellationToken ct = default);

    Task<DeliveryAssignmentDto?> GetMyAssignmentDetailAsync(
        int orderId, int personnelId, CancellationToken ct = default);

    /// <summary>
    /// Transitions an order status. Only the assigned personnel can call this.
    /// Returns (success, error).
    /// </summary>
    Task<(bool Success, string? Error)> UpdateDeliveryStatusAsync(
        int orderId, int personnelId, string userId,
        string newStatus, string? notes,
        CancellationToken ct = default);

    // ── Customer tracking ─────────────────────────────────────────────────────

    Task<DeliveryTrackingDto?> GetTrackingForOrderAsync(
        int orderId, string userId, CancellationToken ct = default);

    // ── Notifications ─────────────────────────────────────────────────────────

    Task<long?> TryQueueNotificationAsync(
        int orderId, string eventType, string recipientUserId,
        string? recipientEmail, string? recipientPhone,
        string channel, CancellationToken ct = default);

    Task MarkNotificationSentAsync(long notificationId, CancellationToken ct = default);

    Task MarkNotificationFailedAsync(long notificationId, string reason, CancellationToken ct = default);

    Task<IReadOnlyList<InAppNotificationDto>> GetInAppNotificationsAsync(
        string userId, int limit, CancellationToken ct = default);
}
