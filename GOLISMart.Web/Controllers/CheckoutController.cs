using GOLISMart.Web.Models;
using GOLISMart.Web.Services;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace GOLISMart.Web.Controllers;

[Authorize]
public class CheckoutController : Controller
{
    private readonly ICheckoutApiClient _checkoutApi;
    private readonly IAuthApiClient _authApi;
    private readonly ICartApiClient _cartApi;
    private readonly IOrderApiClient _orderApi;
    private readonly IPaymentsApiClient _paymentsApi;
    private readonly IConfiguration _config;
    private readonly ILogger<CheckoutController> _logger;

    private const string SessionKeyName   = "GOLISMart_CartSession";
    private const string TokenCookieName  = "GOLISMart_AuthToken";

    public CheckoutController(
        ICheckoutApiClient checkoutApi,
        IAuthApiClient authApi,
        ICartApiClient cartApi,
        IOrderApiClient orderApi,
        IPaymentsApiClient paymentsApi,
        IConfiguration config,
        ILogger<CheckoutController> logger)
    {
        _checkoutApi = checkoutApi;
        _authApi     = authApi;
        _cartApi     = cartApi;
        _orderApi    = orderApi;
        _paymentsApi = paymentsApi;
        _config      = config;
        _logger      = logger;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string? GetSessionKey() => HttpContext.Session.GetString(SessionKeyName);
    private string? GetToken()      => HttpContext.Request.Cookies[TokenCookieName];

    private bool IsAjaxRequest() =>
        HttpContext != null && (
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ||
            Request.Headers.Accept.Any(a => a?.Contains("application/json") == true) ||
            Request.Headers["X-SPA-Request"] == "true"
        );

    // ── Step 1: Select address ────────────────────────────────────────────────

    [HttpGet("checkout")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var token      = GetToken();
        var sessionKey = GetSessionKey();

        if (token is null)
            return RedirectToAction("Login", "Account", new { returnUrl = "/checkout" });

        // Verify cart is not empty before showing address selection
        if (sessionKey is null)
        {
            TempData["CartError"] = "Your cart is empty. Please add items before checking out.";
            return RedirectToAction("Index", "Cart");
        }

        try
        {
            var cart = await _cartApi.GetCartAsync(sessionKey, ct);
            if (cart is null || cart.Items.Count == 0)
            {
                TempData["CartError"] = "Your cart is empty. Please add items before checking out.";
                return RedirectToAction("Index", "Cart");
            }

            var hasAvailableItems = cart.Items.Any(i => i.IsAvailable);
            if (!hasAvailableItems)
            {
                TempData["CartError"] = "All items in your cart are unavailable. Please update your cart.";
                return RedirectToAction("Index", "Cart");
            }

            var addresses = await _authApi.GetAddressesAsync(token, ct);

            // Pre-select default address if one exists
            var defaultAddress = addresses.FirstOrDefault(a => a.IsDefault) ?? addresses.FirstOrDefault();

            return View(new CheckoutAddressViewModel
            {
                Addresses          = addresses,
                Cart               = cart,
                ApiAvailable       = true,
                PreselectedAddressId = defaultAddress?.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load checkout address page");
            return View(new CheckoutAddressViewModel { ApiAvailable = false });
        }
    }

    // ── Step 2: Validate and show review ────────────────────────────────────

    [HttpGet("checkout/review")]
    public async Task<IActionResult> Review([FromQuery] int? addressId = null, CancellationToken ct = default)
    {
        var token      = GetToken();
        var sessionKey = GetSessionKey();

        if (token is null)
            return RedirectToAction("Login", "Account", new { returnUrl = "/checkout" });

        if (sessionKey is null)
        {
            TempData["CartError"] = "Your cart is empty. Please add items before checking out.";
            return RedirectToAction("Index", "Cart");
        }

        try
        {
            // If addressId not provided in query, look up default address or first address
            var targetAddressId = addressId;
            if (targetAddressId is null or <= 0)
            {
                var storedAddressId = HttpContext.Session.GetInt32("Checkout_SelectedAddressId");
                if (storedAddressId.HasValue && storedAddressId.Value > 0)
                {
                    targetAddressId = storedAddressId.Value;
                }
                else
                {
                    var addresses = await _authApi.GetAddressesAsync(token, ct);
                    var defaultAddr = addresses.FirstOrDefault(a => a.IsDefault) ?? addresses.FirstOrDefault();
                    if (defaultAddr is null)
                    {
                        TempData["CheckoutError"] = "Please select or add a delivery address to continue.";
                        return RedirectToAction(nameof(Index));
                    }
                    targetAddressId = defaultAddr.Id;
                }
            }

            HttpContext.Session.SetInt32("Checkout_SelectedAddressId", targetAddressId.Value);

            var result = await _checkoutApi.ValidateAsync(token, sessionKey, targetAddressId.Value, ct);

            if (result is null)
            {
                _logger.LogWarning("Checkout review GET: API returned null for session {SessionKey}, address {AddressId}",
                    sessionKey, targetAddressId);

                return View("Review", new CheckoutReviewViewModel
                {
                    ApiAvailable = false,
                    ErrorMessage = "The checkout service is temporarily unavailable. Please try again."
                });
            }

            if (!result.IsValid)
            {
                _logger.LogInformation("Checkout review GET: validation failed for address {AddressId}. Issues: {Issues}",
                    targetAddressId, string.Join("; ", result.Issues));

                return View("Review", new CheckoutReviewViewModel
                {
                    Summary          = null,
                    ValidationIssues = result.Issues,
                    ApiAvailable     = true,
                    ErrorMessage     = string.Join(" ", result.Issues)
                });
            }

            ViewBag.ApiBaseUrl = _config["ApiSettings:BaseUrl"]?.TrimEnd('/') ?? string.Empty;
            return View("Review", new CheckoutReviewViewModel
            {
                Summary      = result.Summary,
                ApiAvailable = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkout review GET threw an unexpected exception for address {AddressId}", addressId);
            ViewBag.ApiBaseUrl = _config["ApiSettings:BaseUrl"]?.TrimEnd('/') ?? string.Empty;
            return View("Review", new CheckoutReviewViewModel
            {
                ApiAvailable = false,
                ErrorMessage = "An unexpected error occurred. Please try again."
            });
        }
    }

    [HttpPost("checkout/review")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int addressId, CancellationToken ct = default)
    {
        var token      = GetToken();
        var sessionKey = GetSessionKey();

        if (token is null)
        {
            if (IsAjaxRequest())
                return Unauthorized(new { success = false, redirectUrl = "/account/login?returnUrl=/checkout" });
            return RedirectToAction("Login", "Account", new { returnUrl = "/checkout" });
        }

        if (sessionKey is null || addressId <= 0)
        {
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = "Invalid checkout request. Please select a delivery address." });

            TempData["CartError"] = "Invalid checkout request. Please try again.";
            return RedirectToAction(nameof(Index));
        }

        HttpContext.Session.SetInt32("Checkout_SelectedAddressId", addressId);

        try
        {
            var result = await _checkoutApi.ValidateAsync(token, sessionKey, addressId, ct);

            if (result is null)
            {
                _logger.LogWarning(
                    "Checkout review: API returned null for session {SessionKey}, address {AddressId}.",
                    sessionKey, addressId);

                if (IsAjaxRequest())
                    return StatusCode(503, new { success = false, message = "The checkout service is temporarily unavailable. Please try again." });

                return View("Review", new CheckoutReviewViewModel
                {
                    ApiAvailable = false,
                    ErrorMessage = "The checkout service is temporarily unavailable. Please try again."
                });
            }

            if (!result.IsValid)
            {
                _logger.LogInformation(
                    "Checkout review: validation failed for address {AddressId}. Issues: {Issues}",
                    addressId, string.Join("; ", result.Issues));

                if (IsAjaxRequest())
                {
                    return Ok(new
                    {
                        success = false,
                        issues  = result.Issues,
                        message = string.Join(" ", result.Issues)
                    });
                }

                var addresses = await _authApi.GetAddressesAsync(token, ct);
                var cart      = await _cartApi.GetCartAsync(sessionKey, ct);

                return View("Index", new CheckoutAddressViewModel
                {
                    Addresses            = addresses,
                    Cart                 = cart,
                    ApiAvailable         = true,
                    PreselectedAddressId = addressId,
                    ErrorMessage         = string.Join(" ", result.Issues)
                });
            }

            if (IsAjaxRequest())
            {
                return Ok(new
                {
                    success     = true,
                    redirectUrl = $"/checkout/review?addressId={addressId}",
                    summary     = result.Summary
                });
            }

            ViewBag.ApiBaseUrl = _config["ApiSettings:BaseUrl"]?.TrimEnd('/') ?? string.Empty;
            return View("Review", new CheckoutReviewViewModel
            {
                Summary      = result.Summary,
                ApiAvailable = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkout review threw an unexpected exception for address {AddressId}", addressId);

            if (IsAjaxRequest())
                return StatusCode(500, new { success = false, message = "An unexpected error occurred. Please try again." });

            ViewBag.ApiBaseUrl = _config["ApiSettings:BaseUrl"]?.TrimEnd('/') ?? string.Empty;
            return View("Review", new CheckoutReviewViewModel
            {
                ApiAvailable = false,
                ErrorMessage = "An unexpected error occurred. Please try again."
            });
        }
    }

    // ── Step 3: Place order ───────────────────────────────────────────────────

    [HttpPost("checkout/confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int addressId, string? paymentMethod, CancellationToken ct = default)
    {
        var token      = GetToken();
        var sessionKey = GetSessionKey();

        if (token is null)
            return RedirectToAction("Login", "Account", new { returnUrl = "/checkout" });

        if (sessionKey is null || addressId <= 0)
        {
            TempData["CartError"] = "Invalid checkout request. Please try again.";
            return RedirectToAction("Index", "Cart");
        }

        // Validate address selection
        paymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "COD" : paymentMethod.Trim();

        // Generate idempotency key from session (single per form render)
        var idempotencyKey = HttpContext.Session.GetString("Checkout_IdempotencyKey");
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            idempotencyKey = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString("Checkout_IdempotencyKey", idempotencyKey);
        }

        try
        {
            var request = new PlaceOrderRequest(sessionKey, addressId, paymentMethod, null, idempotencyKey);
            var result  = await _orderApi.PlaceOrderAsync(token, request, ct);

            if (result is null)
            {
                TempData["CheckoutError"] = "The order service is temporarily unavailable. Please try again.";
                return RedirectToAction(nameof(Review), new { addressId });
            }

            // Clear idempotency key so the next order gets a fresh one
            HttpContext.Session.Remove("Checkout_IdempotencyKey");

            // Redirect to confirmation page
            TempData["OrderNumber"] = result.OrderNumber;
            return RedirectToAction("Confirmation", "Order", new { orderId = result.OrderId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation(ex, "Order placement validation failed for address {AddressId}", addressId);
            TempData["CheckoutError"] = ex.Message;
            return RedirectToAction(nameof(Review), new { addressId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkout confirm failed");
            TempData["CheckoutError"] = "An unexpected error occurred. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    // ── Razorpay proxy: create gateway order ──────────────────────────────────
    // The browser calls this MVC endpoint, which reads the session key from
    // server-side Session and the JWT from the HttpOnly cookie, then forwards
    // both to the Web API.  Neither secret ever touches the browser.

    [HttpPost("checkout/razorpay/create-order")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RazorpayCreateOrder(
        [FromBody] RazorpayCreateOrderBody body,
        CancellationToken ct = default)
    {
        var token      = GetToken();
        var sessionKey = GetSessionKey();

        if (token is null)
        {
            _logger.LogWarning("RazorpayCreateOrder: unauthenticated request");
            return Unauthorized(new { success = false, message = "Your session has expired. Please log in again.", redirectUrl = "/account/login?returnUrl=/checkout" });
        }

        if (string.IsNullOrEmpty(sessionKey))
        {
            _logger.LogWarning("RazorpayCreateOrder: no cart session key in server-side Session");
            return BadRequest(new { success = false, message = "Your cart session has expired. Please add items to your cart again.", redirectUrl = "/cart" });
        }

        var addressId = HttpContext.Session.GetInt32("Checkout_SelectedAddressId") ?? 0;
        if (addressId <= 0)
        {
            _logger.LogWarning("RazorpayCreateOrder: no address in session, user must re-select");
            return BadRequest(new { success = false, message = "No delivery address selected. Please go back and select an address.", redirectUrl = "/checkout" });
        }

        // Use idempotency key from body if provided, or generate one per checkout session
        var idempotencyKey = body?.IdempotencyKey;
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            idempotencyKey = HttpContext.Session.GetString("Checkout_RzpIdempotencyKey");
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                idempotencyKey = Guid.NewGuid().ToString("N");
                HttpContext.Session.SetString("Checkout_RzpIdempotencyKey", idempotencyKey);
            }
        }

        _logger.LogDebug(
            "RazorpayCreateOrder: forwarding to API for address {AddressId}, idempotencyKey {Key}",
            addressId, idempotencyKey);

        try
        {
            var request = new CreateRazorpayOrderRequest(
                SessionKey:     sessionKey,
                AddressId:      addressId,
                Notes:          null,
                IdempotencyKey: idempotencyKey);

            var result = await _paymentsApi.CreateRazorpayOrderAsync(token, request, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { success = false, message = "Your session has expired. Please log in again.", redirectUrl = "/account/login?returnUrl=/checkout" });
        }
        catch (InvalidOperationException ex)
        {
            // Business rule violation (empty cart, address not found, out of stock, etc.)
            _logger.LogInformation(ex, "[{TraceId}] RazorpayCreateOrder: business rule error", HttpContext.TraceIdentifier);
            return UnprocessableEntity(new { success = false, message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            // Gateway unavailable or API 500/503 — surface as 503 to the browser
            _logger.LogError(ex, "[{TraceId}] RazorpayCreateOrder: payment service error", HttpContext.TraceIdentifier);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{TraceId}] RazorpayCreateOrder: unexpected error", HttpContext.TraceIdentifier);
            return StatusCode(500, new { success = false, message = "An unexpected error occurred. Please try again." });
        }
    }

    // ── Razorpay proxy: verify payment signature ──────────────────────────────

    [HttpPost("checkout/razorpay/verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RazorpayVerify(
        [FromBody] RazorpayVerifyBody body,
        CancellationToken ct = default)
    {
        var token = GetToken();

        if (token is null)
        {
            _logger.LogWarning("RazorpayVerify: unauthenticated request");
            return Unauthorized(new { success = false, message = "Your session has expired. Please log in again." });
        }

        if (body is null ||
            string.IsNullOrWhiteSpace(body.RazorpayOrderId)  ||
            string.IsNullOrWhiteSpace(body.RazorpayPaymentId)||
            string.IsNullOrWhiteSpace(body.RazorpaySignature))
        {
            return BadRequest(new { success = false, message = "Invalid payment details." });
        }

        _logger.LogDebug(
            "RazorpayVerify: verifying payment {PaymentId} for gateway order {OrderId}",
            body.RazorpayPaymentId, body.RazorpayOrderId);

        try
        {
            var request = new VerifyRazorpayPaymentRequest(
                RazorpayOrderId:   body.RazorpayOrderId,
                RazorpayPaymentId: body.RazorpayPaymentId,
                RazorpaySignature: body.RazorpaySignature);

            var result = await _paymentsApi.VerifyRazorpayPaymentAsync(token, request, ct);

            if (result is null)
            {
                _logger.LogError("RazorpayVerify: API returned null");
                return StatusCode(503, new { success = false, message = "Payment verification service is temporarily unavailable." });
            }

            if (result.Success)
            {
                var sessionKey = GetSessionKey();
                if (!string.IsNullOrEmpty(sessionKey))
                {
                    try
                    {
                        await _cartApi.ClearCartAsync(sessionKey, ct);
                    }
                    catch (Exception cartEx)
                    {
                        _logger.LogWarning(cartEx, "Failed to clear cart after successful payment verification");
                    }
                }

                // Clear idempotency key and selected address so subsequent checkout starts clean
                HttpContext.Session.Remove("Checkout_RzpIdempotencyKey");
                HttpContext.Session.Remove("Checkout_SelectedAddressId");
                _logger.LogInformation(
                    "RazorpayVerify: payment verified for order {OrderNumber}", result.OrderNumber);
            }
            else
            {
                _logger.LogWarning(
                    "RazorpayVerify: verification failed — {Message}", result.Message);
            }

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { success = false, message = "Your session has expired. Please log in again." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RazorpayVerify: unexpected error");
            return StatusCode(500, new { success = false, message = "An unexpected error occurred during payment verification." });
        }
    }
}

// ── Request body models for the Razorpay MVC proxies ─────────────────────────
// Kept in this file to avoid scattering small records across the project.

/// <summary>Body sent by the browser to the create-order proxy.</summary>
public record RazorpayCreateOrderBody(string? IdempotencyKey);

/// <summary>Body sent by the browser to the verify proxy.</summary>
public record RazorpayVerifyBody(
    string RazorpayOrderId,
    string RazorpayPaymentId,
    string RazorpaySignature);
