using FreightTrack.Web.Services;
using FreightTrack.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace FreightTrack.Web.Controllers;

/// <summary>
/// MVC request flow: the router maps /Customers/Edit/5 to Edit(5). The action asks the service for
/// data, puts it in a view model and returns View(model). Razor renders Views/Customers/Edit.cshtml.
/// </summary>
public class CustomersController(ICustomerService customers) : Controller
{
    // GET /Customers?search=karoo
    public async Task<IActionResult> Index(string? search)
    {
        var model = new CustomerListViewModel
        {
            Search = search,
            Customers = await customers.SearchAsync(search)
        };
        return View(model);
    }

    // GET /Customers/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var customer = await customers.GetWithShipmentsAsync(id);
        if (customer is null) return NotFound();

        return View(new CustomerDetailsViewModel
        {
            Customer = customer,
            Summary = await customers.GetShipmentSummaryAsync(id), // the stored procedure
            Today = DateTime.Today
        });
    }

    // GET /Customers/Create : show an empty form
    public IActionResult Create() => View(new CustomerFormViewModel());

    // POST /Customers/Create : the form submits here
    [HttpPost]
    [ValidateAntiForgeryToken] // rejects forged cross-site form posts (CSRF protection)
    public async Task<IActionResult> Create(CustomerFormViewModel form)
    {
        // ModelState holds the results of the [Required]/[EmailAddress] checks done during model binding.
        if (!ModelState.IsValid) return View(form);

        try
        {
            var customer = await customers.CreateAsync(form);
            TempData["Message"] = $"Customer '{customer.CompanyName}' created.";
            // Post/Redirect/Get: redirecting after a successful POST stops a browser refresh
            // from submitting the form twice.
            return RedirectToAction(nameof(Details), new { id = customer.Id });
        }
        catch (BusinessRuleException ex)
        {
            ModelState.AddModelError(ex.Field, ex.Message);
            return View(form);
        }
    }

    // GET /Customers/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var c = await customers.GetAsync(id);
        if (c is null) return NotFound();

        return View(new CustomerFormViewModel
        {
            Id = c.Id,
            CompanyName = c.CompanyName,
            ContactName = c.ContactName,
            Email = c.Email,
            Phone = c.Phone,
            City = c.City,
            Country = c.Country
        });
    }

    // POST /Customers/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerFormViewModel form)
    {
        if (!ModelState.IsValid) return View(form);

        try
        {
            await customers.UpdateAsync(id, form);
            TempData["Message"] = "Customer updated.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (BusinessRuleException ex)
        {
            ModelState.AddModelError(ex.Field, ex.Message);
            return View(form);
        }
    }
}
