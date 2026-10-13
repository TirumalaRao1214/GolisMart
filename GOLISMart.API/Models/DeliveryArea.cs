namespace GOLISMart.API.Models;

public class DeliveryArea
{
    public int Id { get; set; }
    public string PinCode { get; set; } = string.Empty;
    public string AreaName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? EstimatedDeliveryWindow { get; set; }
    public decimal? MinOrderValue { get; set; }
    public decimal? DeliveryFee { get; set; }
    public decimal? FreeDeliveryAbove { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
