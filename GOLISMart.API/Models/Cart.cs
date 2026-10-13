namespace GOLISMart.API.Models;

public class Cart
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string? SessionKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
