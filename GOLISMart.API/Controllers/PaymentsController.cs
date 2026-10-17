using System.Security.Claims;
using System.Text;
using GOLISMart.API.DTOs;
using GOLISMart.API.Exceptions;
using GOLISMart.API.Services.Implementations;
using GOLISMart.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace GOLISMart.API.Controllers;

[ApiController]
[Route("api/v1/payments")]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IRazorpayService _razorpay;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IRazorpayService razorpay,
        IConfiguration config,
        ILogger<PaymentsController> logger)
    {
        _razorpay = razorpay;
        _config   = config;
        _logger   = logger;
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    // ── POST /api/v1/payments/razorpay/create-order ───────────────────────────

    /// <summary>
    /// Creates a GOLIS Mart order and a corresponding Razorpay order.
    /// Returns the public Key ID and gateway order ID for use in the browser SDK.
    /// The Key Secret is never returned.
    /// </summary>
    [HttpPost("razorpay/create-order")]
    [Authorize]
    [SwaggerOperation(Summary = "Create a Razorpay order for online payment")]
    [ProducesResponseType(typeof(CreateRazorpayOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateRazorpayOrder(
        [FromBody] CreateRazorpayOrderRequest request,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ErrorResponse("Authentication required."));

        if (string.IsNullOrWhiteSpace(request.SessionKey))
            return BadRequest(new ErrorResponse("SessionKey is required."));
        if (request.AddressId <= 0)
            return BadRequest(new ErrorResponse("A valid AddressId is required."));

        // Validate Razorpay credentials are configured before proceeding
        var keyId = _config["Razorpay:KeyId"]?.Trim();
        if (string.IsNullOrWhiteSpace(keyId))
        {
            _logger.LogError("Razorpay:KeyId is not configured. Cannot create Razorpay order.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse("Payment service is not configured. Please contact support."));
        }

        try
        {
            var result = await _razorpay.CreateOrderAsync(request, userId, ct);

            // Inject the public key (KeyId) — service returns empty string placeholder
            var response = result with { KeyId = keyId };
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation(ex,
                "[{TraceId}] Razorpay create-order: validation error for user {UserId}",
                HttpContext.TraceIdentifier, userId);
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (PaymentGatewayException ex)
        {
            // Log the full gateway error detail (status + Razorpay error code) for diagnosis.
            // GatewayStatusCode is null when the failure was a network/TLS error before the
            // HTTP response was received.
            _logger.LogError(ex,
                "[{TraceId}] Razorpay create-order: gateway error " +
                "(HTTP {GatewayStatus}, code={GatewayCode}, inner={InnerType}) for user {UserId}",
                HttpContext.TraceIdentifier,
                ex.GatewayStatusCode?.ToString() ?? "no-response",
                ex.GatewayErrorCode ?? "n/a",
                ex.InnerException?.GetType().Name ?? "none",
                userId);

            // In Development, include the gateway status + code in the response so the developer
            // can read it directly without needing console access.
            // In Production, return only the safe generic message.
            var isDev = HttpContext.RequestServices
                .GetRequiredService<IWebHostEnvironment>()
                .IsDevelopment();

            var devDetail = isDev
                ? $" [dev: HTTP {ex.GatewayStatusCode?.ToString() ?? "network-error"}" +
                  $", code={ex.GatewayErrorCode ?? "n/a"}" +
                  $", inner={ex.InnerException?.Message ?? ex.Message}]"
                : string.Empty;

            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ErrorResponse(
                    $"Payment gateway unavailable. Please try again or choose Cash on Delivery.{devDetail}",
                    HttpContext.TraceIdentifier));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation(ex,
                "[{TraceId}] Razorpay create-order: business rule error for user {UserId}",
                HttpContext.TraceIdentifier, userId);
            return UnprocessableEntity(new ErrorResponse(ex.Message, HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{TraceId}] Razorpay create-order: unexpected error ({ExType}) for user {UserId}",
                HttpContext.TraceIdentifier, ex.GetType().Name, userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse("An unexpected error occurred. Please try again.", HttpContext.TraceIdentifier));
        }
    }

    // ── POST /api/v1/payments/razorpay/verify ─────────────────────────────────

    /// <summary>
    /// Verifies the payment signature returned by the Razorpay SDK after the customer
    /// completes payment. On success marks the internal order as Paid.
    /// </summary>
    [HttpPost("razorpay/verify")]
    [Authorize]
    [SwaggerOperation(Summary = "Verify a Razorpay payment signature")]
    [ProducesResponseType(typeof(VerifyRazorpayPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyRazorpayPayment(
        [FromBody] VerifyRazorpayPaymentRequest request,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new ErrorResponse("Authentication required."));

        if (string.IsNullOrWhiteSpace(request.RazorpayOrderId)  ||
            string.IsNullOrWhiteSpace(request.RazorpayPaymentId)||
            string.IsNullOrWhiteSpace(request.RazorpaySignature))
        {
            return BadRequest(new ErrorResponse("razorpay_order_id, razorpay_payment_id, and razorpay_signature are required."));
        }

        try
        {
            var result = await _razorpay.VerifyPaymentAsync(request, userId, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{TraceId}] Razorpay verify: unexpected error for user {UserId}",
                HttpContext.TraceIdentifier, userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse("An unexpected error occurred during payment verification."));
        }
    }

    // ── POST /api/v1/payments/razorpay/webhook ────────────────────────────────

    /// <summary>
    /// Razorpay webhook receiver.
    /// Validates the X-Razorpay-Signature header before processing.
    /// Must NOT require JWT authentication (Razorpay calls this endpoint directly).
    /// </summary>
    [HttpPost("razorpay/webhook")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Razorpay webhook endpoint")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RazorpayWebhook(CancellationToken ct = default)
    {
        // ── 1. Read raw body (must be done before any framework body readers) ─
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        // ── 2. Validate webhook secret ────────────────────────────────────────
        var webhookSecret = _config["Razorpay:WebhookSecret"];
        if (string.IsNullOrEmpty(webhookSecret))
        {
            _logger.LogError("Razorpay:WebhookSecret is not configured.");
            // Return 200 so Razorpay does not retry — this is a configuration error.
            return Ok();
        }

        var signature = Request.Headers["X-Razorpay-Signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("Webhook request missing X-Razorpay-Signature header.");
            return BadRequest(new ErrorResponse("Missing signature header."));
        }

        if (!RazorpayService.ValidateWebhookSignature(rawBody, signature, webhookSecret))
        {
            _logger.LogWarning("Webhook signature validation failed.");
            return BadRequest(new ErrorResponse("Signature validation failed."));
        }

        // ── 3. Parse event type and event ID ─────────────────────────────────
        string eventType;
        string eventId;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
            eventType = doc.RootElement.GetProperty("event").GetString() ?? "unknown";
            // Use Razorpay's event id if present, else generate a content hash
            eventId = doc.RootElement.TryGetProperty("id", out var id)
                ? (id.GetString() ?? GenerateEventId(rawBody))
                : GenerateEventId(rawBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse webhook payload.");
            return BadRequest(new ErrorResponse("Invalid JSON payload."));
        }

        _logger.LogInformation(
            "Razorpay webhook received: event={Event}, id={EventId}", eventType, eventId);

        try
        {
            var processed = await _razorpay.ProcessWebhookAsync(eventId, eventType, rawBody, ct);
            return Ok(new { received = true, processed });
        }
        catch (Exception ex)
        {
            // Return 500 so Razorpay retries
            _logger.LogError(ex, "Webhook processing failed for event {EventId}", eventId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse("Webhook processing failed."));
        }
    }

    // ── GET /api/v1/payments/razorpay/smoke-test ──────────────────────────────

    /// <summary>
    /// DEVELOPMENT ONLY — creates a test Razorpay order for ₹10 to verify credentials.
    /// Disabled automatically in non-Development environments.
    /// </summary>
    [HttpGet("razorpay/smoke-test")]
    [Authorize]
    [SwaggerOperation(Summary = "DEV ONLY — smoke-test Razorpay connection (₹10 test order)")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SmokeTest(
        [FromServices] IWebHostEnvironment env,
        CancellationToken ct = default)
    {
        if (!env.IsDevelopment())
            return StatusCode(StatusCodes.Status403Forbidden,
                new ErrorResponse("This endpoint is only available in Development."));

        var keyId     = _config["Razorpay:KeyId"]?.Trim();
        var keySecret = _config["Razorpay:KeySecret"]?.Trim();

        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(keySecret))
            return BadRequest(new ErrorResponse("Razorpay:KeyId or Razorpay:KeySecret is not configured."));

        // Create a minimal ₹10 order directly via the Razorpay API
        try
        {
            var payload = new { amount = 1000L, currency = "INR", receipt = "smoke-test" };
            var json    = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
            using var req   = new HttpRequestMessage(HttpMethod.Post, "https://api.razorpay.com/v1/orders")
            {
                Content = content
            };
            req.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            using var httpClient = new HttpClient();
            var response = await httpClient.SendAsync(req, ct);
            var body     = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode,
                    new { success = false, error = "Razorpay API returned an error.", detail = body });
            }

            return Ok(new
            {
                success = true,
                message = "Razorpay test order created successfully (DEV ONLY — ₹10).",
                response = System.Text.Json.JsonDocument.Parse(body).RootElement
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Smoke test failed");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { success = false, error = ex.Message });
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string GenerateEventId(string rawBody)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(rawBody));
        return "evt-" + Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
