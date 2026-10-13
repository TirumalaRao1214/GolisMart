using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Interfaces;

namespace GOLISMart.API.Services.Implementations;

/// <summary>
/// In-app notification provider.
/// Stores the notification record in NotificationEvents and marks it Sent immediately —
/// the in-app inbox is served from the database, so "sending" is simply marking it as delivered.
/// </summary>
public class InAppNotificationProvider : INotificationProvider
{
    private readonly IDeliveryManagementRepository _repo;
    private readonly ILogger<InAppNotificationProvider> _logger;

    public string Channel => "InApp";

    public InAppNotificationProvider(
        IDeliveryManagementRepository repo,
        ILogger<InAppNotificationProvider> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task<bool> SendAsync(
        long notificationId,
        int orderId,
        string orderNumber,
        string eventType,
        string recipientUserId,
        string? recipientEmail,
        string? recipientPhone,
        CancellationToken ct = default)
    {
        try
        {
            await _repo.MarkNotificationSentAsync(notificationId, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "InAppNotificationProvider failed to mark notification {Id} as sent", notificationId);
            try
            {
                await _repo.MarkNotificationFailedAsync(notificationId, ex.Message, ct);
            }
            catch { /* swallow secondary failure */ }
            return false;
        }
    }
}
