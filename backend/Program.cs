using AspNetCore.Identity.MongoDbCore.Extensions;
using AspNetCore.Identity.MongoDbCore.Models;
using Microsoft.AspNetCore.Identity;
using ServConnect.Models;
using ServConnect.Services;

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

// Configure cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Firebase Authentication is handled client-side and verified server-side

builder.Services.AddControllers();       // Adds API controllers
builder.Services.AddControllersWithViews(); // Adds MVC + Razor views
builder.Services.AddScoped<DatabaseSeeder>();

// Register custom services
builder.Services.AddHttpClient<Fast2SmsOtpService>();
builder.Services.AddScoped<ISmsService, Fast2SmsOtpService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IFirebaseAuthService, FirebaseAuthService>();
builder.Services.AddScoped<IItemService, ItemService>();
// New service catalog for service-provider linking
builder.Services.AddScoped<IServiceCatalog, ServiceCatalog>();
// Local directory for public services (hospitals, police, petrol, etc.)
builder.Services.AddScoped<ILocalDirectory, LocalDirectory>();
builder.Services.AddMemoryCache();

var app = builder.Build();

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

// Middleware pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();