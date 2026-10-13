namespace GOLISMart.API.Models;

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
}
