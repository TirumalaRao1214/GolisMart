using GOLISMart.Web.Services;

namespace GOLISMart.Web.Services.Interfaces;

public interface IDeliveryManagementApiClient
{
    // ── Admin: Delivery Orders ─────────────────────────────────────────────────
    Task<PagedResult<AdminDeliveryOrderListDto>?> GetDeliveryOrdersAsync(
        string token, int page = 1, int pageSize = 20,
        string? search = null, string? status = null,
        DateTime? from = null, DateTime? to = null,
        string? pincode = null, int? personnelId = null,
        CancellationToken ct = default);

    Task<AdminDeliveryOrderDetailDto?> GetDeliveryOrderDetailAsync(
        string token, int orderId, CancellationToken ct = default);

    Task<(bool Success, string? Error)> AssignOrderAsync(
        string token, int orderId, AdminAssignDeliveryRequest request, CancellationToken ct = default);

    Task<(bool Success, string? Error)> UnassignOrderAsync(
        string token, int orderId, AdminUnassignDeliveryRequest request, CancellationToken ct = default);

    // ── Admin: Delivery Personnel ──────────────────────────────────────────────
    Task<PagedResult<DeliveryPersonnelDto>?> GetPersonnelAsync(
        string token, int page = 1, int pageSize = 20,
        string? search = null, bool? isActive = null, CancellationToken ct = default);

    Task<DeliveryPersonnelDto?> GetPersonnelByIdAsync(
        string token, int id, CancellationToken ct = default);

    Task<(bool Success, string? Error, DeliveryPersonnelDto? Personnel)> CreatePersonnelAsync(
        string token, AdminCreatePersonnelRequest request, CancellationToken ct = default);

    Task<(bool Success, string? Error)> UpdatePersonnelAsync(
        string token, int id, AdminUpdatePersonnelRequest request, CancellationToken ct = default);

    // ── Delivery Person: My Assignments ───────────────────────────────────────
    Task<IReadOnlyList<DeliveryAssignmentDto>?> GetMyAssignmentsAsync(
        string token, string? status = null, CancellationToken ct = default);

    Task<DeliveryAssignmentDto?> GetMyAssignmentDetailAsync(
        string token, int orderId, CancellationToken ct = default);

    Task<(bool Success, string? Error)> UpdateDeliveryStatusAsync(
        string token, int orderId, UpdateDeliveryStatusRequest request, CancellationToken ct = default);

    Task<(bool Success, string? Error)> ConfirmDeliveredAsync(
        string token, int orderId, ConfirmDeliveredRequest request, CancellationToken ct = default);

    // ── Customer: Tracking + Notifications ────────────────────────────────────
    Task<DeliveryTrackingDto?> GetTrackingAsync(
        string token, int orderId, CancellationToken ct = default);

    Task<IReadOnlyList<InAppNotificationDto>?> GetNotificationsAsync(
        string token, int limit = 20, CancellationToken ct = default);
}
