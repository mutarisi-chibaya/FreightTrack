using FreightTrack.Web.Models;

namespace FreightTrack.Web.Services;

/// <summary>
/// The status-transition rules, kept as pure functions (no database), so they are trivial to unit test.
///
/// Rules:
///  1. Status only moves forward: Booked → Collected → In transit → At customs → Out for delivery → Delivered.
///     Steps may be skipped (domestic road freight never goes through customs), and repeating the same
///     status is allowed (e.g. a second "In transit" update from a new location).
///  2. Any status can move to Exception (a required note is checked in ShipmentService).
///  3. After an Exception, the shipment resumes from where it was: it may not go backwards from the
///     last *normal* status it reached.
///  4. Delivered is final. Nothing can follow it, because the shipment is closed and DeliveredDate is set.
///     (Design choice: a problem found after delivery is a claim, not a tracking status.)
/// </summary>
public static class ShipmentStatusRules
{
    public static bool CanMoveTo(ShipmentStatus lastProgressStatus, ShipmentStatus next)
    {
        if (lastProgressStatus == ShipmentStatus.Delivered) return false;
        if (next == ShipmentStatus.Exception) return true;

        // The "can't go backwards" check. Works because the enum values are numbered in flow order.
        return next >= lastProgressStatus;
    }

    /// <summary>The latest non-Exception status in the history (Booked if there is none).</summary>
    public static ShipmentStatus LastProgressStatus(IEnumerable<ShipmentStatusUpdate> history) =>
        history
            .Where(u => u.Status != ShipmentStatus.Exception)
            .OrderBy(u => u.UpdatedAt).ThenBy(u => u.Id)
            .Select(u => u.Status)
            .LastOrDefault(ShipmentStatus.Booked);

    public static IReadOnlyList<ShipmentStatus> AllowedNextStatuses(IEnumerable<ShipmentStatusUpdate> history)
    {
        var last = LastProgressStatus(history);
        return Enum.GetValues<ShipmentStatus>().Where(s => CanMoveTo(last, s)).ToList();
    }
}
