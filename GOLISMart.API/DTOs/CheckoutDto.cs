namespace GOLISMart.API.DTOs;

// ── Checkout validation request ────────────────────────────────────────────

/// <summary>
/// Sent from the MVC layer to validate a pending checkout.
/// The addressId must belong to the authenticated customer (verified server-side).
/// </summary>
public record CheckoutValidationRequest(
    string SessionKey,
    int AddressId
);

// ── Checkout validation response ───────────────────────────────────────────

public record CheckoutValidationResponse(
    bool IsValid,
    IReadOnlyList<string> Issues,
    CheckoutSummary? Summary
);

public record CheckoutSummary(
    CheckoutAddressDto Address,
    IReadOnlyList<CheckoutLineItem> Items,
    decimal SubTotal,
    decimal DeliveryFee,
    decimal FinalTotal,
    bool IsFreeDelivery,
    decimal? FreeDeliveryAbove,
    decimal? MinOrderValue,
    bool MeetsMinOrder,
    string? EstimatedDeliveryWindow,
    string PinCode
);

public record CheckoutAddressDto(
    int AddressId,
    string RecipientName,
    string RecipientMobile,
    string AddressLabel,
    string HouseNumber,
    string BuildingStreet,
    string Locality,
    string? Landmark,
    string City,
    string State,
    string PinCode,
    string? DeliveryInstructions,
    bool IsDefault
);

public record CheckoutLineItem(
    int ProductId,
    string Name,
    string UnitLabel,
    string? ImagePath,
    decimal Quantity,
    decimal UnitPrice,
    decimal OriginalPrice,
    decimal LineTotal,
    bool IsAvailable,
    bool IsInStock
);
