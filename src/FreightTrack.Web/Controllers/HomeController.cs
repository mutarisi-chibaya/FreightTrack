using System.Diagnostics;
using FreightTrack.Web.Models;
using FreightTrack.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace FreightTrack.Web.Controllers;

// Constructor injection: ASP.NET Core sees the IShipmentService parameter, looks it up in the
// DI container (registered in Program.cs) and passes in a ShipmentService automatically.
public class HomeController(IShipmentService shipments) : Controller
{
    // GET /  → the managers' dashboard
    public async Task<IActionResult> Index()
    {
        var model = await shipments.GetDashboardAsync();
        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
