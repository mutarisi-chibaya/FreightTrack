namespace FreightTrack.Web.Models;

/// <summary>One entry in a shipment's history. Rows are only ever added, never edited (an audit trail).</summary>
public class ShipmentStatusUpdate
{
    public int Id { get; set; }
    public int ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;
    public ShipmentStatus Status { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
