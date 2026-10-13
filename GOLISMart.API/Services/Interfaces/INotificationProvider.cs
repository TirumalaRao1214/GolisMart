namespace GOLISMart.API.Services.Interfaces;

/// <summary>
/// Abstraction for sending order/delivery notifications.
/// Implementations may be in-app, email, or WhatsApp.
/// </summary>
public interface INotificationProvider
{
    /// <summary>
    /// Sends a notification for the given event.
    /// Returns true if sent, false if skipped/unavailable.
    /// Must never throw — failures should be logged internally.
    /// </summary>
    Task<bool> SendAsync(
        long notificationId,
        int orderId,
        string orderNumber,
        string eventType,
        string recipientUserId,
        string? recipientEmail,
        string? recipientPhone,
        CancellationToken ct = default);

    /// <summary>Channel name this provider handles (e.g. "InApp", "Email").</summary>
    string Channel { get; }
}
