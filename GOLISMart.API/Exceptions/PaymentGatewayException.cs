namespace GOLISMart.API.Exceptions;

/// <summary>
/// Thrown when the Razorpay API (or any upstream payment gateway) returns an error
/// or is unreachable.  Distinct from <see cref="InvalidOperationException"/>, which
/// is used for internal business-rule violations, so controllers can map this to
/// HTTP 503 Service Unavailable rather than 422 Unprocessable Entity.
/// </summary>
public sealed class PaymentGatewayException : Exception
{
    /// <summary>The HTTP status code returned by the gateway, if available.</summary>
    public int? GatewayStatusCode { get; }

    /// <summary>The gateway's own error code (e.g. "BAD_REQUEST_ERROR"), if available.</summary>
    public string? GatewayErrorCode { get; }

    public PaymentGatewayException(string message) : base(message) { }

    public PaymentGatewayException(string message, int gatewayStatusCode, string? gatewayErrorCode = null)
        : base(message)
    {
        GatewayStatusCode = gatewayStatusCode;
        GatewayErrorCode  = gatewayErrorCode;
    }

    public PaymentGatewayException(string message, Exception inner) : base(message, inner) { }
}
