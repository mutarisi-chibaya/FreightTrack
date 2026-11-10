using System.ComponentModel.DataAnnotations;
using FreightTrack.Web.Data;
using FreightTrack.Web.Models;
using FreightTrack.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace FreightTrack.Web.Services;

// Controllers depend on this interface, not the class. That lets DI swap in a fake for testing
// and keeps controllers unaware of EF Core. (Same idea as coding to interfaces in Spring.)
public interface ICustomerService
{
    Task<List<CustomerListItem>> SearchAsync(string? search);
    Task<List<Customer>> GetAllAsync();
    Task<Customer?> GetAsync(int id);
    Task<Customer?> GetWithShipmentsAsync(int id);
    Task<Customer> CreateAsync(CustomerFormViewModel form);
    Task UpdateAsync(int id, CustomerFormViewModel form);
    Task<List<CustomerShipmentSummaryRow>> GetShipmentSummaryAsync(int customerId);
}

public class CustomerService(AppDbContext db, TimeProvider time) : ICustomerService
{
    public async Task<List<CustomerListItem>> SearchAsync(string? search)
    {
        // IQueryable builds up a SQL query step by step; nothing hits the database until ToListAsync().
        IQueryable<Customer> query = db.Customers;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            // EF.Functions.Like becomes SQL "LIKE @p". The value is sent as a parameter, so this is
            // safe from SQL injection. Matches on company OR contact name.
            query = query.Where(c => EF.Functions.Like(c.CompanyName, pattern)
                                  || EF.Functions.Like(c.ContactName, pattern));
        }

        // Project straight into the list DTO: SQL only selects the columns we show, and the
        // shipment count becomes a COUNT subquery instead of loading every shipment.
        return await query
            .OrderBy(c => c.CompanyName)
            .Select(c => new CustomerListItem
            {
                Id = c.Id,
                CompanyName = c.CompanyName,
                ContactName = c.ContactName,
                Email = c.Email,
                City = c.City,
                Country = c.Country,
                ShipmentCount = c.Shipments.Count
            })
            .ToListAsync();
    }

    public Task<List<Customer>> GetAllAsync() =>
        db.Customers.AsNoTracking().OrderBy(c => c.CompanyName).ToListAsync();

    public Task<Customer?> GetAsync(int id) => db.Customers.FindAsync(id).AsTask();

    public Task<Customer?> GetWithShipmentsAsync(int id) =>
        db.Customers
          .AsNoTracking()   // read-only page: skip change tracking for speed
          .Include(c => c.Shipments.OrderByDescending(s => s.BookedDate))
          .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<Customer> CreateAsync(CustomerFormViewModel form)
    {
        Validate(form);
        var customer = new Customer { CreatedAt = time.GetLocalNow().DateTime };
        Apply(form, customer);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    public async Task UpdateAsync(int id, CustomerFormViewModel form)
    {
        Validate(form);
        var customer = await db.Customers.FindAsync(id)
            ?? throw new KeyNotFoundException($"Customer {id} not found.");
        Apply(form, customer);
        await db.SaveChangesAsync(); // EF noticed which properties changed and UPDATEs only those
    }

    /// <summary>
    /// Calls the hand-written stored procedure (database/stored_procedures.sql).
    /// The interpolated string is turned into a SQL parameter (@p0) by EF, not pasted into the SQL text.
    /// </summary>
    public Task<List<CustomerShipmentSummaryRow>> GetShipmentSummaryAsync(int customerId) =>
        db.Database
          .SqlQuery<CustomerShipmentSummaryRow>($"EXEC dbo.usp_GetCustomerShipmentSummary @CustomerId = {customerId}")
          .ToListAsync();

    /// <summary>
    /// The service re-checks the rules even though the form has [Required] etc., because the
    /// service is the single source of truth: an API or a test could call it without going through MVC.
    /// </summary>
    private static void Validate(CustomerFormViewModel form)
    {
        if (string.IsNullOrWhiteSpace(form.CompanyName))
            throw new BusinessRuleException(nameof(form.CompanyName), "Company name is required.");
        if (string.IsNullOrWhiteSpace(form.ContactName))
            throw new BusinessRuleException(nameof(form.ContactName), "Contact name is required.");
        if (string.IsNullOrWhiteSpace(form.Email) || !new EmailAddressAttribute().IsValid(form.Email))
            throw new BusinessRuleException(nameof(form.Email), "A valid email address is required.");
        if (string.IsNullOrWhiteSpace(form.City))
            throw new BusinessRuleException(nameof(form.City), "City is required.");
        if (string.IsNullOrWhiteSpace(form.Country))
            throw new BusinessRuleException(nameof(form.Country), "Country is required.");
    }

    private static void Apply(CustomerFormViewModel form, Customer customer)
    {
        customer.CompanyName = form.CompanyName.Trim();
        customer.ContactName = form.ContactName.Trim();
        customer.Email = form.Email.Trim();
        customer.Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim();
        customer.City = form.City.Trim();
        customer.Country = form.Country.Trim();
    }
}
