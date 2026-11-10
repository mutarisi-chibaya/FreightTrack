using FreightTrack.Web.Models;

namespace FreightTrack.Web.Services;

/// <summary>
/// Thrown when input breaks a business rule. The controller catches it and shows the message
/// next to the offending form field (Field = the property name, or "" for a general error).
/// </summary>
public class BusinessRuleException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

/// <summary>One page of results plus what the view needs to draw paging links.</summary>
public class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

/// <summary>Filters for the shipment list. Bound from the query string, e.g. ?status=InTransit&lateOnly=true&page=2</summary>
public class ShipmentFilter
{
    public ShipmentStatus? Status { get; set; }
    public ShipmentMode? Mode { get; set; }
    public int? CustomerId { get; set; }
    public bool LateOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
}

/// <summary>One row returned by the stored procedure usp_GetCustomerShipmentSummary.
/// Status is NULL on the grand-total row produced by GROUP BY ROLLUP.</summary>
public class CustomerShipmentSummaryRow
{
    public string? Status { get; set; }
    public int ShipmentCount { get; set; }
    public decimal TotalWeightKg { get; set; }
}
