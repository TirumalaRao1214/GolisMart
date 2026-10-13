namespace GOLISMart.API.Models;

/// <summary>Represents a delivery worker record in the DeliveryPersonnel table.</summary>
public class DeliveryPersonnel
{
    public int      Id              { get; set; }
    public string?  UserId          { get; set; }   // FK to AspNetUsers; null = no app account
    public string   FullName        { get; set; } = string.Empty;
    public string   ContactNumber   { get; set; } = string.Empty;
    public bool     IsActive        { get; set; } = true;
    public string?  Notes           { get; set; }
    public DateTime CreatedAt       { get; set; }
    public DateTime UpdatedAt       { get; set; }
}
