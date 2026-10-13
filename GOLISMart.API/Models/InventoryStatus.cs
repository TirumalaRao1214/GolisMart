namespace GOLISMart.API.Models;

public class InventoryStatus
{
    public int ProductId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal ReservedQuantity { get; set; }
    public decimal LowStockThreshold { get; set; }
    public decimal AvailableQuantity => QuantityOnHand - ReservedQuantity;
    public bool IsInStock => AvailableQuantity > 0;
    public bool IsLowStock => AvailableQuantity > 0 && AvailableQuantity <= LowStockThreshold;
}
