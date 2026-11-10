using FreightTrack.Web.Data;
using FreightTrack.Web.Models;
using FreightTrack.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace FreightTrack.Web.Services;

public interface IShipmentService
{
    DateTime Today { get; }
    Task<PagedResult<Shipment>> ListAsync(ShipmentFilter filter);
    Task<Shipment?> GetDetailsAsync(int id);
    Task<Shipment?> FindByTrackingNumberAsync(string trackingNumber);
    Task<Shipment> CreateAsync(ShipmentFormViewModel form, string createdBy);
    Task UpdateAsync(int id, ShipmentFormViewModel form);
    Task<ShipmentStatusUpdate> AddStatusUpdateAsync(int shipmentId, StatusUpdateFormViewModel form);
    Task<string> GenerateTrackingNumberAsync();
    Task<DashboardViewModel> GetDashboardAsync();
}

/// <summary>
/// All shipment business rules live here, not in the controller. Why:
///  - one place to read and change the rules;
///  - they can be unit tested without HTTP, views or a browser;
///  - any future entry point (REST API, background import) gets the same rules for free.
/// The controller's only job is HTTP: read the request, call the service, choose a view.
///
/// TimeProvider is injected instead of calling DateTime.Now directly, so tests can freeze "today"
/// and check the late-shipment logic deterministically.
/// </summary>
public class ShipmentService(AppDbContext db, TimeProvider time) : IShipmentService
{
    public DateTime Today => time.GetLocalNow().Date;
    private DateTime Now => time.GetLocalNow().DateTime;

