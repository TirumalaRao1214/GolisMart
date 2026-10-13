using GOLISMart.API.DTOs;

namespace GOLISMart.API.Services.Interfaces;

public interface ICheckoutService
{
    /// <summary>
    /// Validates the cart and address for checkout.
    /// The userId must come from the authenticated identity claim — never from the request body.
    /// </summary>
    Task<CheckoutValidationResponse> ValidateAsync(
        string sessionKey,
        int addressId,
        string userId,
        CancellationToken ct = default);
}
