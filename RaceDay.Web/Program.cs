using System.Globalization;
using RaceDay.Web.Services;

// Decimal numbers in forms (distance, fees) always use "." so they match what browsers post,
// whatever the regional settings of the machine or container are.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Session management: the signed-in user's token and role live in server-side session state,
// identified by a secure cookie. The MVC app has no database; every operation goes through the API.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
                            
{
    o.IdleTimeout = TimeSpan.FromMinutes(60);
    o.Cookie.Name = ".RaceDay.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddScoped<UserSession>();

var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Missing Api:BaseUrl configuration.");
builder.Services.AddHttpClient<ApiClient>(c =>
{
    c.BaseAddress = new Uri(apiBaseUrl);
    c.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Friendly error pages instead of raw exception text
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/HttpStatus", "?code={0}");

app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(name: "event", pattern: "events/{id:int}",
    defaults: new { controller = "Home", action = "Details" });
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
