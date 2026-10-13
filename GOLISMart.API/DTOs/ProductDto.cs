namespace GOLISMart.API.DTOs;

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

public record ProductImageDto(
    int Id,
    string ImagePath,
    string? AltText,
    int SortOrder,
    bool IsPrimary
);
