using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using GOLISMart.API.Data;
using GOLISMart.API.Middleware;
using GOLISMart.API.Models;
using GOLISMart.API.Repositories.Implementations;
using GOLISMart.API.Repositories.Interfaces;
using GOLISMart.API.Services.Implementations;
using GOLISMart.API.Services.Interfaces;
using GOLISMart.API.Validators;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Swashbuckle.AspNetCore.Annotations;

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

// ── Data access ─────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();

// ── Repositories ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<ICartRepository, CartRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IDeliveryRepository, DeliveryRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IAdminRepository, AdminRepository>();
builder.Services.AddScoped<IDeliveryManagementRepository, DeliveryManagementRepository>();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IDeliveryService, DeliveryService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IDeliveryManagementService, DeliveryManagementService>();
builder.Services.AddScoped<INotificationProvider, InAppNotificationProvider>();
builder.Services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();

// ── Geocoding ─────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IGeocodingService, NominatimGeocodingService>(client =>
{
    var baseUrl = builder.Configuration["Nominatim:BaseUrl"]
        ?? "https://nominatim.openstreetmap.org/";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    // Nominatim requires a descriptive User-Agent with a valid contact URL or email.
    // Replace the contact URL with your production domain before deploying.
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "GOLISMart/1.0 (https://example.com/contact)");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ── FluentValidation ─────────────────────────────────────────────────────────
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

// ── JWT Authentication ────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DeliveryPersonnel", policy =>
        policy.RequireRole("DeliveryPersonnel"));
});

// ── Rate limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit       = 10;
        limiterOptions.Window            = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit        = 0;
    });

    // Geocode endpoint: 20 requests per minute per remote IP.
    // This protects both Nominatim's 1 req/s policy and the API itself.
    options.AddFixedWindowLimiter("geocode", limiterOptions =>
    {
        limiterOptions.PermitLimit       = 20;
        limiterOptions.Window            = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit        = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── MVC ───────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── OpenAPI / Swagger ────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "GOLIS Mart API",
        Version     = "v1",
        Description = "Catalogue, cart, and customer endpoints for the GOLIS Mart grocery platform."
    });
    options.EnableAnnotations();
});

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClient", policy =>
        policy.WithOrigins("https://localhost:5003", "http://localhost:5002")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "GOLIS Mart API v1"));
}

app.UseHttpsRedirection();
app.UseCors("WebClient");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

// Expose for test project integration
public partial class Program { }
