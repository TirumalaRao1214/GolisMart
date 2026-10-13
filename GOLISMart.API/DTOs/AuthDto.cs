using System.ComponentModel.DataAnnotations;

namespace GOLISMart.API.DTOs;

// ── Auth ──────────────────────────────────────────────────────────────────────

public record RegisterRequest
{
    [Required] public string FullName { get; init; } = string.Empty;
    [Required] public string Email { get; init; } = string.Empty;
    [Required] public string Mobile { get; init; } = string.Empty;
    [Required] public string Password { get; init; } = string.Empty;
    [Required] public string ConfirmPassword { get; init; } = string.Empty;
    public bool AcceptTerms { get; init; }
}

public record LoginRequest
{
    [Required] public string Email { get; init; } = string.Empty;
    [Required] public string Password { get; init; } = string.Empty;
}

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

// ── Profile ───────────────────────────────────────────────────────────────────

public record UpdateProfileRequest
{
    [Required] public string DisplayName { get; init; } = string.Empty;
    [Required] public string Mobile { get; init; } = string.Empty;
}

public record ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; init; } = string.Empty;
    [Required] public string NewPassword { get; init; } = string.Empty;
    [Required] public string ConfirmNewPassword { get; init; } = string.Empty;
}

public record CustomerProfileDto(
    string UserId,
    string Email,
    string DisplayName,
    string? PhoneNumber,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// ── Addresses ────────────────────────────────────────────────────────────────

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

public record CreateAddressRequest
{
    [Required] public string RecipientName { get; init; } = string.Empty;
    [Required] public string RecipientMobile { get; init; } = string.Empty;
    [Required] public string AddressLabel { get; init; } = "Home";
    [Required] public string HouseNumber { get; init; } = string.Empty;
    [Required] public string BuildingStreet { get; init; } = string.Empty;
    [Required] public string Locality { get; init; } = string.Empty;
    public string? Landmark { get; init; }
    [Required] public string City { get; init; } = string.Empty;
    [Required] public string State { get; init; } = string.Empty;
    [Required] public string Pincode { get; init; } = string.Empty;
    public string? DeliveryInstructions { get; init; }
    public bool IsDefault { get; init; }
}

public record UpdateAddressRequest
{
    [Required] public string RecipientName { get; init; } = string.Empty;
    [Required] public string RecipientMobile { get; init; } = string.Empty;
    [Required] public string AddressLabel { get; init; } = "Home";
    [Required] public string HouseNumber { get; init; } = string.Empty;
    [Required] public string BuildingStreet { get; init; } = string.Empty;
    [Required] public string Locality { get; init; } = string.Empty;
    public string? Landmark { get; init; }
    [Required] public string City { get; init; } = string.Empty;
    [Required] public string State { get; init; } = string.Empty;
    [Required] public string Pincode { get; init; } = string.Empty;
    public string? DeliveryInstructions { get; init; }
    public bool IsDefault { get; init; }
}
