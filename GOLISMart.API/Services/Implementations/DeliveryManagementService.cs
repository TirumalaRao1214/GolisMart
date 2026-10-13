using GOLISMart.API.DTOs;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Interfaces;

namespace GOLISMart.API.Services.Implementations;

public class DeliveryManagementService : IDeliveryManagementService
{
    private readonly IDeliveryManagementRepository _repo;
    private readonly IAdminRepository _adminRepo;
    private readonly IEnumerable<INotificationProvider> _notificationProviders;
    private readonly ILogger<DeliveryManagementService> _logger;

    public DeliveryManagementService(
        IDeliveryManagementRepository repo,
        IAdminRepository adminRepo,
        IEnumerable<INotificationProvider> notificationProviders,
        ILogger<DeliveryManagementService> logger)
    {
        _repo                  = repo;
        _adminRepo             = adminRepo;
        _notificationProviders = notificationProviders;
        _logger                = logger;
    }

    // ── Delivery Personnel ────────────────────────────────────────────────────

    public Task<(IReadOnlyList<DeliveryPersonnelDto> Items, int TotalCount)> GetPersonnelAsync(
        string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default)
        => _repo.GetPersonnelAsync(search, isActive, page, pageSize, ct);

    public Task<DeliveryPersonnelDto?> GetPersonnelByIdAsync(int id, CancellationToken ct = default)
        => _repo.GetPersonnelByIdAsync(id, ct);

    public async Task<(bool Success, string? Error, int Id)> CreatePersonnelAsync(
        CreateDeliveryPersonnelRequest request, string adminId, CancellationToken ct = default)
    {
        var id = await _repo.CreatePersonnelAsync(request, ct);
        await _adminRepo.LogAdminActionAsync(adminId, "CreateDeliveryPersonnel", "DeliveryPersonnel",
            id.ToString(), $"Created '{request.FullName}'", ct);
        return (true, null, id);
    }

    public async Task<(bool Success, string? Error)> UpdatePersonnelAsync(
        int id, UpdateDeliveryPersonnelRequest request, string adminId, CancellationToken ct = default)
    {
        // If deactivating, warn if they have active assignments
        if (!request.IsActive)
        {
            var count = await _repo.GetActiveAssignmentCountAsync(id, ct);
            if (count > 0)
                return (false,
                    $"Cannot deactivate: {count} active delivery assignment(s) are still open. " +
                    "Reassign or complete those deliveries first.");
        }

        var updated = await _repo.UpdatePersonnelAsync(id, request, ct);
        if (!updated) return (false, "Delivery personnel not found.");

        await _adminRepo.LogAdminActionAsync(adminId, "UpdateDeliveryPersonnel", "DeliveryPersonnel",
            id.ToString(), $"Updated '{request.FullName}', IsActive={request.IsActive}", ct);
        return (true, null);
    }

    // ── Delivery Assignment (admin) ───────────────────────────────────────────

    public async Task<(bool Success, string? Error)> AssignOrderAsync(
        int orderId, int personnelId, string adminId, string? notes, CancellationToken ct = default)
    {
        var (success, error) = await _repo.AssignOrderAsync(orderId, personnelId, adminId, notes, ct);
        if (success)
        {
            await _adminRepo.LogAdminActionAsync(adminId, "AssignDelivery", "Order",
                orderId.ToString(), $"Assigned to personnel #{personnelId}", ct);
        }
        return (success, error);
    }

    public async Task<(bool Success, string? Error)> UnassignOrderAsync(
        int orderId, string adminId, string reason, CancellationToken ct = default)
    {
        var (success, error) = await _repo.UnassignOrderAsync(orderId, adminId, reason, ct);
        if (success)
        {
            await _adminRepo.LogAdminActionAsync(adminId, "UnassignDelivery", "Order",
                orderId.ToString(), $"Reason: {reason}", ct);
        }
        return (success, error);
    }

