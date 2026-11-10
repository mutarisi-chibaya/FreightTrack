namespace FreightTrack.Web.Models;

// An entity: a plain class that EF Core maps to the Customers table.
// C# properties ({ get; set; }) replace Java's getter/setter boilerplate.
public class Customer
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }          // "string?" = nullable, so this column allows NULL
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Navigation property: one customer has many shipments.
    public List<Shipment> Shipments { get; set; } = new();
}
