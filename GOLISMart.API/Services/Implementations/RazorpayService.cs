using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GOLISMart.API.DTOs;
using GOLISMart.API.Exceptions;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Interfaces;

namespace GOLISMart.API.Services.Implementations;

/// <summary>
/// Razorpay Test Mode integration service.
/// Covers order creation, signature verification, and webhook processing.
/// </summary>
public class RazorpayService : IRazorpayService
{
    // ── Dependencies ────────────────────────────────────────────────────────────
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IInventoryRepository _inventoryRepo;
    private readonly IAddressRepository _addressRepo;
    private readonly IDeliveryService _deliveryService;
    private readonly IOrderRepository _orderRepo;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IUserRepository _userRepo;
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<RazorpayService> _logger;

    // ── Configuration ───────────────────────────────────────────────────────────
    private string KeyId     => (_config["Razorpay:KeyId"]     ?? throw new InvalidOperationException("Razorpay:KeyId is not configured.")).Trim();
    private string KeySecret => (_config["Razorpay:KeySecret"] ?? throw new InvalidOperationException("Razorpay:KeySecret is not configured.")).Trim();

    private decimal DefaultDeliveryFee     => _config.GetValue<decimal>("CartSettings:DeliveryFee", 40m);
    private decimal DefaultFreeDeliveryAbove => _config.GetValue<decimal>("CartSettings:FreeDeliveryAbove", 500m);

