using FreightTrack.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace FreightTrack.Web.Data;

/// <summary>
/// The EF Core "unit of work": each DbSet is a table, and SaveChanges() writes all pending changes
/// in a single transaction. Similar to a JPA EntityManager / Hibernate Session.
/// The constructor syntax (options) after the class name is a C# 12 "primary constructor".
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentStatusUpdate> ShipmentStatusUpdates => Set<ShipmentStatusUpdate>();

    // The "Fluent API": configures column sizes, keys and indexes in code instead of annotations,
    // keeping the entity classes clean.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.Property(c => c.CompanyName).HasMaxLength(150).IsRequired();
            e.Property(c => c.ContactName).HasMaxLength(100).IsRequired();
            e.Property(c => c.Email).HasMaxLength(200).IsRequired();
            e.Property(c => c.Phone).HasMaxLength(30);
            e.Property(c => c.City).HasMaxLength(100).IsRequired();
            e.Property(c => c.Country).HasMaxLength(100).IsRequired();

            // The customer list is always sorted by name, so this index lets SQL Server read rows
            // in order instead of sorting. (A "contains" search, LIKE '%abc%', can't seek on it, though.)
            e.HasIndex(c => c.CompanyName);
        });

        modelBuilder.Entity<Shipment>(e =>
        {
            e.Property(s => s.TrackingNumber).HasMaxLength(20).IsRequired();

            // UNIQUE index: the database itself guarantees no two shipments share a tracking number,
            // even if two users create shipments at the same moment. It also makes tracking lookups
            // (the most common query) an index seek instead of a table scan.
            e.HasIndex(s => s.TrackingNumber).IsUnique();

            e.Property(s => s.Origin).HasMaxLength(100).IsRequired();
            e.Property(s => s.Destination).HasMaxLength(100).IsRequired();

            // Store enums as readable text ('InTransit') rather than numbers, so the hand-written
            // SQL scripts and reports are easy to read.
            e.Property(s => s.Mode).HasConversion<string>().HasMaxLength(10);
            e.Property(s => s.CurrentStatus).HasConversion<string>().HasMaxLength(20);

            e.Property(s => s.WeightKg).HasPrecision(10, 2);
            e.Property(s => s.DeclaredValue).HasPrecision(18, 2);

            // "date" columns: we only care about the day, not the time.
            e.Property(s => s.BookedDate).HasColumnType("date");
            e.Property(s => s.ExpectedDeliveryDate).HasColumnType("date");
            e.Property(s => s.DeliveredDate).HasColumnType("date");

            // Composite index for the two hottest filters: "by status" (dashboard counts, list filter)
            // and "late" (status <> Delivered AND ExpectedDeliveryDate < today).
            // CurrentStatus is the leading column, so it also serves status-only queries.
            e.HasIndex(s => new { s.CurrentStatus, s.ExpectedDeliveryDate });

            // EF automatically indexes the CustomerId foreign key (IX_Shipments_CustomerId), which
            // speeds up "all shipments for this customer" and the JOIN to Customers.
            // Restrict = you can't delete a customer who still has shipments (protects history).
            e.HasOne(s => s.Customer)
             .WithMany(c => c.Shipments)
             .HasForeignKey(s => s.CustomerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ShipmentStatusUpdate>(e =>
        {
            e.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(u => u.Location).HasMaxLength(100).IsRequired();
            e.Property(u => u.Notes).HasMaxLength(500);
            e.Property(u => u.UpdatedBy).HasMaxLength(100).IsRequired();

            e.HasOne(u => u.Shipment)
             .WithMany(s => s.StatusUpdates)
             .HasForeignKey(u => u.ShipmentId)
             .OnDelete(DeleteBehavior.Cascade);

            // The timeline query is "updates for shipment X ordered by time". This index covers the
            // foreign key AND the sort, so EF doesn't need a separate ShipmentId-only index.
            e.HasIndex(u => new { u.ShipmentId, u.UpdatedAt });
        });
    }
}
