using System.ComponentModel.DataAnnotations;
using FreightTrack.Web.Models;
using FreightTrack.Web.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FreightTrack.Web.ViewModels;

/// <summary>
/// The shipment Create/Edit form. Note what's NOT here: TrackingNumber (generated, never edited)
/// and CurrentStatus (only changes through a status update). The form physically can't change them.
/// </summary>
public class ShipmentFormViewModel
{
    public int? Id { get; set; }

    /// <summary>Shown read-only on the Edit page.</summary>
    public string? TrackingNumber { get; set; }

    [Required, Display(Name = "Customer")]
    public int? CustomerId { get; set; }

    [Required, StringLength(100)]
    public string Origin { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Destination { get; set; } = string.Empty;

    [Required]
    public ShipmentMode? Mode { get; set; }

    [Required, Range(0.01, 1_000_000, ErrorMessage = "Weight must be greater than 0."), Display(Name = "Weight (kg)")]
    public decimal? WeightKg { get; set; }

    [Required, Range(0, 1_000_000_000, ErrorMessage = "Declared value can't be negative."), Display(Name = "Declared value (ZAR)")]
    public decimal? DeclaredValue { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Booked date")]
    public DateTime? BookedDate { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Expected delivery")]
    public DateTime? ExpectedDeliveryDate { get; set; }

    // Dropdown options (not posted back).
    public IEnumerable<SelectListItem> Customers { get; set; } = [];
}

public class StatusUpdateFormViewModel
{
    [Required]
    public ShipmentStatus? Status { get; set; }

    [Required, StringLength(100)]
    public string Location { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Notes { get; set; }

    [Required, StringLength(100), Display(Name = "Updated by")]
    public string UpdatedBy { get; set; } = string.Empty;
}

public class ShipmentDetailsViewModel
{
    public required Shipment Shipment { get; set; }
    public required IReadOnlyList<ShipmentStatus> AllowedNextStatuses { get; set; }
    public required DateTime Today { get; set; }
    public StatusUpdateFormViewModel NewUpdate { get; set; } = new();
}

public class ShipmentListViewModel
{
    public required ShipmentFilter Filter { get; set; }
    public required PagedResult<Shipment> Results { get; set; }
    public required IEnumerable<SelectListItem> Customers { get; set; }
    public required DateTime Today { get; set; }

    /// <summary>Query-string values for a paging link that keeps the current filters.</summary>
    public Dictionary<string, string> RouteFor(int page)
    {
        var route = new Dictionary<string, string> { ["page"] = page.ToString() };
        if (Filter.Status is not null) route["status"] = Filter.Status.ToString()!;
        if (Filter.Mode is not null) route["mode"] = Filter.Mode.ToString()!;
        if (Filter.CustomerId is not null) route["customerId"] = Filter.CustomerId.ToString()!;
        if (Filter.LateOnly) route["lateOnly"] = "true";
        return route;
    }
}

public class DashboardViewModel
{
    public required List<(ShipmentStatus Status, int Count)> StatusCounts { get; set; }
    public int TotalShipments { get; set; }
    public int InProgressCount { get; set; }
    public int LateCount { get; set; }
    public required List<Shipment> MostOverdue { get; set; }
    public required DateTime Today { get; set; }
}