    private static readonly HashSet<string> AllowedPaymentMethods =
        new(StringComparer.OrdinalIgnoreCase) { "COD", "UPI", "Card", "NetBanking", "Wallet", "Online" };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull
    };

    public RazorpayService(
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IInventoryRepository inventoryRepo,
        IAddressRepository addressRepo,
        IDeliveryService deliveryService,
        IOrderRepository orderRepo,
        IPaymentRepository paymentRepo,
        IUserRepository userRepo,
        HttpClient http,
        IConfiguration config,
        ILogger<RazorpayService> logger)
    {
        _cartRepo       = cartRepo;
        _productRepo    = productRepo;
        _inventoryRepo  = inventoryRepo;
        _addressRepo    = addressRepo;
        _deliveryService = deliveryService;
        _orderRepo      = orderRepo;
        _paymentRepo    = paymentRepo;
        _userRepo       = userRepo;
        _http           = http;
        _config         = config;
        _logger         = logger;
    }

    // ── CreateOrderAsync ───────────────────────────────────────────────────────

    public async Task<CreateRazorpayOrderResponse> CreateOrderAsync(
        CreateRazorpayOrderRequest request,
        string userId,
        CancellationToken ct = default)
    {
        // ── 0. Idempotency check ───────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _orderRepo.GetByIdempotencyKeyAsync(
                request.IdempotencyKey, userId, ct);

            if (existing is not null)
            {
                // Return the existing Razorpay order details
                var existingPayment = await _paymentRepo.GetByOrderIdAsync(existing.Id, ct);
                if (existingPayment?.GatewayOrderId is not null)
                {
                    var user2 = await _userRepo.GetByIdAsync(userId, ct)!;
                    return BuildResponse(existing, existingPayment, user2!);
                }
            }
        }

        // ── 1. Load and validate cart ─────────────────────────────────────────
        var cart = await _cartRepo.GetBySessionKeyAsync(request.SessionKey, ct)
            ?? throw new InvalidOperationException("Your cart is empty. Please add items before placing an order.");

        var cartItems = await _cartRepo.GetItemsAsync(cart.Id, ct);
        if (cartItems.Count == 0)
            throw new InvalidOperationException("Your cart is empty. Please add items before placing an order.");

        // ── 2. Verify address ownership ───────────────────────────────────────
        var address = await _addressRepo.GetByIdAndUserIdAsync(request.AddressId, userId, ct)
            ?? throw new ArgumentException("The selected delivery address was not found.");

        // ── 3. Check delivery serviceability ──────────────────────────────────
        var serviceability = await _deliveryService.CheckServiceabilityAsync(address.Pincode, ct);
        if (!serviceability.IsServiceable)
            throw new InvalidOperationException(
                serviceability.UnavailableReason ?? "Delivery is not available to the selected address.");

        // ── 4. Revalidate products + stock, compute server-side totals ─────────
        var productIds   = cartItems.Select(i => i.ProductId).Distinct().ToList();
        var inventoryMap = await _inventoryRepo.GetByProductIdsAsync(productIds, ct);

        var orderItems   = new List<OrderItem>();
        decimal subTotal = 0m;

        foreach (var ci in cartItems)
        {
            var product = await _productRepo.GetByIdAsync(ci.ProductId, ct);

            if (product is null || !product.IsActive)
                throw new InvalidOperationException(
                    $"Product (ID {ci.ProductId}) is no longer available. Please update your cart.");

            inventoryMap.TryGetValue(ci.ProductId, out var inv);

            if (inv is null || !inv.IsInStock)
                throw new InvalidOperationException($"'{product.Name}' is currently out of stock.");

            if (ci.Quantity <= 0 || ci.Quantity > 20)
                throw new InvalidOperationException($"Invalid quantity for '{product.Name}'.");

            if (inv.AvailableQuantity < ci.Quantity)
                throw new InvalidOperationException(
                    $"Only {inv.AvailableQuantity} unit(s) of '{product.Name}' are available.");

            var unitPrice      = product.Price;
            var offerPrice     = product.OfferPrice;
            var effectivePrice = offerPrice ?? unitPrice;
            var lineTotal      = Math.Round(effectivePrice * ci.Quantity, 2);

            subTotal += lineTotal;

            orderItems.Add(new OrderItem
            {
                ProductId      = product.Id,
                ProductName    = product.Name,
                ProductSKU     = product.SKU,
                UnitLabel      = product.UnitLabel,
                Quantity       = ci.Quantity,
                UnitPrice      = unitPrice,
                OfferPrice     = offerPrice,
                EffectivePrice = effectivePrice,
                LineTotal      = lineTotal
            });
        }

        // ── 5. Compute delivery fee ────────────────────────────────────────────
        var rawDeliveryFee = serviceability.DeliveryFee ?? DefaultDeliveryFee;
        var freeAbove      = serviceability.FreeDeliveryAbove ?? DefaultFreeDeliveryAbove;
        var deliveryFee    = subTotal >= freeAbove ? 0m : rawDeliveryFee;
        var totalAmount    = subTotal + deliveryFee;

        var minOrder = serviceability.MinOrderValue;
        if (minOrder.HasValue && subTotal < minOrder.Value)
            throw new InvalidOperationException(
                $"A minimum order of ₹{minOrder:N2} is required for delivery to this area.");

        // ── 6. Build and persist the internal GOLIS Mart order ────────────────
        var orderNumber = GenerateOrderNumber();

        var order = new Order
        {
            OrderNumber         = orderNumber,
            UserId              = userId,
            ShipToAddressLine1  = $"{address.HouseNumber}, {address.BuildingStreet}",
            ShipToAddressLine2  = string.IsNullOrWhiteSpace(address.Locality)
                                      ? address.Landmark
                                      : address.Locality + (address.Landmark != null ? $", near {address.Landmark}" : ""),
            ShipToCity          = address.City,
            ShipToState         = address.State,
            ShipToPincode       = address.Pincode,
            SubTotal            = subTotal,
            DiscountAmount      = 0m,
            DeliveryFee         = deliveryFee,
            TotalAmount         = totalAmount,
            PaymentMethod       = "Online",
            PaymentStatus       = "Pending",
            Status              = "Pending",
            Notes               = request.Notes?.Trim(),
            IdempotencyKey      = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                                      ? null
                                      : request.IdempotencyKey.Trim()
        };

        var createdOrder = await _orderRepo.CreateOrderAsync(order, orderItems, ct);

        // ── 7. Convert total to paise (integer arithmetic to avoid float drift) ─
        // totalAmount is DECIMAL(10,2) — multiply by 100 and round.
        long amountPaise = (long)Math.Round(totalAmount * 100m, MidpointRounding.AwayFromZero);

        // ── 8. Call Razorpay Orders API ────────────────────────────────────────
        // If Razorpay rejects or is unreachable, void the internal order so it does
        // not accumulate as a dangling Pending row with no payment association.
        var receipt = $"GM-{createdOrder.Id}-{DateTime.UtcNow:yyyyMMdd}";

        string razorpayOrderId;
        try
        {
            razorpayOrderId = await CallRazorpayCreateOrderAsync(amountPaise, receipt, ct);
        }
        catch
        {
            // Void the order — re-throw so the controller still returns 503.
            // Inventory was already deducted inside the transaction in CreateOrderAsync;
            // VoidOrderAsync restores it and marks the order Cancelled.
            try
            {
                await _orderRepo.VoidOrderAsync(createdOrder.Id, ct);
                _logger.LogInformation(
                    "[{CorrelationId}] Order {OrderNumber} voided after Razorpay call failure",
                    Activity.Current?.Id ?? "n/a", createdOrder.OrderNumber);
            }
            catch (Exception voidEx)
            {
                _logger.LogError(voidEx,
                    "[{CorrelationId}] Failed to void order {OrderNumber} after Razorpay failure",
                    Activity.Current?.Id ?? "n/a", createdOrder.OrderNumber);
            }
            throw;
        }

        // ── 9. Persist payment attempt ────────────────────────────────────────
        var payment = await _paymentRepo.CreateAsync(new Payment
        {
            OrderId        = createdOrder.Id,
            GatewayName    = "Razorpay",
            GatewayOrderId = razorpayOrderId,
            Amount         = totalAmount,
            Currency       = "INR",
            Status         = "Initiated"
        }, ct);

        _logger.LogInformation(
            "[{CorrelationId}] Razorpay order {RazorpayOrderId} created for internal order {OrderNumber} (₹{Amount})",
            Activity.Current?.Id ?? "n/a", razorpayOrderId, createdOrder.OrderNumber, totalAmount);

        // ── 10. Load user for prefill ──────────────────────────────────────────
        var user = await _userRepo.GetByIdAsync(userId, ct);

        return BuildResponse(createdOrder, payment, user);
    }

    // ── VerifyPaymentAsync ─────────────────────────────────────────────────────

    public async Task<VerifyRazorpayPaymentResponse> VerifyPaymentAsync(
        VerifyRazorpayPaymentRequest request,
        string userId,
        CancellationToken ct = default)
    {
        // ── 1. Basic input validation ──────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(request.RazorpayOrderId)  ||
            string.IsNullOrWhiteSpace(request.RazorpayPaymentId)||
            string.IsNullOrWhiteSpace(request.RazorpaySignature))
        {
            return Fail("Invalid payment details. Missing required fields.");
        }

        // ── 2. Verify HMAC-SHA256 signature ────────────────────────────────────
        // Razorpay spec: HMAC_SHA256(key_secret, razorpay_order_id + "|" + razorpay_payment_id)
        var expectedSignature = ComputeHmacSha256(
            $"{request.RazorpayOrderId}|{request.RazorpayPaymentId}",
            KeySecret);

        if (!CryptographicEquals(expectedSignature, request.RazorpaySignature))
        {
            _logger.LogWarning(
                "[{CorrelationId}] Razorpay signature mismatch for order {RazorpayOrderId}",
                Activity.Current?.Id ?? "n/a", request.RazorpayOrderId);
            return Fail("Payment signature verification failed. Your payment has not been processed.");
        }

        // ── 3. Load payment record ─────────────────────────────────────────────
        var payment = await _paymentRepo.GetByGatewayOrderIdAsync(request.RazorpayOrderId, ct);
        if (payment is null)
        {
            _logger.LogWarning("No payment record found for Razorpay order {Id}", request.RazorpayOrderId);
            return Fail("Payment record not found.");
        }

        // ── 4. Already captured — idempotent success ──────────────────────────
        if (string.Equals(payment.Status, "Captured", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Payment {PaymentId} already captured — duplicate verify request.", payment.Id);
            var ord2 = await _orderRepo.GetByIdAndUserIdAsync(payment.OrderId, userId, ct);
            if (ord2 is null) return Fail("Order not found.");
            return Success(ord2);
        }

        // ── 5. Load and validate the internal order ────────────────────────────
        var order = await _orderRepo.GetByIdAndUserIdAsync(payment.OrderId, userId, ct);
        if (order is null)
        {
            _logger.LogWarning(
                "Order {OrderId} not found or does not belong to user {UserId}",
                payment.OrderId, userId);
            return Fail("Order not found or access denied.");
        }

        // ── 6. Verify payment with Razorpay API ────────────────────────────────
        RazorpayPaymentApiResponse? rzPayment = null;
        try
        {
            rzPayment = await FetchRazorpayPaymentAsync(request.RazorpayPaymentId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{CorrelationId}] Failed to fetch payment {PaymentId} from Razorpay",
                Activity.Current?.Id ?? "n/a", request.RazorpayPaymentId);
            // Proceed with signature-only verification as fallback; mark Captured
        }

        if (rzPayment is not null)
        {
            // Validate order ID matches
            if (!string.Equals(rzPayment.OrderId, request.RazorpayOrderId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Razorpay payment {PmtId} belongs to order {ActualOrderId}, not {ExpectedOrderId}",
                    request.RazorpayPaymentId, rzPayment.OrderId, request.RazorpayOrderId);
                return Fail("Payment order mismatch.");
            }

            // Validate amount (paise)
            long expectedPaise = (long)Math.Round(order.TotalAmount * 100m, MidpointRounding.AwayFromZero);
            if (rzPayment.Amount != expectedPaise)
            {
                _logger.LogWarning(
                    "Amount mismatch: expected {Expected} paise, got {Actual} paise for order {OrderId}",
                    expectedPaise, rzPayment.Amount, order.Id);
                return Fail("Payment amount does not match the order total.");
            }

            // Require currency INR
            if (!string.Equals(rzPayment.Currency, "INR", StringComparison.OrdinalIgnoreCase))
            {
                return Fail("Invalid currency. Only INR is accepted.");
            }

            // Require captured status
            if (!rzPayment.Captured &&
                !string.Equals(rzPayment.Status, "captured", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Razorpay payment {PmtId} status is {Status} (not captured)", request.RazorpayPaymentId, rzPayment.Status);
                await _paymentRepo.MarkFailedAsync(payment.Id, $"Gateway status: {rzPayment.Status}", ct);
                return Fail("Payment has not been captured by the gateway.");
            }
        }

        // ── 7. Mark payment as Captured and order PaymentStatus as Paid ─────────
        var captured = await _paymentRepo.CaptureAsync(
            payment.Id,
            request.RazorpayPaymentId,
            rzPayment?.Method,
            ct);

        if (!captured)
        {
            // Already captured (concurrent request), still return success
            _logger.LogInformation("Payment {PaymentId} concurrent capture — already captured.", payment.Id);
        }

        // Reload order to get fresh status
        var updatedOrder = await _orderRepo.GetByIdAndUserIdAsync(order.Id, userId, ct) ?? order;

        _logger.LogInformation(
            "Payment verified and captured for order {OrderNumber}, PaymentId {PaymentId}",
            updatedOrder.OrderNumber, request.RazorpayPaymentId);

        return Success(updatedOrder);
    }

    // ── ProcessWebhookAsync ────────────────────────────────────────────────────

    public async Task<bool> ProcessWebhookAsync(
        string gatewayEventId,
        string eventType,
        string rawPayload,
        CancellationToken ct = default)
    {
        // ── 1. Record the event (idempotency check inside) ─────────────────────
        var evt = await _paymentRepo.RecordWebhookEventAsync(new PaymentWebhookEvent
        {
            GatewayName    = "Razorpay",
            EventType      = eventType,
            GatewayEventId = gatewayEventId,
            RawPayload     = rawPayload
        }, ct);

        if (evt is null)
        {
            _logger.LogInformation("Webhook event {EventId} already processed — skipping.", gatewayEventId);
            return false; // duplicate
        }

        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            switch (eventType)
            {
                case "payment.captured":
                    await HandlePaymentCapturedAsync(root, evt, ct);
                    break;

                case "payment.failed":
                    await HandlePaymentFailedAsync(root, evt, ct);
                    break;

                default:
                    // Acknowledge unknown events without error
                    _logger.LogDebug("Razorpay webhook: unhandled event type {EventType}", eventType);
                    break;
            }

            await _paymentRepo.MarkWebhookEventProcessedAsync(evt.Id, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook event {EventId} processing failed", gatewayEventId);
            await _paymentRepo.MarkWebhookEventErrorAsync(evt.Id, ex.Message, ct);
            throw; // Let the controller return 500 so Razorpay will retry
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private async Task HandlePaymentCapturedAsync(
        JsonElement root,
        PaymentWebhookEvent evt,
        CancellationToken ct)
    {
        var entity = root
            .GetProperty("payload")
            .GetProperty("payment")
            .GetProperty("entity");

        var razorpayOrderId  = entity.GetProperty("order_id").GetString();
        var razorpayPaymentId = entity.GetProperty("id").GetString();
        var method           = entity.TryGetProperty("method", out var m) ? m.GetString() : null;

        if (string.IsNullOrEmpty(razorpayOrderId) || string.IsNullOrEmpty(razorpayPaymentId))
        {
            _logger.LogWarning("payment.captured webhook missing order_id or id in payload.");
            return;
        }

        var payment = await _paymentRepo.GetByGatewayOrderIdAsync(razorpayOrderId, ct);
        if (payment is null)
        {
            _logger.LogWarning("Webhook payment.captured: no payment record for Razorpay order {OrderId}", razorpayOrderId);
            return;
        }

        // Update the evt's PaymentId for audit trail
        evt.PaymentId = payment.Id;

        if (string.Equals(payment.Status, "Captured", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Webhook payment.captured: payment {PaymentId} already captured.", payment.Id);
            return;
        }

        await _paymentRepo.CaptureAsync(payment.Id, razorpayPaymentId, method, ct);
        _logger.LogInformation(
            "Webhook payment.captured: payment {PaymentId} captured for Razorpay order {OrderId}",
            payment.Id, razorpayOrderId);
    }

    private async Task HandlePaymentFailedAsync(
        JsonElement root,
        PaymentWebhookEvent evt,
        CancellationToken ct)
    {
        var entity = root
            .GetProperty("payload")
            .GetProperty("payment")
            .GetProperty("entity");

        var razorpayOrderId = entity.GetProperty("order_id").GetString();
        var errorDesc       = entity.TryGetProperty("error_description", out var ed)
            ? ed.GetString()
            : "Payment failed";

        if (string.IsNullOrEmpty(razorpayOrderId)) return;

        var payment = await _paymentRepo.GetByGatewayOrderIdAsync(razorpayOrderId, ct);
        if (payment is null) return;

        evt.PaymentId = payment.Id;

        if (!string.Equals(payment.Status, "Captured", StringComparison.OrdinalIgnoreCase))
        {
            await _paymentRepo.MarkFailedAsync(payment.Id, errorDesc, ct);
            _logger.LogInformation(
                "Webhook payment.failed: payment {PaymentId} marked Failed ({Reason})",
                payment.Id, errorDesc);
        }
    }

    // ── Razorpay REST API helpers ──────────────────────────────────────────────

    private async Task<string> CallRazorpayCreateOrderAsync(
        long amountPaise,
        string receipt,
        CancellationToken ct)
    {
        var payload = new
        {
            amount   = amountPaise,
            currency = "INR",
            receipt  = receipt.Length > 40 ? receipt[..40] : receipt
        };

        var json    = JsonSerializer.Serialize(payload, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/orders")
        {
            Content = content
        };
        req.Headers.Authorization = BuildBasicAuth(KeyId, KeySecret);

        HttpResponseMessage response;
        string body;
        try
        {
            response = await _http.SendAsync(req, ct);
            body     = await response.Content.ReadAsStringAsync(ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "[{CorrelationId}] Could not reach Razorpay API (network error)",
                Activity.Current?.Id ?? "n/a");
            throw new PaymentGatewayException(
                "Unable to reach the payment gateway. Please try again.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            // Extract error.code and error.description from Razorpay's JSON error body
            // so we can log actionable detail without exposing secrets.
            var (errCode, errDesc) = ExtractRazorpayError(body);

            _logger.LogError(
                "[{CorrelationId}] Razorpay Orders API returned {Status} — code={ErrCode}, description={ErrDesc}",
                Activity.Current?.Id ?? "n/a",
                (int)response.StatusCode,
                errCode ?? "n/a",
                errDesc ?? body[..Math.Min(body.Length, 300)]);

            throw new PaymentGatewayException(
                "Payment gateway unavailable. Please try again.",
                (int)response.StatusCode,
                errCode);
        }

        var result = JsonSerializer.Deserialize<RazorpayOrderApiResponse>(body, JsonOpts)
            ?? throw new PaymentGatewayException("Unexpected response from payment gateway.");

        if (string.IsNullOrEmpty(result.Id))
            throw new PaymentGatewayException("Payment gateway did not return an order ID.");

        return result.Id;
    }

    /// <summary>
    /// Safely extracts the Razorpay error envelope fields from a JSON error body.
    /// Returns (null, null) if parsing fails or the fields are absent.
    /// </summary>
    private static (string? Code, string? Description) ExtractRazorpayError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                var code = err.TryGetProperty("code", out var c) ? c.GetString() : null;
                var desc = err.TryGetProperty("description", out var d) ? d.GetString() : null;
                return (code, desc);
            }
        }
        catch { /* parsing failure — body may not be JSON */ }
        return (null, null);
    }

    private async Task<RazorpayPaymentApiResponse?> FetchRazorpayPaymentAsync(
        string razorpayPaymentId,
        CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"v1/payments/{razorpayPaymentId}");
        req.Headers.Authorization = BuildBasicAuth(KeyId, KeySecret);

        var response = await _http.SendAsync(req, ct);
        if (!response.IsSuccessStatusCode) return null;

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<RazorpayPaymentApiResponse>(body, JsonOpts);
    }

    // ── Cryptographic helpers ──────────────────────────────────────────────────

    /// <summary>Computes HMAC-SHA256 and returns lowercase hex.</summary>
    public static string ComputeHmacSha256(string data, string secret)
    {
        var keyBytes  = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var hash      = HMACSHA256.HashData(keyBytes, dataBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Validates Razorpay webhook signature.</summary>
    public static bool ValidateWebhookSignature(string rawBody, string signature, string webhookSecret)
    {
        var expected = ComputeHmacSha256(rawBody, webhookSecret);
        return CryptographicEquals(expected, signature);
    }

    /// <summary>Constant-time string comparison to prevent timing attacks.</summary>
    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++)
            diff |= a[i] ^ b[i];
        return diff == 0;
    }

    // ── Misc helpers ───────────────────────────────────────────────────────────

    private static AuthenticationHeaderValue BuildBasicAuth(string keyId, string keySecret)
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));
        return new AuthenticationHeaderValue("Basic", credentials);
    }

    private static string GenerateOrderNumber()
    {
        var datePart   = DateTime.UtcNow.ToString("yyyyMMdd");
        var uniquePart = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"GM-{datePart}-{uniquePart}";
    }

    private static CreateRazorpayOrderResponse BuildResponse(
        Order order,
        Payment payment,
        ApplicationUser? user)
    {
        long amountPaise = (long)Math.Round(order.TotalAmount * 100m, MidpointRounding.AwayFromZero);
        return new CreateRazorpayOrderResponse(
            InternalOrderId:     order.Id,
            InternalOrderNumber: order.OrderNumber,
            RazorpayOrderId:     payment.GatewayOrderId ?? string.Empty,
            AmountPaise:         amountPaise,
            Currency:            "INR",
            KeyId:               string.Empty, // filled by the controller
            CustomerName:        user?.DisplayName ?? user?.UserName ?? string.Empty,
            CustomerEmail:       user?.Email ?? string.Empty,
            CustomerPhone:       user?.PhoneNumber ?? string.Empty
        );
    }

    private static VerifyRazorpayPaymentResponse Fail(string message) =>
        new(false, message, null, null, null, null);

    private static VerifyRazorpayPaymentResponse Success(Order order) =>
        new(true, "Payment verified successfully.",
            order.Id, order.OrderNumber, order.Status, order.PaymentStatus);
}
