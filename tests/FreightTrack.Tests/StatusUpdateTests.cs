using FreightTrack.Web.Models;
using FreightTrack.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace FreightTrack.Tests;

public class StatusUpdateTests
{
    private readonly FixedTimeProvider _clock = new(new DateTime(2026, 11, 10, 9, 0, 0));

    private async Task<(TestDb db, ShipmentService service, Shipment shipment)> ArrangeAsync()
    {
        var db = new TestDb();
        var customer = db.AddCustomer();
        var service = new ShipmentService(db.Context, _clock);
        var shipment = await service.CreateAsync(Forms.Shipment(customer.Id), "Test");
        return (db, service, shipment);
    }

    // [Theory] + [InlineData] = one test method run with several inputs (like JUnit's @ParameterizedTest).
    [Theory]
    [InlineData(ShipmentStatus.InTransit, ShipmentStatus.Collected)]
    [InlineData(ShipmentStatus.InTransit, ShipmentStatus.Booked)]
    [InlineData(ShipmentStatus.OutForDelivery, ShipmentStatus.AtCustoms)]
    public void Status_cannot_move_backwards(ShipmentStatus current, ShipmentStatus next)
    {
        Assert.False(ShipmentStatusRules.CanMoveTo(current, next));
    }

    [Theory]
    [InlineData(ShipmentStatus.Booked, ShipmentStatus.Collected)]
    [InlineData(ShipmentStatus.Collected, ShipmentStatus.OutForDelivery)] // skipping steps is fine
    [InlineData(ShipmentStatus.InTransit, ShipmentStatus.InTransit)]      // same status, new location
    [InlineData(ShipmentStatus.AtCustoms, ShipmentStatus.Exception)]      // anything → Exception
    public void Status_can_move_forward_or_to_exception(ShipmentStatus current, ShipmentStatus next)
    {
        Assert.True(ShipmentStatusRules.CanMoveTo(current, next));
    }

    [Theory]
    [InlineData(ShipmentStatus.InTransit)]
    [InlineData(ShipmentStatus.Exception)]
    public void Nothing_can_follow_delivered(ShipmentStatus next)
    {
        Assert.False(ShipmentStatusRules.CanMoveTo(ShipmentStatus.Delivered, next));
    }

    [Fact]
    public async Task Moving_backwards_is_rejected_and_nothing_is_saved()
    {
        var (db, service, shipment) = await ArrangeAsync();
        using (db)
        {
            await service.AddStatusUpdateAsync(shipment.Id, Forms.Update(ShipmentStatus.InTransit));

            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.AddStatusUpdateAsync(shipment.Id, Forms.Update(ShipmentStatus.Collected)));

            Assert.Equal("Status", ex.Field);
            var saved = await db.NewContext().Shipments.Include(s => s.StatusUpdates).SingleAsync();
            Assert.Equal(ShipmentStatus.InTransit, saved.CurrentStatus);
            Assert.Equal(2, saved.StatusUpdates.Count); // Booked + InTransit only
        }
    }

    [Fact]
    public async Task Exception_requires_a_note()
    {
        var (db, service, shipment) = await ArrangeAsync();
        using (db)
        {
            var ex = await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.AddStatusUpdateAsync(shipment.Id, Forms.Update(ShipmentStatus.Exception, notes: "  ")));
            Assert.Equal("Notes", ex.Field);

            await service.AddStatusUpdateAsync(shipment.Id, Forms.Update(ShipmentStatus.Exception, "Held at customs: missing invoice"));
            Assert.Equal(ShipmentStatus.Exception, (await db.NewContext().Shipments.SingleAsync()).CurrentStatus);
        }
    }

    [Fact]
    public async Task After_an_exception_the_shipment_resumes_but_still_cannot_go_backwards()
    {
        var (db, service, shipment) = await ArrangeAsync();
        using (db)
        {
            await service.AddStatusUpdateAsync(shipment.Id, Forms.Update(ShipmentStatus.AtCustoms));
            _clock.LocalNow = _clock.LocalNow.AddHours(1);
            await service.AddStatusUpdateAsync(shipment.Id, Forms.Update(ShipmentStatus.Exception, "Docs missing"));
            _clock.LocalNow = _clock.LocalNow.AddHours(1);

            await Assert.ThrowsAsync<BusinessRuleException>(
                () => service.AddStatusUpdateAsync(shipment.Id, Forms.Update(ShipmentStatus.InTransit)));

            var update = await service.AddStatusUpdateAsync(shipment.Id, Forms.Update(ShipmentStatus.OutForDelivery));
            Assert.Equal(ShipmentStatus.OutForDelivery, update.Status);
        }
    }

    [Fact]
    public async Task Adding_an_update_changes_current_status_and_records_history()
    {
        var (db, service, shipment) = await ArrangeAsync();
        using (db)
        {
            await service.AddStatusUpdateAsync(shipment.Id, Forms.Update(ShipmentStatus.Collected));

            var saved = await db.NewContext().Shipments.Include(s => s.StatusUpdates).SingleAsync();
            Assert.Equal(ShipmentStatus.Collected, saved.CurrentStatus);
            Assert.Equal(ShipmentStatus.Collected, saved.StatusUpdates.OrderBy(u => u.Id).Last().Status);
            Assert.Null(saved.DeliveredDate);
        }
    }

    [Fact]
    public async Task Delivering_sets_delivered_date_to_today()
    {
        var (db, service, shipment) = await ArrangeAsync();
        using (db)
        {
            await service.AddStatusUpdateAsync(shipment.Id, Forms.Update(ShipmentStatus.Delivered));

            var saved = await db.NewContext().Shipments.SingleAsync();
            Assert.Equal(ShipmentStatus.Delivered, saved.CurrentStatus);
            Assert.Equal(new DateTime(2026, 11, 10), saved.DeliveredDate);
        }
    }

    [Fact]
    public async Task New_shipment_starts_booked_with_one_history_entry()
    {
        var (db, _, shipment) = await ArrangeAsync();
        using (db)
        {
            var saved = await db.NewContext().Shipments.Include(s => s.StatusUpdates).SingleAsync(s => s.Id == shipment.Id);
            Assert.Equal(ShipmentStatus.Booked, saved.CurrentStatus);
            Assert.Single(saved.StatusUpdates);
        }
    }
}
