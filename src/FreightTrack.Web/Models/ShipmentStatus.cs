using System.ComponentModel.DataAnnotations;

namespace FreightTrack.Web.Models;

/// <summary>
/// Where a shipment is in its journey. The numeric values matter: the normal flow is in
/// ascending order, so "moving forward" simply means the new value is >= the old one.
/// Exception is deliberately given a large number outside the normal flow.
/// </summary>
public enum ShipmentStatus
{
    [Display(Name = "Booked")] Booked = 0,
    [Display(Name = "Collected")] Collected = 1,
    [Display(Name = "In transit")] InTransit = 2,
    [Display(Name = "At customs")] AtCustoms = 3,
    [Display(Name = "Out for delivery")] OutForDelivery = 4,
    [Display(Name = "Delivered")] Delivered = 5,
    [Display(Name = "Exception")] Exception = 99
}
