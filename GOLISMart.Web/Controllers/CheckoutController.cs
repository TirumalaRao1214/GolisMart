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
    private readonly ILogger<CheckoutController> _logger;

    private const string SessionKeyName   = "GOLISMart_CartSession";
    private const string TokenCookieName  = "GOLISMart_AuthToken";

    public CheckoutController(
        ICheckoutApiClient checkoutApi,
        IAuthApiClient authApi,
        ICartApiClient cartApi,
        IOrderApiClient orderApi,
        ILogger<CheckoutController> logger)
    {
        _checkoutApi = checkoutApi;
        _authApi     = authApi;
        _cartApi     = cartApi;
        _orderApi    = orderApi;
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

            return View("Review", new CheckoutReviewViewModel
            {
                Summary      = result.Summary,
                ApiAvailable = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Checkout review GET threw an unexpected exception for address {AddressId}", addressId);
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
}
