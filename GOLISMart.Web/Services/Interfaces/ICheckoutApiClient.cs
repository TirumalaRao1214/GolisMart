namespace GOLISMart.Web.Services.Interfaces;

public interface ICheckoutApiClient
{
    Task<CheckoutValidationResponse?> ValidateAsync(
        string token,
        string sessionKey,
        int addressId,
        CancellationToken ct = default);
}
