using System.ComponentModel.DataAnnotations;
using FreightTrack.Web.Models;
using FreightTrack.Web.Services;

namespace FreightTrack.Web.ViewModels;

/// <summary>
/// What the Create/Edit form posts. We never bind the Customer entity directly to a form:
/// a malicious POST could otherwise set fields the form doesn't show (e.g. CreatedAt or Id).
/// This is called "over-posting" (mass assignment).
/// The [Required]/[EmailAddress] attributes drive both server-side validation and the
/// client-side jQuery validation in the browser.
/// </summary>
public class CustomerFormViewModel
{
    public int? Id { get; set; }

    [Required, StringLength(150), Display(Name = "Company name")]
    public string CompanyName { get; set; } = string.Empty;

    [Required, StringLength(100), Display(Name = "Contact name")]
    public string ContactName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? Phone { get; set; }

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Country { get; set; } = string.Empty;
}

public class CustomerListItem
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int ShipmentCount { get; set; }
}

public class CustomerListViewModel
{
    public string? Search { get; set; }
    public List<CustomerListItem> Customers { get; set; } = new();
}

public class CustomerDetailsViewModel
{
    public required Customer Customer { get; set; }
    public required List<CustomerShipmentSummaryRow> Summary { get; set; }
    public required DateTime Today { get; set; }
}
