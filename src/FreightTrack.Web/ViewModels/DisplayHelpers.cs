using FreightTrack.Web.Models;

namespace FreightTrack.Web.ViewModels;

public static class DisplayHelpers
{
    /// <summary>Bootstrap badge colour for each status. A C# "switch expression" (like Java 14+ switch).</summary>
    public static string BadgeClass(ShipmentStatus status) => status switch
    {
        ShipmentStatus.Booked => "text-bg-secondary",
        ShipmentStatus.Collected => "text-bg-info",
        ShipmentStatus.InTransit => "text-bg-primary",
        ShipmentStatus.AtCustoms => "text-bg-warning",
        ShipmentStatus.OutForDelivery => "text-bg-primary",
        ShipmentStatus.Delivered => "text-bg-success",
        ShipmentStatus.Exception => "text-bg-danger",
        _ => "text-bg-light"
    };
}
