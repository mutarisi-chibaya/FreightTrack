using FreightTrack.Web.Data;
using FreightTrack.Web.Models;
using FreightTrack.Web.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FreightTrack.Tests;

/// <summary>
/// A real relational database (SQLite) living only in memory. Unlike EF's "InMemory" provider it
/// enforces foreign keys and unique indexes, so tests behave like SQL Server for our rules.
/// Each test gets a fresh, empty database. IDisposable = cleaned up by the "using" statement.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;
    public AppDbContext Context { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open(); // the in-memory database lives as long as this connection is open
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    /// <summary>A fresh DbContext on the same database, to prove data was really saved.</summary>
    public AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public Customer AddCustomer(string name = "Karoo Fresh Produce", string contact = "Nomsa Dlamini")
    {
        var c = new Customer
        {
            CompanyName = name, ContactName = contact, Email = "test@example.com",
            City = "Cape Town", Country = "South Africa", CreatedAt = new DateTime(2026, 1, 1)
        };
        Context.Customers.Add(c);
        Context.SaveChanges();
        return c;
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

/// <summary>A clock frozen at a chosen moment, so "today" is the same every time the tests run.</summary>
public class FixedTimeProvider(DateTime localNow) : TimeProvider
{
    public DateTime LocalNow { get; set; } = localNow;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(LocalNow, DateTimeKind.Utc));
}

public static class Forms
{
    public static ShipmentFormViewModel Shipment(int customerId) => new()
    {
        CustomerId = customerId,
        Origin = "Durban, South Africa",
        Destination = "Gaborone, Botswana",
        Mode = ShipmentMode.Road,
        WeightKg = 1200m,
        DeclaredValue = 250_000m,
        BookedDate = new DateTime(2026, 11, 1),
        ExpectedDeliveryDate = new DateTime(2026, 11, 6),
    };

    public static StatusUpdateFormViewModel Update(ShipmentStatus status, string? notes = null) => new()
    {
        Status = status,
        Location = "Skilpadshek Border Post",
        Notes = notes,
        UpdatedBy = "Test Clerk",
    };
}
