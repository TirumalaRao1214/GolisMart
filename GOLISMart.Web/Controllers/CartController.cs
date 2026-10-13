using GOLISMart.Web.Models;
using GOLISMart.Web.Services;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.Controllers;

public class CartController : Controller
{
    private readonly ICartApiClient _cartApi;
    private readonly ILogger<CartController> _logger;
    private readonly decimal _deliveryFee;
    private readonly decimal _freeDeliveryAbove;
    private const string SessionKeyName = "GOLISMart_CartSession";

    public CartController(ICartApiClient cartApi, IConfiguration config, ILogger<CartController> logger)
    {
        _cartApi           = cartApi;
        _logger            = logger;
        _deliveryFee       = config.GetValue<decimal>("CartSettings:DeliveryFee", 40m);
        _freeDeliveryAbove = config.GetValue<decimal>("CartSettings:FreeDeliveryAbove", 500m);
    }

    private string GetOrCreateSessionKey()
    {
        var key = HttpContext.Session.GetString(SessionKeyName);
        if (string.IsNullOrEmpty(key))
        {
            key = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString(SessionKeyName, key);
        }
        return key;
    }

    private bool IsAjaxRequest() =>
        HttpContext != null && (
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase) ||
            Request.Headers.Accept.Any(a => a?.Contains("application/json") == true) ||
            Request.Headers["X-SPA-Request"] == "true"
        );

    private object CreateCartResponse(CartDto cart, int? targetProductId = null, string? message = null)
    {
        var isFreeDelivery = cart.SubTotal >= _freeDeliveryAbove;
        var deliveryFee = isFreeDelivery ? 0m : _deliveryFee;
        var finalTotal = cart.SubTotal + deliveryFee;
        var thresholdRemaining = Math.Max(0m, _freeDeliveryAbove - cart.SubTotal);

        var targetItem = targetProductId.HasValue
            ? cart.Items.FirstOrDefault(i => i.ProductId == targetProductId.Value)
            : null;

        return new
        {
            success             = true,
            message             = message ?? "Cart updated.",
            cartId              = cart.CartId,
            totalItems          = cart.TotalItems,
            itemCount           = cart.Items.Count,
            subTotal            = cart.SubTotal,
            subTotalFormatted   = cart.SubTotal.ToString("N2"),
            deliveryFee         = deliveryFee,
            deliveryFeeFormatted = isFreeDelivery ? "Free" : $"₹{deliveryFee:N2}",
            isFreeDelivery      = isFreeDelivery,
            freeDeliveryAbove   = _freeDeliveryAbove,
            freeDeliveryRemaining = thresholdRemaining,
            freeDeliveryRemainingFormatted = thresholdRemaining.ToString("N2"),
            finalTotal          = finalTotal,
            finalTotalFormatted = finalTotal.ToString("N2"),
            itemLineTotal       = targetItem?.LineTotal,
            itemLineTotalFormatted = targetItem != null ? targetItem.LineTotal.ToString("N2") : null,
            itemQuantity        = targetItem?.Quantity,
            hasAvailableItems   = cart.Items.Any(i => i.IsAvailable)
        };
    }

    [HttpGet("cart")]
    public async Task<IActionResult> Index(CancellationToken ct = default)
    {
        var sessionKey = GetOrCreateSessionKey();
        try
        {
            var cart = await _cartApi.GetCartAsync(sessionKey, ct);
            var vm = new CartViewModel
            {
                Cart               = cart,
                DeliveryFee        = _deliveryFee,
                FreeDeliveryAbove  = _freeDeliveryAbove,
                ApiAvailable       = cart is not null
            };
            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load cart");
            return View(new CartViewModel { ApiAvailable = false });
        }
    }

    [HttpGet("cart/summary")]
    public async Task<IActionResult> Summary(CancellationToken ct = default)
    {
        var sessionKey = GetOrCreateSessionKey();
        try
        {
            var cart = await _cartApi.GetCartAsync(sessionKey, ct);
            if (cart is null)
                return Ok(new { success = true, totalItems = 0, subTotal = 0m, finalTotal = 0m });

            return Ok(CreateCartResponse(cart));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cart summary");
            return StatusCode(500, new { success = false, message = "Could not fetch cart summary." });
        }
    }

    [HttpPost("cart/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, decimal quantity = 1, string? returnUrl = null, CancellationToken ct = default)
    {
        if (productId <= 0 || quantity <= 0 || quantity > 20)
        {
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = "Invalid product or quantity." });

            TempData["CartError"] = "Invalid product or quantity.";
            return RedirectToLocal(returnUrl);
        }

        var sessionKey = GetOrCreateSessionKey();
        var cart = await _cartApi.AddItemAsync(sessionKey, productId, quantity, ct);

        if (cart is null)
        {
            if (IsAjaxRequest())
                return StatusCode(500, new { success = false, message = "Could not add the product to your cart. Please try again." });

            TempData["CartError"] = "Could not add the product to your cart. Please try again.";
        }
        else
        {
            if (IsAjaxRequest())
                return Ok(CreateCartResponse(cart, productId, "Added to cart!"));

            TempData["CartSuccess"] = "Added to cart!";
        }

        return RedirectToLocal(returnUrl);
    }

    [HttpPost("cart/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int productId, decimal quantity, CancellationToken ct = default)
    {
        if (quantity <= 0 || quantity > 20)
        {
            if (IsAjaxRequest())
                return BadRequest(new { success = false, message = "Quantity must be between 1 and 20." });

            TempData["CartError"] = "Quantity must be between 1 and 20.";
            return RedirectToAction(nameof(Index));
        }

        var sessionKey = GetOrCreateSessionKey();
        var cart = await _cartApi.UpdateItemAsync(sessionKey, productId, quantity, ct);

        if (cart is null)
        {
            if (IsAjaxRequest())
                return StatusCode(500, new { success = false, message = "Could not update item quantity. Please try again." });

            TempData["CartError"] = "Could not update item quantity. Please try again.";
        }
        else
        {
            if (IsAjaxRequest())
                return Ok(CreateCartResponse(cart, productId, "Quantity updated."));
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cart/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int productId, CancellationToken ct = default)
    {
        var sessionKey = GetOrCreateSessionKey();
        var cart = await _cartApi.RemoveItemAsync(sessionKey, productId, ct);

        if (cart is null)
        {
            if (IsAjaxRequest())
                return StatusCode(500, new { success = false, message = "Could not remove the item. Please try again." });

            TempData["CartError"] = "Could not remove the item. Please try again.";
        }
        else
        {
            if (IsAjaxRequest())
                return Ok(CreateCartResponse(cart, productId, "Item removed from cart."));
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cart/clear")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear(CancellationToken ct = default)
    {
        var sessionKey = GetOrCreateSessionKey();
        await _cartApi.ClearCartAsync(sessionKey, ct);

        if (IsAjaxRequest())
        {
            return Ok(new
            {
                success           = true,
                message           = "Cart cleared.",
                totalItems        = 0,
                itemCount         = 0,
                subTotal          = 0m,
                subTotalFormatted = "0.00",
                deliveryFee       = 0m,
                deliveryFeeFormatted = "₹0.00",
                finalTotal        = 0m,
                finalTotalFormatted = "0.00",
                hasAvailableItems = false
            });
        }

        return RedirectToAction(nameof(Index));
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }
}
