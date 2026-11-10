using FreightTrack.Web.Services;
using FreightTrack.Web.ViewModels;

namespace FreightTrack.Tests;

public class ValidationTests
{
    private static readonly FixedTimeProvider Clock = new(new DateTime(2026, 11, 10, 9, 0, 0));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Weight_must_be_greater_than_zero(decimal weight)
    {
        using var db = new TestDb();
        var service = new ShipmentService(db.Context, Clock);
        var form = Forms.Shipment(db.AddCustomer().Id);
        form.WeightKg = weight;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(form, "Test"));
        Assert.Equal(nameof(form.WeightKg), ex.Field);
    }

    [Fact]
    public async Task Expected_delivery_cannot_be_before_booked_date()
    {
        using var db = new TestDb();
        var service = new ShipmentService(db.Context, Clock);
        var form = Forms.Shipment(db.AddCustomer().Id);
        form.BookedDate = new DateTime(2026, 11, 5);
        form.ExpectedDeliveryDate = new DateTime(2026, 11, 4);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(form, "Test"));
        Assert.Equal(nameof(form.ExpectedDeliveryDate), ex.Field);
        Assert.Empty(db.NewContext().Shipments);
    }

    [Fact]
    public async Task Expected_delivery_on_the_booked_date_is_allowed()
    {
        using var db = new TestDb();
        var service = new ShipmentService(db.Context, Clock);
        var form = Forms.Shipment(db.AddCustomer().Id);
        form.ExpectedDeliveryDate = form.BookedDate;

        var shipment = await service.CreateAsync(form, "Test");
        Assert.True(shipment.Id > 0);
    }

    [Fact]
    public async Task Origin_and_destination_must_differ()
    {
        using var db = new TestDb();
        var service = new ShipmentService(db.Context, Clock);
        var form = Forms.Shipment(db.AddCustomer().Id);
        form.Destination = " durban, south africa ";

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(form, "Test"));
        Assert.Equal(nameof(form.Destination), ex.Field);
    }

    [Fact]
    public async Task Shipment_must_belong_to_an_existing_customer()
    {
        using var db = new TestDb();
        var service = new ShipmentService(db.Context, Clock);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(Forms.Shipment(customerId: 999), "Test"));
        Assert.Equal("CustomerId", ex.Field);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public async Task Customer_needs_a_valid_email(string email)
    {
        using var db = new TestDb();
        var service = new CustomerService(db.Context, Clock);
        var form = new CustomerFormViewModel
        {
            CompanyName = "Table Bay Wines", ContactName = "Charlotte", Email = email, City = "Stellenbosch", Country = "South Africa"
        };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(form));
        Assert.Equal(nameof(form.Email), ex.Field);
    }

    [Fact]
    public async Task Customer_search_matches_company_or_contact_name_case_insensitively()
    {
        using var db = new TestDb();
        db.AddCustomer("Karoo Fresh Produce", "Nomsa Dlamini");
        db.AddCustomer("Table Bay Wines", "Charlotte Meyer");
        db.AddCustomer("Highveld Mining", "Pieter Botha");
        var service = new CustomerService(db.Context, Clock);

        Assert.Equal("Karoo Fresh Produce", Assert.Single(await service.SearchAsync("karoo")).CompanyName);
        Assert.Equal("Table Bay Wines", Assert.Single(await service.SearchAsync("meyer")).CompanyName);
        Assert.Equal(3, (await service.SearchAsync(null)).Count);
    }
}
