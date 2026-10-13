namespace GOLISMart.API.Models;

/// <summary>Represents an assignment of a delivery person to an order.</summary>
public class DeliveryAssignment
{
    public int      Id                  { get; set; }
    public int      OrderId             { get; set; }
    public int      DeliveryPersonnelId { get; set; }
    public string?  AssignedBy          { get; set; }
    public DateTime AssignedAt          { get; set; }
    public DateTime? UnassignedAt       { get; set; }
    public string?  UnassignedBy        { get; set; }
    public string?  UnassignedReason    { get; set; }
    public bool     IsActive            { get; set; }
}
