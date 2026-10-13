using GOLISMart.Web.Services.Implementations;
using GOLISMart.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery();

// ── Session ───────────────────────────────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(24);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// ── Cookie authentication ─────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath        = "/account/login";
        options.LogoutPath       = "/account/logout";
        options.AccessDeniedPath = "/account/login";
        options.Cookie.HttpOnly  = true;
        options.Cookie.SameSite  = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.Name      = "GOLISMart_Auth";
        options.ExpireTimeSpan   = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("DeliveryPersonnel", policy =>
        policy.RequireRole("DeliveryPersonnel"));
});

// ── Typed HTTP clients for the GOLIS Mart API ────────────────────────────────
var configureApiClient = (HttpClient client) =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"]
        ?? throw new InvalidOperationException("ApiSettings:BaseUrl is not configured.");
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
};

builder.Services.AddHttpClient<ICatalogApiClient, CatalogApiClient>(configureApiClient);
builder.Services.AddHttpClient<ICartApiClient, CartApiClient>(configureApiClient);
builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>(configureApiClient);
builder.Services.AddHttpClient<IDeliveryApiClient, DeliveryApiClient>(configureApiClient);
builder.Services.AddHttpClient<ICheckoutApiClient, CheckoutApiClient>(configureApiClient);
builder.Services.AddHttpClient<ILocationApiClient, LocationApiClient>(configureApiClient);
builder.Services.AddHttpClient<IOrderApiClient, OrderApiClient>(configureApiClient);
builder.Services.AddHttpClient<IAdminApiClient, AdminApiClient>(configureApiClient);
builder.Services.AddHttpClient<IDeliveryManagementApiClient, DeliveryManagementApiClient>(configureApiClient);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Expose for test project integration
public partial class Program { }
