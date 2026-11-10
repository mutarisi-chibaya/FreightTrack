using FreightTrack.Web.Models;
using FreightTrack.Web.Services;

namespace FreightTrack.Tests;

public class LateShipmentTests
{
    private static readonly DateTime Today = new(2026, 11, 10);

    private static Shipment Make(ShipmentStatus status, DateTime expected) =>
        new() { CurrentStatus = status, ExpectedDeliveryDate = expected };

    [Fact]
    public void Undelivered_and_past_expected_date_is_late()
    {
        var s = Make(ShipmentStatus.InTransit, Today.AddDays(-3));
        Assert.True(s.IsLate(Today));
        Assert.Equal(3, s.DaysOverdue(Today));
    }

    [Fact]
    public void Due_today_is_not_late_yet()
    {
        Assert.False(Make(ShipmentStatus.OutForDelivery, Today).IsLate(Today));
    }

    [Fact]
    public void Delivered_is_never_late_even_if_it_arrived_after_the_expected_date()
    {
        Assert.False(Make(ShipmentStatus.Delivered, Today.AddDays(-10)).IsLate(Today));
    }

    [Fact]
    public void Exception_shipments_past_their_date_count_as_late()
    {
        Assert.True(Make(ShipmentStatus.Exception, Today.AddDays(-1)).IsLate(Today));
    }

    /// <summary>The same rule must also work when EF translates it to SQL (the list filter and dashboard).</summary>
    [Fact]
    public async Task Late_filter_and_dashboard_agree_with_the_rule()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer();
        db.Context.Shipments.AddRange(
            New("FT-2026-000001", ShipmentStatus.InTransit, Today.AddDays(-5)),   // late (most overdue)
            New("FT-2026-000002", ShipmentStatus.AtCustoms, Today.AddDays(-1)),   // late
            New("FT-2026-000003", ShipmentStatus.InTransit, Today),               // due today: not late
            New("FT-2026-000004", ShipmentStatus.Delivered, Today.AddDays(-9)),   // delivered: not late
            New("FT-2026-000005", ShipmentStatus.Booked, Today.AddDays(4)));      // future: not late
        db.Context.SaveChanges();

        var service = new ShipmentService(db.Context, new FixedTimeProvider(Today.AddHours(10)));

        var late = await service.ListAsync(new ShipmentFilter { LateOnly = true });
        var dashboard = await service.GetDashboardAsync();

        Assert.Equal(2, late.TotalCount);
        Assert.Equal(2, dashboard.LateCount);
        Assert.Equal("FT-2026-000001", dashboard.MostOverdue[0].TrackingNumber);
        Assert.Equal(4, dashboard.InProgressCount);

        Shipment New(string tn, ShipmentStatus status, DateTime expected) => new()
        {
            TrackingNumber = tn, CustomerId = customer.Id, Origin = "A", Destination = "B",
            Mode = ShipmentMode.Road, WeightKg = 10, BookedDate = Today.AddDays(-20),
            ExpectedDeliveryDate = expected, CurrentStatus = status
        };
    }
}
