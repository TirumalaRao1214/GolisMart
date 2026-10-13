namespace GOLISMart.API.DTOs;

/// <summary>Response for GET /api/v1/delivery/serviceability?pinCode={pinCode}</summary>
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

/// <summary>Response item for GET /api/v1/delivery/areas</summary>
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
