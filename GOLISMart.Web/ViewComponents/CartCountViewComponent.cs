using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GOLISMart.Web.ViewComponents;

/// <summary>
/// Renders the cart badge count shown in the header.
/// Reads the session key from IHttpContextAccessor and calls the cart API.
/// Returns count 0 silently if session or API is unavailable.
/// </summary>
public class CartCountViewComponent : ViewComponent
{
    private readonly ICartApiClient _cartApi;
    private const string SessionKeyName = "GOLISMart_CartSession";

    public CartCountViewComponent(ICartApiClient cartApi)
    {
        _cartApi = cartApi;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var count = 0;
        try
        {
            var sessionKey = HttpContext.Session.GetString(SessionKeyName);
            if (!string.IsNullOrEmpty(sessionKey))
            {
                var cart = await _cartApi.GetCartAsync(sessionKey);
                count = cart?.TotalItems ?? 0;
            }
        }
        catch
        {
            // Silently fall back to 0 — the header should never break
        }
        return View(count);
    }
}
