namespace GOLISMart.API.Models;

public class UserAddress
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientMobile { get; set; } = string.Empty;
    public string AddressLabel { get; set; } = "Home";
    public string HouseNumber { get; set; } = string.Empty;
    public string BuildingStreet { get; set; } = string.Empty;
    public string Locality { get; set; } = string.Empty;
    public string? Landmark { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Pincode { get; set; } = string.Empty;
    public string? DeliveryInstructions { get; set; }
    public bool IsDefault { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    // Legacy fields retained for backward compatibility
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
}
