using FreightTrack.Web.Models;
using FreightTrack.Web.Services;

namespace FreightTrack.Tests;

public class ShipmentJourneyTests
{
    private static Shipment Make(string origin, string destination, params ShipmentStatus[] history)
    {
        var s = new Shipment { Origin = origin, Destination = destination };
        var t = new DateTime(2026, 11, 1, 8, 0, 0);
        for (int i = 0; i < history.Length; i++)
            s.StatusUpdates.Add(new ShipmentStatusUpdate { Id = i + 1, Status = history[i], Location = "X", UpdatedAt = t.AddHours(i) });
        s.CurrentStatus = history[^1];
        return s;
    }

    [Theory]
    [InlineData("Durban, South Africa", "Gaborone, Botswana", true)]
    [InlineData("Durban, South Africa", "Johannesburg, South Africa", false)]
    [InlineData("Durban, south africa", "Cape Town, South Africa", false)] // case-insensitive
    public void Crosses_border_compares_countries(string origin, string destination, bool expected)
    {
        Assert.Equal(expected, new Shipment { Origin = origin, Destination = destination }.CrossesBorder);
    }

    [Theory]
    [InlineData(ShipmentStatus.Booked, true, ShipmentStatus.Collected)]
    [InlineData(ShipmentStatus.InTransit, true, ShipmentStatus.AtCustoms)]
    [InlineData(ShipmentStatus.InTransit, false, ShipmentStatus.OutForDelivery)] // domestic: no customs
    [InlineData(ShipmentStatus.OutForDelivery, false, ShipmentStatus.Delivered)]
    public void Suggests_the_next_logical_step(ShipmentStatus last, bool crossesBorder, ShipmentStatus expected)
    {
        Assert.Equal(expected, ShipmentJourney.SuggestedNext(last, crossesBorder));
    }

    [Fact]
    public void Nothing_is_suggested_after_delivery()
    {
        Assert.Null(ShipmentJourney.SuggestedNext(ShipmentStatus.Delivered, crossesBorder: true));
    }

    [Fact]
    public void Jumping_ahead_reports_the_skipped_steps()
    {
        var skipped = ShipmentJourney.SkippedSteps(ShipmentStatus.Collected, ShipmentStatus.OutForDelivery, crossesBorder: true);
        Assert.Equal([ShipmentStatus.InTransit, ShipmentStatus.AtCustoms], skipped);

        Assert.Empty(ShipmentJourney.SkippedSteps(ShipmentStatus.Collected, ShipmentStatus.InTransit, crossesBorder: true));
        Assert.Empty(ShipmentJourney.SkippedSteps(ShipmentStatus.Collected, ShipmentStatus.Exception, crossesBorder: true));
    }

    [Fact]
    public void Options_preselect_next_step_and_prefill_sensible_locations()
    {
        var s = Make("Durban, South Africa", "Gaborone, Botswana", ShipmentStatus.Booked);

        var options = ShipmentJourney.Options(s);

        var suggested = Assert.Single(options, o => o.IsSuggested);
        Assert.Equal(ShipmentStatus.Collected, suggested.Status);
        Assert.Equal("Durban, South Africa", suggested.SuggestedLocation);                                    // collected at origin
        Assert.Equal("Gaborone, Botswana", options.Single(o => o.Status == ShipmentStatus.Delivered).SuggestedLocation);
        Assert.Contains(options, o => o.Status == ShipmentStatus.Exception);
    }

    [Fact]
    public void Domestic_shipments_are_not_offered_customs()
    {
        var s = Make("Durban, South Africa", "Johannesburg, South Africa", ShipmentStatus.Booked, ShipmentStatus.InTransit);

        Assert.DoesNotContain(ShipmentJourney.Options(s), o => o.Status == ShipmentStatus.AtCustoms);
        Assert.DoesNotContain(ShipmentJourney.Steps(s), step => step.Status == ShipmentStatus.AtCustoms);
    }

    [Fact]
    public void Journey_marks_done_current_skipped_and_upcoming()
    {
        // Booked → Out for delivery directly (Collected and In transit were skipped)
        var s = Make("Durban, South Africa", "Johannesburg, South Africa", ShipmentStatus.Booked, ShipmentStatus.OutForDelivery);

        var states = ShipmentJourney.Steps(s).ToDictionary(x => x.Status, x => x.State);

        Assert.Equal(JourneyStepState.Done, states[ShipmentStatus.Booked]);
        Assert.Equal(JourneyStepState.Skipped, states[ShipmentStatus.Collected]);
        Assert.Equal(JourneyStepState.Skipped, states[ShipmentStatus.InTransit]);
        Assert.Equal(JourneyStepState.Current, states[ShipmentStatus.OutForDelivery]);
        Assert.Equal(JourneyStepState.Upcoming, states[ShipmentStatus.Delivered]);
    }

    [Fact]
    public void During_an_exception_the_last_normal_step_stays_current()
    {
        var s = Make("Durban, South Africa", "Gaborone, Botswana",
            ShipmentStatus.Booked, ShipmentStatus.InTransit, ShipmentStatus.AtCustoms, ShipmentStatus.Exception);

        var current = Assert.Single(ShipmentJourney.Steps(s), x => x.State == JourneyStepState.Current);
        Assert.Equal(ShipmentStatus.AtCustoms, current.Status);
        Assert.Equal(ShipmentStatus.OutForDelivery, ShipmentJourney.Options(s).Single(o => o.IsSuggested).Status);
    }
}