    public async Task<PagedResult<Shipment>> ListAsync(ShipmentFilter filter)
    {
        IQueryable<Shipment> query = db.Shipments.AsNoTracking().Include(s => s.Customer);

        // Each filter adds a WHERE clause only if it was supplied.
        if (filter.Status is not null)
            query = query.Where(s => s.CurrentStatus == filter.Status);
        if (filter.Mode is not null)
            query = query.Where(s => s.Mode == filter.Mode);
        if (filter.CustomerId is not null)
            query = query.Where(s => s.CustomerId == filter.CustomerId);
        if (filter.LateOnly)
            query = query.Where(Shipment.IsLateExpression(Today));

        var pageSize = Math.Clamp(filter.PageSize, 5, 100);
        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var page = Math.Clamp(filter.Page, 1, totalPages);

        // Paging happens in SQL (OFFSET ... FETCH NEXT ...), so we never load all rows.
        // A stable ORDER BY is required for paging to be consistent between pages.
        var items = await query
            .OrderByDescending(s => s.BookedDate).ThenByDescending(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Shipment> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public Task<Shipment?> GetDetailsAsync(int id) =>
        db.Shipments
          .AsNoTracking()
          .Include(s => s.Customer)
          .Include(s => s.StatusUpdates.OrderByDescending(u => u.UpdatedAt).ThenByDescending(u => u.Id))
          .FirstOrDefaultAsync(s => s.Id == id);

    public Task<Shipment?> FindByTrackingNumberAsync(string trackingNumber)
    {
        var normalised = trackingNumber.Trim().ToUpperInvariant();
        return db.Shipments.AsNoTracking().FirstOrDefaultAsync(s => s.TrackingNumber == normalised);
    }

    public async Task<Shipment> CreateAsync(ShipmentFormViewModel form, string createdBy)
    {
        await ValidateAsync(form);

        var shipment = new Shipment
        {
            TrackingNumber = await GenerateTrackingNumberAsync(),
            CurrentStatus = ShipmentStatus.Booked,
        };
        Apply(form, shipment);

        // Every shipment starts with a "Booked" entry, so CurrentStatus always matches the latest update.
        shipment.StatusUpdates.Add(new ShipmentStatusUpdate
        {
            Status = ShipmentStatus.Booked,
            Location = shipment.Origin,
            Notes = "Shipment booked.",
            UpdatedBy = createdBy,
            UpdatedAt = Now,
        });

        db.Shipments.Add(shipment);
        await db.SaveChangesAsync(); // shipment + first update are inserted in ONE transaction
        return shipment;
    }

    public async Task UpdateAsync(int id, ShipmentFormViewModel form)
    {
        await ValidateAsync(form);
        var shipment = await db.Shipments.FindAsync(id)
            ?? throw new KeyNotFoundException($"Shipment {id} not found.");
        Apply(form, shipment); // TrackingNumber and CurrentStatus are deliberately not touched
        await db.SaveChangesAsync();
    }

    public async Task<ShipmentStatusUpdate> AddStatusUpdateAsync(int shipmentId, StatusUpdateFormViewModel form)
    {
        var shipment = await db.Shipments
            .Include(s => s.StatusUpdates)
            .FirstOrDefaultAsync(s => s.Id == shipmentId)
            ?? throw new KeyNotFoundException($"Shipment {shipmentId} not found.");

        if (form.Status is not { } next)
            throw new BusinessRuleException(nameof(form.Status), "Choose a status.");
        if (string.IsNullOrWhiteSpace(form.Location))
            throw new BusinessRuleException(nameof(form.Location), "Location is required.");
        if (string.IsNullOrWhiteSpace(form.UpdatedBy))
            throw new BusinessRuleException(nameof(form.UpdatedBy), "Please enter who is making this update.");
        if (next == ShipmentStatus.Exception && string.IsNullOrWhiteSpace(form.Notes))
            throw new BusinessRuleException(nameof(form.Notes), "A note is required when recording an exception (e.g. what was damaged or why it is held).");

        var last = ShipmentStatusRules.LastProgressStatus(shipment.StatusUpdates);
        if (!ShipmentStatusRules.CanMoveTo(last, next))
        {
            throw new BusinessRuleException(nameof(form.Status), last == ShipmentStatus.Delivered
                ? "This shipment has already been delivered; no further updates are allowed."
                : $"Status can only move forward. It is currently '{last.GetDisplayName()}', so it can't go to '{next.GetDisplayName()}'.");
        }

        var update = new ShipmentStatusUpdate
        {
            Status = next,
            Location = form.Location.Trim(),
            Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim(),
            UpdatedBy = form.UpdatedBy.Trim(),
            UpdatedAt = Now,
        };
        shipment.StatusUpdates.Add(update);

        // Keep the denormalised CurrentStatus in sync. It's saved in the same transaction as the
        // new history row, so the two can never disagree.
        shipment.CurrentStatus = next;
        if (next == ShipmentStatus.Delivered)
            shipment.DeliveredDate = Today;

        await db.SaveChangesAsync();
        return update;
    }

    /// <summary>
    /// Format: FT-{year}-{6-digit sequence}, e.g. FT-2026-000123. The sequence restarts each year.
    /// Because the number is zero-padded, sorting the strings sorts the numbers too, so the highest
    /// existing number for this year is just the "max" string.
    /// If two users save at the exact same instant, the UNIQUE index on TrackingNumber rejects the
    /// duplicate rather than silently storing it. (A SQL SEQUENCE would remove even that edge case.)
    /// </summary>
    public async Task<string> GenerateTrackingNumberAsync()
    {
        var prefix = $"FT-{Today.Year}-";
        var latest = await db.Shipments
            .Where(s => s.TrackingNumber.StartsWith(prefix))
            .OrderByDescending(s => s.TrackingNumber)
            .Select(s => s.TrackingNumber)
            .FirstOrDefaultAsync();

        var next = latest is null ? 1 : int.Parse(latest[prefix.Length..]) + 1;
        return $"{prefix}{next:D6}";
    }

    public async Task<DashboardViewModel> GetDashboardAsync()
    {
        var today = Today;

        // GROUP BY CurrentStatus in SQL: one round trip, no rows loaded into memory.
        var counts = await db.Shipments
            .GroupBy(s => s.CurrentStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var lateQuery = db.Shipments.AsNoTracking().Where(Shipment.IsLateExpression(today));

        // "Most overdue" = earliest expected delivery date among late shipments.
        var mostOverdue = await lateQuery
            .Include(s => s.Customer)
            .OrderBy(s => s.ExpectedDeliveryDate).ThenBy(s => s.Id)
            .Take(10)
            .ToListAsync();

        return new DashboardViewModel
        {
            // Every status in flow order, including ones with zero shipments.
            StatusCounts = Enum.GetValues<ShipmentStatus>()
                .Select(s => (s, counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0))
                .ToList(),
            TotalShipments = counts.Sum(c => c.Count),
            InProgressCount = counts.Where(c => c.Status != ShipmentStatus.Delivered).Sum(c => c.Count),
            LateCount = await lateQuery.CountAsync(),
            MostOverdue = mostOverdue,
            Today = today,
        };
    }

    private async Task ValidateAsync(ShipmentFormViewModel form)
    {
        if (form.CustomerId is null || !await db.Customers.AnyAsync(c => c.Id == form.CustomerId))
            throw new BusinessRuleException(nameof(form.CustomerId), "Choose an existing customer.");
        if (string.IsNullOrWhiteSpace(form.Origin))
            throw new BusinessRuleException(nameof(form.Origin), "Origin is required.");
        if (string.IsNullOrWhiteSpace(form.Destination))
            throw new BusinessRuleException(nameof(form.Destination), "Destination is required.");
        if (form.Mode is null)
            throw new BusinessRuleException(nameof(form.Mode), "Choose a transport mode.");
        if (form.WeightKg is null || form.WeightKg <= 0)
            throw new BusinessRuleException(nameof(form.WeightKg), "Weight must be greater than 0.");
        if (form.DeclaredValue is null || form.DeclaredValue < 0)
            throw new BusinessRuleException(nameof(form.DeclaredValue), "Declared value can't be negative.");
        if (form.BookedDate is null)
            throw new BusinessRuleException(nameof(form.BookedDate), "Booked date is required.");
        if (form.ExpectedDeliveryDate is null)
            throw new BusinessRuleException(nameof(form.ExpectedDeliveryDate), "Expected delivery date is required.");
        if (form.ExpectedDeliveryDate.Value.Date < form.BookedDate.Value.Date)
            throw new BusinessRuleException(nameof(form.ExpectedDeliveryDate), "Expected delivery can't be before the booked date.");
    }

    private static void Apply(ShipmentFormViewModel form, Shipment shipment)
    {
        shipment.CustomerId = form.CustomerId!.Value;
        shipment.Origin = form.Origin.Trim();
        shipment.Destination = form.Destination.Trim();
        shipment.Mode = form.Mode!.Value;
        shipment.WeightKg = form.WeightKg!.Value;
        shipment.DeclaredValue = form.DeclaredValue!.Value;
        shipment.BookedDate = form.BookedDate!.Value.Date;
        shipment.ExpectedDeliveryDate = form.ExpectedDeliveryDate!.Value.Date;
    }
}
