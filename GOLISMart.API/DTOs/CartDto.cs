namespace GOLISMart.API.DTOs;

public record CartDto(
    int CartId,
    string SessionKey,
    IReadOnlyList<CartItemDto> Items,
    decimal SubTotal,
    int TotalItems    // sum of quantities as int (ceiling)
);

public record CartItemDto(
    int ProductId,
    string Name,
    string Slug,
    string UnitLabel,
    string? ImagePath,
    decimal Quantity,
    decimal UnitPrice,         // current price from DB (OfferPrice ?? Price)
    decimal OriginalPrice,     // Price from DB (for strikethrough)
    decimal LineTotal,
    bool IsInStock,
    bool IsAvailable           // IsActive && IsInStock
);

public record AddToCartRequest(int ProductId, decimal Quantity);
public record UpdateCartItemRequest(decimal Quantity);
