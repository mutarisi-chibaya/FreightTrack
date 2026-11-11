using System.ComponentModel.DataAnnotations;
using FreightTrack.Web.Models;

namespace FreightTrack.Web.Dtos;

// DTOs (Data Transfer Objects) are the shapes the REST API sends and receives.
// Why not just return the EF entities?
//  - Control: the API returns exactly these fields. Adding a column to the database doesn't silently
//    change the public JSON contract that other systems depend on.
//  - Safety: entities have navigation properties (Shipment -> Customer -> Shipments -> ...) that
//    would cause reference loops or leak data we never meant to expose.
//  - Over-posting: a client can only send the fields in the request DTO. It can't sneak in
//    "CurrentStatus" or "TrackingNumber", which the business rules say must never be set directly.
// C# "records" are ideal for DTOs: immutable, one-line declarations, value equality. (Like Java records.)

/// <summary>One row in the shipment list.</summary>
public record ShipmentSummaryDto(
    int Id,
    string TrackingNumber,
    string CustomerName,
    string Origin,
    string Destination,
    ShipmentMode Mode,
    ShipmentStatus CurrentStatus,
    DateTime BookedDate,
    DateTime ExpectedDeliveryDate,
    DateTime? DeliveredDate,
    bool IsLate,
    int DaysOverdue);

/// <summary>A single shipment with its full status history (newest first).</summary>
public record ShipmentDetailDto(
    int Id,
    string TrackingNumber,
    int CustomerId,
    string CustomerName,
    string Origin,
    string Destination,
    ShipmentMode Mode,
    decimal WeightKg,
    decimal DeclaredValue,
    ShipmentStatus CurrentStatus,
    DateTime BookedDate,
    DateTime ExpectedDeliveryDate,
    DateTime? DeliveredDate,
    bool IsLate,
    int DaysOverdue,
    IReadOnlyList<StatusUpdateDto> History);

/// <summary>One entry in a shipment's history.</summary>
public record StatusUpdateDto(
    int Id,
    ShipmentStatus Status,
    string Location,
    string? Notes,
    string UpdatedBy,
    DateTime UpdatedAt);

/// <summary>A page of results plus paging metadata, so clients know whether to ask for more.</summary>
public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

/// <summary>
/// Body of POST /api/shipments/{id}/status. [ApiController] checks these attributes automatically
/// and returns 400 Bad Request with the errors before our action code even runs.
/// </summary>
public class StatusUpdateRequest
{
    [Required]
    public ShipmentStatus? Status { get; set; }

    [Required, StringLength(100)]
    public string Location { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Notes { get; set; }

    [Required, StringLength(100)]
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Entity -> DTO conversions in one place. These are "extension methods": the `this` keyword on the
/// first parameter lets us write shipment.ToSummaryDto(today) as if it were a method on Shipment.
/// Java has no equivalent; you'd write a static helper and call ShipmentMapper.toSummary(shipment).
/// </summary>
public static class ShipmentDtoMappings
{
    public static ShipmentSummaryDto ToSummaryDto(this Shipment s, DateTime today) => new(
        s.Id, s.TrackingNumber, s.Customer.CompanyName, s.Origin, s.Destination, s.Mode,
        s.CurrentStatus, s.BookedDate, s.ExpectedDeliveryDate, s.DeliveredDate,
        s.IsLate(today), s.DaysOverdue(today));

    public static ShipmentDetailDto ToDetailDto(this Shipment s, DateTime today) => new(
        s.Id, s.TrackingNumber, s.CustomerId, s.Customer.CompanyName, s.Origin, s.Destination, s.Mode,
        s.WeightKg, s.DeclaredValue, s.CurrentStatus, s.BookedDate, s.ExpectedDeliveryDate, s.DeliveredDate,
        s.IsLate(today), s.DaysOverdue(today),
        s.StatusUpdates.Select(u => u.ToDto()).ToList());

    public static StatusUpdateDto ToDto(this ShipmentStatusUpdate u) =>
        new(u.Id, u.Status, u.Location, u.Notes, u.UpdatedBy, u.UpdatedAt);
}
