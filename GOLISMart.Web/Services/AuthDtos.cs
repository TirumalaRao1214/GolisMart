namespace GOLISMart.Web.Services;

// ── Auth DTOs (mirrored from API) ─────────────────────────────────────────────

public record RegisterRequest(
    string FullName,
    string Email,
    string Mobile,
    string Password,
    string ConfirmPassword,
    bool AcceptTerms);

public record LoginRequest(
    string Email,
    string Password);

public record AuthResponse(
    string Token,
    string UserId,
    string Email,
    string DisplayName,
    DateTime ExpiresAt);

public record MeResponse(
    string UserId,
    string Email,
    string DisplayName,
    string? PhoneNumber,
    DateTime CreatedAt);

public record UpdateProfileRequest(
    string DisplayName,
    string Mobile);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword);

public record CustomerProfileDto(
    string UserId,
    string Email,
    string DisplayName,
    string? PhoneNumber,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record AddressDto(
    int Id,
    string RecipientName,
    string RecipientMobile,
    string AddressLabel,
    string HouseNumber,
    string BuildingStreet,
    string Locality,
    string? Landmark,
    string City,
    string State,
    string Pincode,
    string? DeliveryInstructions,
    bool IsDefault,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateAddressRequest(
    string RecipientName,
    string RecipientMobile,
    string AddressLabel,
    string HouseNumber,
    string BuildingStreet,
    string Locality,
    string? Landmark,
    string City,
    string State,
    string Pincode,
    string? DeliveryInstructions,
    bool IsDefault);

public record UpdateAddressRequest(
    string RecipientName,
    string RecipientMobile,
    string AddressLabel,
    string HouseNumber,
    string BuildingStreet,
    string Locality,
    string? Landmark,
    string City,
    string State,
    string Pincode,
    string? DeliveryInstructions,
    bool IsDefault);