    // ── Admin delivery list ───────────────────────────────────────────────────

    public Task<(IReadOnlyList<AdminDeliveryOrderListDto> Orders, int TotalCount)> GetDeliveryOrdersAsync(
        string? search, string? status, DateTime? from, DateTime? to,
        string? pincode, int? personnelId,
        int page, int pageSize, CancellationToken ct = default)
        => _repo.GetDeliveryOrdersAsync(search, status, from, to, pincode, personnelId, page, pageSize, ct);

    public Task<AdminDeliveryOrderDetailDto?> GetDeliveryOrderDetailAsync(int orderId, CancellationToken ct = default)
        => _repo.GetDeliveryOrderDetailAsync(orderId, ct);

    // ── Delivery person's dashboard ───────────────────────────────────────────

    public async Task<IReadOnlyList<DeliveryAssignmentDto>> GetMyAssignmentsAsync(
        string userId, string? statusFilter, CancellationToken ct = default)
    {
        var personnel = await _repo.GetPersonnelByUserIdAsync(userId, ct);
        if (personnel is null || !personnel.IsActive) return [];
        return await _repo.GetMyAssignmentsAsync(personnel.Id, statusFilter, ct);
    }

    public async Task<DeliveryAssignmentDto?> GetMyAssignmentDetailAsync(
        int orderId, string userId, CancellationToken ct = default)
    {
        var personnel = await _repo.GetPersonnelByUserIdAsync(userId, ct);
        if (personnel is null || !personnel.IsActive) return null;
        return await _repo.GetMyAssignmentDetailAsync(orderId, personnel.Id, ct);
    }

    public async Task<(bool Success, string? Error)> UpdateDeliveryStatusAsync(
        int orderId, string userId, string newStatus, string? notes, CancellationToken ct = default)
    {
        var personnel = await _repo.GetPersonnelByUserIdAsync(userId, ct);
        if (personnel is null || !personnel.IsActive)
            return (false, "Delivery personnel profile not found or inactive.");

        var (success, error) = await _repo.UpdateDeliveryStatusAsync(
            orderId, personnel.Id, userId, newStatus, notes, ct);

        return (success, error);
    }

    // ── Customer tracking ─────────────────────────────────────────────────────

    public Task<DeliveryTrackingDto?> GetTrackingForOrderAsync(
        int orderId, string userId, CancellationToken ct = default)
        => _repo.GetTrackingForOrderAsync(orderId, userId, ct);

    // ── In-app notifications ──────────────────────────────────────────────────

    public Task<IReadOnlyList<InAppNotificationDto>> GetInAppNotificationsAsync(
        string userId, int limit = 20, CancellationToken ct = default)
        => _repo.GetInAppNotificationsAsync(userId, limit, ct);

    public async Task FireNotificationsAsync(
        int orderId, string orderNumber, string eventType,
        string recipientUserId, string? recipientEmail, string? recipientPhone,
        CancellationToken ct = default)
    {
        foreach (var provider in _notificationProviders)
        {
            try
            {
                var notifId = await _repo.TryQueueNotificationAsync(
                    orderId, eventType, recipientUserId,
                    recipientEmail, recipientPhone,
                    provider.Channel, ct);

                if (notifId is null)
                {
                    // Already queued/sent — skip (idempotency)
                    _logger.LogDebug(
                        "Notification {EventType} for order {OrderId} channel {Channel} already exists — skipped.",
                        eventType, orderId, provider.Channel);
                    continue;
                }

                await provider.SendAsync(
                    notifId.Value, orderId, orderNumber, eventType,
                    recipientUserId, recipientEmail, recipientPhone, ct);
            }
            catch (Exception ex)
            {
                // Notification failure must NOT propagate — log and continue
                _logger.LogError(ex,
                    "Notification provider {Channel} failed for event {EventType} order {OrderId}",
                    provider.Channel, eventType, orderId);
            }
        }
    }
}
