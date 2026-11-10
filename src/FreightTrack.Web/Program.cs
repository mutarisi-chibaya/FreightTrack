// Program.cs is the entry point of the app (like `public static void main` in Java).
// C# "top-level statements" let us skip writing the Program class and Main method by hand.

// 1) BUILD THE HOST
// The builder loads configuration (appsettings.json, environment variables), logging,
// and the dependency injection (DI) container. Think of it like Spring Boot's ApplicationContext.
var builder = WebApplication.CreateBuilder(args);

// 2) REGISTER SERVICES IN THE DI CONTAINER
// Anything registered here can be "asked for" in a constructor, and ASP.NET Core supplies it.
// AddControllersWithViews() registers everything MVC needs: controllers, Razor views, model binding, validation.
// In later milestones we'll register AppDbContext and our CustomerService / ShipmentService here too.
builder.Services.AddControllersWithViews();

var app = builder.Build();

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
