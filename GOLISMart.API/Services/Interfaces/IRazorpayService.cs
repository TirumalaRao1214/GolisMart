using GOLISMart.API.DTOs;

namespace GOLISMart.API.Services.Interfaces;

public interface IRazorpayService
{
    /// <summary>
    /// Creates a Razorpay order for the authenticated customer's current cart.
    /// Internally creates a GOLIS Mart order with status Pending / PaymentStatus Pending,
    /// then calls the Razorpay Orders API.
    /// </summary>
    Task<CreateRazorpayOrderResponse> CreateOrderAsync(
        CreateRazorpayOrderRequest request,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Verifies a Razorpay payment signature after the browser callback.
    /// On success marks the payment Captured and the order PaymentStatus Paid.
    /// </summary>
    Task<VerifyRazorpayPaymentResponse> VerifyPaymentAsync(
        VerifyRazorpayPaymentRequest request,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Processes an inbound Razorpay webhook event.
    /// Returns true if the event was processed; false if it was a duplicate.
    /// </summary>
    Task<bool> ProcessWebhookAsync(
        string gatewayEventId,
        string eventType,
        string rawPayload,
        CancellationToken ct = default);
}
