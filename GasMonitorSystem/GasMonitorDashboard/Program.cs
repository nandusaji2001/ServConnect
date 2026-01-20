using GasMonitorDashboard.Hubs;
using GasMonitorDashboard.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddSignalR();

// Configure MongoDB (with fallback)
try
{
    builder.Services.Configure<MongoDBSettings>(
        builder.Configuration.GetSection("MongoDB"));
    builder.Services.AddSingleton<MongoDBService>();
    builder.Services.AddSingleton<IWeightReadingService, WeightReadingService>();
}
catch (Exception ex)
{
    // Fallback to in-memory service if MongoDB fails
    Console.WriteLine($"MongoDB configuration failed, using in-memory storage: {ex.Message}");
    builder.Services.AddSingleton<IWeightReadingService, InMemoryWeightReadingService>();
}
builder.Services.AddHostedService<SignalRBackgroundService>();

// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for ESP32 communication
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowESP32", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors("AllowESP32");
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapHub<WeightHub>("/weighthub");

app.Run();