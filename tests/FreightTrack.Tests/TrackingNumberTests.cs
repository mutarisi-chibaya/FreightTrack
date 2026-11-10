using System.Text.RegularExpressions;
using FreightTrack.Web.Services;

namespace FreightTrack.Tests;

public class TrackingNumberTests
{
    private static readonly FixedTimeProvider Clock = new(new DateTime(2026, 11, 10, 9, 0, 0));

    [Fact]
    public async Task First_tracking_number_of_the_year_is_000001()
    {
        using var db = new TestDb();
        var service = new ShipmentService(db.Context, Clock);

        var number = await service.GenerateTrackingNumberAsync();

        Assert.Equal("FT-2026-000001", number);
    }

    [Fact]
    public async Task Created_shipments_get_unique_tracking_numbers_in_the_right_format()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer();
        var service = new ShipmentService(db.Context, Clock);

        var numbers = new List<string>();
        for (int i = 0; i < 12; i++)
            numbers.Add((await service.CreateAsync(Forms.Shipment(customer.Id), "Test")).TrackingNumber);

        Assert.All(numbers, n => Assert.Matches(new Regex(@"^FT-2026-\d{6}$"), n));
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        Assert.Equal("FT-2026-000012", numbers[^1]); // sequential
    }

    [Fact]
    public async Task Sequence_restarts_in_a_new_year()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer();
        var clock = new FixedTimeProvider(new DateTime(2026, 12, 31, 16, 0, 0));
        var service = new ShipmentService(db.Context, clock);
        await service.CreateAsync(Forms.Shipment(customer.Id), "Test");

        clock.LocalNow = new DateTime(2027, 1, 2, 8, 0, 0);

        Assert.Equal("FT-2027-000001", await service.GenerateTrackingNumberAsync());
    }

    [Fact]
    public async Task Editing_a_shipment_does_not_change_its_tracking_number()
    {
        using var db = new TestDb();
        var customer = db.AddCustomer();
        var service = new ShipmentService(db.Context, Clock);
        var shipment = await service.CreateAsync(Forms.Shipment(customer.Id), "Test");
        var original = shipment.TrackingNumber;

        var form = Forms.Shipment(customer.Id);
        form.TrackingNumber = "FT-9999-999999"; // even if someone sends one
        form.Destination = "Harare, Zimbabwe";
        await service.UpdateAsync(shipment.Id, form);

        var reloaded = await db.NewContext().Shipments.FindAsync(shipment.Id);
        Assert.Equal(original, reloaded!.TrackingNumber);
        Assert.Equal("Harare, Zimbabwe", reloaded.Destination);
    }
}
