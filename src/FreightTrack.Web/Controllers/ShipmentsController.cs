using FreightTrack.Web.Models;
using FreightTrack.Web.Services;
using FreightTrack.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FreightTrack.Web.Controllers;

public class ShipmentsController(IShipmentService shipments, ICustomerService customers) : Controller
{
    // GET /Shipments?status=InTransit&mode=Sea&customerId=3&lateOnly=true&page=2
    // [FromQuery] tells model binding to fill the ShipmentFilter from the query string.
    public async Task<IActionResult> Index([FromQuery] ShipmentFilter filter)
    {
        return View(new ShipmentListViewModel
        {
            Filter = filter,
            Results = await shipments.ListAsync(filter),
            Customers = await CustomerOptionsAsync(),
            Today = shipments.Today
        });
    }

    // GET /Shipments/Track?trackingNumber=FT-2026-000123 (the search box in the navbar)
    public async Task<IActionResult> Track(string? trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
            return RedirectToAction(nameof(Index));

        var shipment = await shipments.FindByTrackingNumberAsync(trackingNumber);
        if (shipment is null)
        {
            TempData["Error"] = $"No shipment found with tracking number '{trackingNumber.Trim()}'.";
            return RedirectToAction(nameof(Index));
        }
        return RedirectToAction(nameof(Details), new { id = shipment.Id });
    }

    // GET /Shipments/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var model = await BuildDetailsAsync(id);
        return model is null ? NotFound() : View(model);
    }

    // POST /Shipments/AddStatusUpdate/5 (the form on the details page)
    // [Bind(Prefix)] because the form fields are named "NewUpdate.Status", "NewUpdate.Location", ...
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddStatusUpdate(int id, [Bind(Prefix = "NewUpdate")] StatusUpdateFormViewModel form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var update = await shipments.AddStatusUpdateAsync(id, form);
                TempData["Message"] = $"Status updated to '{update.Status.GetDisplayName()}'.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError($"NewUpdate.{ex.Field}", ex.Message);
            }
        }

        // Something was invalid: show the details page again with the errors and what they typed.
        var model = await BuildDetailsAsync(id);
        if (model is null) return NotFound();
        model.NewUpdate = form;
        return View(nameof(Details), model);
    }

    // GET /Shipments/Create?customerId=3 (customerId is optional; it pre-selects the customer)
    public async Task<IActionResult> Create(int? customerId)
    {
        var today = shipments.Today;
        return View(new ShipmentFormViewModel
        {
            CustomerId = customerId,
            BookedDate = today,
            ExpectedDeliveryDate = today.AddDays(7),
            Customers = await CustomerOptionsAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ShipmentFormViewModel form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                // No login yet, so new shipments are attributed to "Operations".
                var shipment = await shipments.CreateAsync(form, createdBy: "Operations");
                TempData["Message"] = $"Shipment {shipment.TrackingNumber} created.";
                return RedirectToAction(nameof(Details), new { id = shipment.Id });
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(ex.Field, ex.Message);
            }
        }

        form.Customers = await CustomerOptionsAsync();
        return View(form);
    }

    // GET /Shipments/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var s = await shipments.GetDetailsAsync(id);
        if (s is null) return NotFound();

        return View(new ShipmentFormViewModel
        {
            Id = s.Id,
            TrackingNumber = s.TrackingNumber,
            CustomerId = s.CustomerId,
            Origin = s.Origin,
            Destination = s.Destination,
            Mode = s.Mode,
            WeightKg = s.WeightKg,
            DeclaredValue = s.DeclaredValue,
            BookedDate = s.BookedDate,
            ExpectedDeliveryDate = s.ExpectedDeliveryDate,
            Customers = await CustomerOptionsAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ShipmentFormViewModel form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await shipments.UpdateAsync(id, form);
                TempData["Message"] = "Shipment updated.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(ex.Field, ex.Message);
            }
        }

        // The tracking number isn't posted back (it's read-only), so reload it for display.
        form.TrackingNumber = (await shipments.GetDetailsAsync(id))?.TrackingNumber;
        form.Customers = await CustomerOptionsAsync();
        return View(form);
    }

    private async Task<ShipmentDetailsViewModel?> BuildDetailsAsync(int id)
    {
        var shipment = await shipments.GetDetailsAsync(id);
        if (shipment is null) return null;

        return new ShipmentDetailsViewModel
        {
            Shipment = shipment,
            AllowedNextStatuses = ShipmentStatusRules.AllowedNextStatuses(shipment.StatusUpdates),
            Today = shipments.Today
        };
    }

    private async Task<IEnumerable<SelectListItem>> CustomerOptionsAsync() =>
        (await customers.GetAllAsync())
            .Select(c => new SelectListItem(c.CompanyName, c.Id.ToString()));
}
