using FreightTrack.Web.Models;

namespace FreightTrack.Web.Services;

public enum JourneyStepState { Done, Current, Skipped, Upcoming }

public record JourneyStep(ShipmentStatus Status, JourneyStepState State);

/// <summary>One choice in the "record an update" panel, with the hints the UI shows for it.</summary>
public record StatusOption(
    ShipmentStatus Status,
    string Label,
    bool IsSuggested,
    string SuggestedLocation,
    IReadOnlyList<ShipmentStatus> SkippedSteps);

/// <summary>
/// Guidance for the person updating a shipment: where it is in its journey, what the logical next
/// step is, and a sensible default location for each choice. This only *suggests*. The hard rules
/// (no going backwards, note required for Exception) are still enforced by ShipmentStatusRules and
/// ShipmentService, so a user can't break them by ignoring a suggestion.
/// </summary>
public static class ShipmentJourney
{
    /// <summary>The normal steps for this shipment. Domestic shipments don't go through customs.</summary>
    public static IReadOnlyList<ShipmentStatus> StepsFor(bool crossesBorder) =>
        crossesBorder
            ? [ShipmentStatus.Booked, ShipmentStatus.Collected, ShipmentStatus.InTransit, ShipmentStatus.AtCustoms, ShipmentStatus.OutForDelivery, ShipmentStatus.Delivered]
            : [ShipmentStatus.Booked, ShipmentStatus.Collected, ShipmentStatus.InTransit, ShipmentStatus.OutForDelivery, ShipmentStatus.Delivered];

    /// <summary>The next step after the last one reached, or null when the shipment is delivered.</summary>
    public static ShipmentStatus? SuggestedNext(ShipmentStatus lastProgress, bool crossesBorder) =>
        StepsFor(crossesBorder).Where(s => s > lastProgress).Cast<ShipmentStatus?>().FirstOrDefault();

    /// <summary>Normal steps that would be jumped over by going from lastProgress straight to next.</summary>
    public static IReadOnlyList<ShipmentStatus> SkippedSteps(ShipmentStatus lastProgress, ShipmentStatus next, bool crossesBorder) =>
        next == ShipmentStatus.Exception
            ? []
            : StepsFor(crossesBorder).Where(s => s > lastProgress && s < next).ToList();

    /// <summary>Where the goods most likely are for a given status, used to pre-fill the Location box.</summary>
    public static string SuggestedLocation(ShipmentStatus status, Shipment shipment, string currentLocation) => status switch
    {
        ShipmentStatus.Booked or ShipmentStatus.Collected => shipment.Origin,
        ShipmentStatus.AtCustoms or ShipmentStatus.OutForDelivery or ShipmentStatus.Delivered => shipment.Destination,
        _ => currentLocation   // In transit / Exception: wherever it was last seen (the clerk usually edits this)
    };

    public static IReadOnlyList<JourneyStep> Steps(Shipment shipment)
    {
        var history = shipment.StatusUpdates;
        var last = ShipmentStatusRules.LastProgressStatus(history);
        var reached = history.Select(u => u.Status).ToHashSet();

        // Show customs if the route needs it, or if it happened anyway.
        var steps = StepsFor(shipment.CrossesBorder || reached.Contains(ShipmentStatus.AtCustoms));

        return steps.Select(s => new JourneyStep(s,
            s == last ? (s == ShipmentStatus.Delivered ? JourneyStepState.Done : JourneyStepState.Current)
            : s > last ? JourneyStepState.Upcoming
            : reached.Contains(s) ? JourneyStepState.Done
            : JourneyStepState.Skipped)).ToList();
    }

    public static IReadOnlyList<StatusOption> Options(Shipment shipment)
    {
        var last = ShipmentStatusRules.LastProgressStatus(shipment.StatusUpdates);
        var crosses = shipment.CrossesBorder;
        var suggested = SuggestedNext(last, crosses);
        var currentLocation = shipment.StatusUpdates
            .OrderByDescending(u => u.UpdatedAt).ThenByDescending(u => u.Id)
            .Select(u => u.Location).FirstOrDefault() ?? shipment.Origin;

        return ShipmentStatusRules.AllowedNextStatuses(shipment.StatusUpdates)
            // Hide customs for domestic shipments unless that's where it already is.
            .Where(s => s != ShipmentStatus.AtCustoms || crosses || last == ShipmentStatus.AtCustoms)
            .Select(s => new StatusOption(
                s,
                s == ShipmentStatus.Exception ? "Report a problem (Exception)"
                    : s == last ? $"{s.GetDisplayName()}: new location"
                    : s.GetDisplayName(),
                s == suggested,
                SuggestedLocation(s, shipment, currentLocation),
                SkippedSteps(last, s, crosses)))
            .ToList();
    }
}
