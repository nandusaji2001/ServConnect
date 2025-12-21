using AspNetCore.Identity.MongoDbCore.Extensions;
using AspNetCore.Identity.MongoDbCore.Models;
using Microsoft.AspNetCore.Identity;
using ServConnect.Models;
using ServConnect.Services;
using System.Globalization; // Localization
using Microsoft.AspNetCore.Localization; // Localization
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// MongoDB Atlas Configuration
var mongoConnectionString = builder.Configuration["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
var mongoDatabaseName = builder.Configuration["MongoDB:DatabaseName"] ?? "ServConnectDb";

builder.Services.AddIdentity<Users, MongoIdentityRole>()
    .AddMongoDbStores<Users, MongoIdentityRole, Guid>(
        mongoConnectionString,  // MongoDB Atlas connection string
        mongoDatabaseName       // Database name
    )
    .AddDefaultTokenProviders();

// Identity configuration (unchanged)
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireDigit = false;
    options.User.RequireUniqueEmail = true;
});

// Configure cookie settings (enable persistent "Remember Me" cookies)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "ServConnect.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always; // use HTTPS

    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";

    // Make Remember Me work by setting a long-lived auth cookie
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30); // persist up to 30 days when RememberMe is checked
    options.ReturnUrlParameter = "returnUrl";
    
    // Return 401 for API requests instead of redirecting to login page
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = 403;
            return Task.CompletedTask;
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

// Localization services
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Firebase Authentication is handled client-side and verified server-side

// Add CORS services
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowCredentials()
              .SetIsOriginAllowed(origin => true) // Allow any origin in development
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();       // Adds API controllers
builder.Services.AddControllersWithViews(options =>
{
    // Enforce profile completion and admin approval for all authenticated users
    options.Filters.Add<ServConnect.Filters.RequireApprovedUserFilter>();
})
.AddViewLocalization(Microsoft.AspNetCore.Mvc.Razor.LanguageViewLocationExpanderFormat.Suffix)
.AddDataAnnotationsLocalization()
.AddJsonOptions(o =>
{
    o.JsonSerializerOptions.WriteIndented = true;
    o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
}); // Adds MVC + Razor views with global filters
builder.Services.AddScoped<DatabaseSeeder>();

// Filters
builder.Services.AddScoped<ServConnect.Filters.RequireApprovedUserFilter>();
builder.Services.AddScoped<ServConnect.Filters.RequireApprovedUserApiFilter>();
// Register custom services
builder.Services.AddHttpClient<Fast2SmsOtpService>();
builder.Services.AddHttpClient<NewsService>();
builder.Services.AddHttpClient<TranslationService>()
    .ConfigureHttpClient(client => 
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });
builder.Services.AddHttpClient<PexelsImageService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ISmsService, Fast2SmsOtpService>();
builder.Services.AddScoped<INewsService, NewsService>();
builder.Services.AddScoped<ITranslationService, TranslationService>();
builder.Services.AddScoped<IPexelsImageService, PexelsImageService>();
builder.Services.AddScoped<IFirebaseAuthService, FirebaseAuthService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<IAdvertisementService, AdvertisementService>();
builder.Services.AddScoped<IAdvertisementRequestService, AdvertisementRequestService>();
builder.Services.AddScoped<IComplaintService, ComplaintService>();
// New service catalog for service-provider linking
builder.Services.AddScoped<IServiceCatalog, ServiceCatalog>();
builder.Services.AddScoped<IBookingService, BookingService>();
// Service payment service
builder.Services.AddScoped<IServicePaymentService, ServicePaymentService>();
// Booking payment service (requires HttpClientFactory)
builder.Services.AddScoped<IBookingPaymentService, BookingPaymentService>();
// Availability validation service
builder.Services.AddScoped<IAvailabilityValidationService, AvailabilityValidationService>();
// Service OTP service
builder.Services.AddScoped<IServiceOtpService, ServiceOtpService>();
// Service Transfer service
builder.Services.AddScoped<IServiceTransferService, ServiceTransferService>();
// Notification service
builder.Services.AddScoped<INotificationService, NotificationService>();
// Background service for automatic service expiry
builder.Services.AddHostedService<ServConnect.BackgroundServices.ServiceExpiryBackgroundService>();

// Orders & payments
builder.Services.AddScoped<IOrderService, OrderService>();
// Address management
builder.Services.AddScoped<IAddressService, AddressService>();
// Local directory: admin CRUD via Mongo + public discovery via Google Places
builder.Services.AddHttpClient<GooglePlacesService>();
builder.Services.AddScoped<LocalDirectory>();
builder.Services.AddScoped<ILocalDirectory, LocalDirectoryFacade>();
// Recommendations
builder.Services.AddScoped<IRecommendationService, RecommendationService>();
// Ratings
builder.Services.AddScoped<IRatingService, RatingService>();
// Email service
builder.Services.AddScoped<IEmailService, EmailService>();
// Community module service
builder.Services.AddScoped<ICommunityService, CommunityService>();
// Revenue service for analytics and ML predictions
builder.Services.AddScoped<IRevenueService, RevenueService>();
// Rental property service for house rentals module
builder.Services.AddScoped<IRentalPropertyService, RentalPropertyService>();
// Rental subscription service for house rentals subscription management
builder.Services.AddScoped<IRentalSubscriptionService, RentalSubscriptionService>();
// Rental query service for user-owner messaging
builder.Services.AddScoped<IRentalQueryService, RentalQueryService>();
builder.Services.AddMemoryCache();

var app = builder.Build();

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

// Localization middleware configuration
var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("ml-IN") };
var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};
localizationOptions.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Configure static files with GLB MIME type support
var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".glb"] = "model/gltf-binary";
provider.Mappings[".gltf"] = "model/gltf+json";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

app.UseRequestLocalization(localizationOptions);
app.UseRouting();
app.UseCors(); // Add CORS middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();