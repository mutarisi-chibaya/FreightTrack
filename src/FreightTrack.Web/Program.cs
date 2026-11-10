using FreightTrack.Web.Data;
using FreightTrack.Web.Services;
using Microsoft.EntityFrameworkCore;

// Program.cs is the entry point of the app (like `public static void main` in Java).
// C# "top-level statements" let us skip writing the Program class and Main method by hand.

// 1) BUILD THE HOST
// The builder loads configuration (appsettings.json, environment variables), logging,
// and the dependency injection (DI) container. Think of it like Spring Boot's ApplicationContext.
var builder = WebApplication.CreateBuilder(args);

// 2) REGISTER SERVICES IN THE DI CONTAINER
// Anything registered here can be "asked for" in a constructor, and ASP.NET Core supplies it.
// AddControllersWithViews() registers everything MVC needs: controllers, Razor views, model binding, validation.
builder.Services.AddControllersWithViews();

// EF Core DbContext, using the connection string from appsettings.json. Registered as "scoped":
// one instance per HTTP request, so each request gets its own unit of work.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Our services. Scoped, like the DbContext they depend on.
// Controllers ask for the interface; DI decides which class to give them.
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IShipmentService, ShipmentService>();

// The system clock, injectable so tests can swap in a fixed date. Singleton = one shared instance.
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

// In development: apply any pending EF migrations and seed demo data on startup, so a fresh clone
// "just works". (In production you'd run `dotnet ef database update` as a deliberate deploy step.)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    SeedData.Initialize(db, TimeProvider.System);
}

// 3) CONFIGURE THE MIDDLEWARE PIPELINE
// Every HTTP request passes through these components in order (like a chain of Java Servlet Filters).
if (!app.Environment.IsDevelopment())
{
    // In production, show a friendly error page instead of a stack trace.
    app.UseExceptionHandler("/Home/Error");
    // HSTS tells browsers to always use HTTPS for this site.
    app.UseHsts();
}

app.UseHttpsRedirection();   // redirect http:// to https://
app.UseRouting();            // work out which controller/action matches the URL

app.UseAuthorization();      // check [Authorize] attributes (used later if we add login)

app.MapStaticAssets();       // serve CSS/JS/images from wwwroot

// 4) CONVENTIONAL ROUTING
// URL /Shipments/Details/5  ->  ShipmentsController.Details(int id = 5)
// URL /                     ->  HomeController.Index()   (the defaults)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// 5) START LISTENING FOR REQUESTS
app.Run();
