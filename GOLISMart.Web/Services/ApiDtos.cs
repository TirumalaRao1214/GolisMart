namespace GOLISMart.Web.Services;

// ---------------------------------------------------------------------------
// These DTOs mirror the shapes returned by the GOLIS Mart API.
// They are intentionally NOT a project reference to GOLISMart.API so that
// the Web project never gets a dependency on the API's data layer.
// ---------------------------------------------------------------------------

public record CategoryDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    string? ImagePath,
    int SortOrder,
    int ProductCount
);

public record ProductSummaryDto(
    int Id,
    string SKU,
    string Name,
    string Slug,
    string UnitLabel,
    decimal Price,
    decimal? OfferPrice,
    bool IsFeatured,
    bool IsInStock,
    bool IsLowStock,
    string? ImagePath,
    string? CategoryName,
    string? CategorySlug
);

public record ProductDetailDto(
    int Id,
    string SKU,
    string Name,
    string Slug,
    string? Description,
    string UnitCode,
    string UnitLabel,
    decimal Price,
    decimal? OfferPrice,
    bool IsFeatured,
    bool IsInStock,
    bool IsLowStock,
    decimal AvailableQuantity,
    string? ImagePath,
    int CategoryId,
    string? CategoryName,
    string? CategorySlug,
    List<ProductImageDto> Images
);

public record CartDto(
    int CartId,
    string SessionKey,
    List<CartItemDto> Items,
    decimal SubTotal,
    int TotalItems
);

public record CartItemDto(
    int ProductId,
    string Name,
    string Slug,
    string UnitLabel,
    string? ImagePath,
    decimal Quantity,
    decimal UnitPrice,
    decimal OriginalPrice,
    decimal LineTotal,
    bool IsInStock,
    bool IsAvailable
);

public record AddToCartRequest(int ProductId, decimal Quantity);
public record UpdateCartItemRequest(decimal Quantity);

public record ProductImageDto(
    int Id,
    string ImagePath,
    string? AltText,
    int SortOrder,
    bool IsPrimary
);

public record PagedResult<T>(
    List<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);

// ── Delivery DTOs (mirrored from API) ─────────────────────────────────────────

public record ServiceabilityResponse(
    string PinCode,
    bool IsServiceable,
    string? AreaName,
    string? EstimatedDeliveryWindow,
    decimal? MinOrderValue,
    decimal? DeliveryFee,
    decimal? FreeDeliveryAbove,
    string? UnavailableReason
);

public record DeliveryAreaDto(
    int Id,
    string PinCode,
    string AreaName,
    bool IsActive,
    string? EstimatedDeliveryWindow,
    decimal? MinOrderValue,
    decimal? DeliveryFee,
    decimal? FreeDeliveryAbove
);

// ── Location / Geocoding DTOs (mirrored from API) ────────────────────────────

public record ReverseGeocodeAddressDto(
    string HouseNumber,
    string Street,
    string Locality,
    string? Landmark,
    string City,
    string State,
    string PinCode,
    string DisplayName
);

public record ReverseGeocodeResponse(
    bool Success,
    ReverseGeocodeAddressDto? Address,
    string? Error
);

// ── Checkout DTOs (mirrored from API) ─────────────────────────────────────────

public record CheckoutValidationRequest(string SessionKey, int AddressId);

public record CheckoutValidationResponse(
    bool IsValid,
    List<string> Issues,
    CheckoutSummaryDto? Summary
);

public record CheckoutSummaryDto(
    CheckoutAddressDto Address,
    List<CheckoutLineItemDto> Items,
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

public record CheckoutLineItemDto(
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
