namespace GOLISMart.API.Models;

public class Product
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string UnitCode { get; set; } = string.Empty;
    public string UnitLabel { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OfferPrice { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsActive { get; set; }
    public string? ImagePath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    // Joined fields
    public string? CategoryName { get; set; }
    public string? CategorySlug { get; set; }
}
