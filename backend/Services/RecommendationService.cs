using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using ServConnect.Models;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;

namespace ServConnect.Services
{
    public class RecommendationService : IRecommendationService
    {
        private readonly IMongoCollection<Booking> _bookings;
        private readonly string _pythonExe;
        private readonly string _modelPath;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
        private readonly Microsoft.Extensions.Logging.ILogger<RecommendationService> _logger;
        private readonly int _cacheSeconds;
        private readonly string[] _vocabulary;

        public RecommendationService(IConfiguration config, Microsoft.Extensions.Caching.Memory.IMemoryCache cache, Microsoft.Extensions.Logging.ILogger<RecommendationService> logger)
        {
            var conn = config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var dbName = config["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(conn);
            var db = client.GetDatabase(dbName);
            _bookings = db.GetCollection<Booking>("Bookings");

            _pythonExe = config["Recommendations:PythonExe"] ?? "python"; // ensure python in PATH on server
            _modelPath = Path.Combine(AppContext.BaseDirectory, "service_recommendation_model.pkl");
            _cache = cache;
            _logger = logger;
            _cacheSeconds = int.TryParse(config["Recommendations:CacheSeconds"], out var cs) ? cs : 600;
            _vocabulary = config.GetSection("Recommendations:Vocabulary").Get<string[]>() ?? new[]
            {
                "Plumber", "Electrician", "Cleaner", "Painting", "Salon",
                "Carpentry", "Pest Control", "Gardening", "Appliance Repair", "AC Repair",
                "Car Repair"
            };
        }

        public async Task<List<string>> GetTopServicesForUserAsync(Guid userId, int topN = 3)
        {
            try
            {
                var cacheKey = $"recs:{userId}:{topN}";
                if (_cache.TryGetValue(cacheKey, out object? cachedObj) && cachedObj is List<string> cached)
                    return cached;

                // 1) Fetch user's past bookings
                var userBookings = await _bookings.Find(x => x.UserId == userId)
                                                  .SortByDescending(x => x.RequestedAtUtc)
                                                  .Limit(100)
                                                  .ToListAsync();

                // 2) Build one-hot vector based on Vocabulary (normalize via slug)
                static string ToSlug(string name)
                {
                    var s = (name ?? string.Empty).Trim().ToLowerInvariant();
                    var chars = s.Select(ch => char.IsLetterOrDigit(ch) ? ch : (ch == ' ' || ch == '-' ? '-' : '\0'))
                                 .Where(ch => ch != '\0');
                    var slug = string.Join("", chars).Replace("--", "-");
                    while (slug.Contains("--")) slug = slug.Replace("--", "-");
                    return slug.Trim('-');
                }

                var vocabSlugs = _vocabulary.Select(ToSlug).ToArray();
                var oneHot = new int[_vocabulary.Length];
                foreach (var b in userBookings)
                {
                    var slug = ToSlug(b.ServiceName);
                    var idx = Array.FindIndex(vocabSlugs, v => v == slug);
                    if (idx >= 0) oneHot[idx] = 1; // presence of having booked
                }

                // If no history, return empty to allow fallback
                if (oneHot.All(x => x == 0))
                {
                    _logger.LogInformation("Recommendations: user {UserId} has no matching booking history to vocab.", userId);
                    return new List<string>();
                }

                // 3) Call python helper to score with the .pkl
                var helperPath = Path.Combine(AppContext.BaseDirectory, "recommendation_helper.py");
                if (!File.Exists(helperPath) || !File.Exists(_modelPath))
                {
                    _logger.LogWarning("Recommendations: helper or model missing. helper={HelperExists} model={ModelExists}", File.Exists(helperPath), File.Exists(_modelPath));
                    return new List<string>();
                }

                // Prepare input JSON: {"features":[0/1,...], "labels":["Plumber",...]} so Python can align
                var payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    features = oneHot,
                    labels = _vocabulary
                });

                var psi = new ProcessStartInfo
                {
                    FileName = _pythonExe,
                    Arguments = $"\"{helperPath}\"",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = AppContext.BaseDirectory
                };

                using var proc = Process.Start(psi);
                if (proc == null) return new List<string>();

                await proc.StandardInput.WriteLineAsync(payload);
                proc.StandardInput.Close();

                var stdout = await proc.StandardOutput.ReadToEndAsync();
                var stderr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                if (proc.ExitCode != 0)
                {
                    _logger.LogWarning("Recommendations: python exited with code {Code}. stderr={Err}", proc.ExitCode, stderr);
                    return new List<string>();
                }

                // Expecting JSON { "top": ["ServiceA","ServiceB","ServiceC"] }
                var doc = System.Text.Json.JsonDocument.Parse(stdout);
                if (doc.RootElement.TryGetProperty("top", out var topEl) && topEl.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var list = new List<string>();
                    foreach (var it in topEl.EnumerateArray())
                    {
                        var name = it.GetString();
                        if (!string.IsNullOrWhiteSpace(name)) list.Add(name!);
                    }
                    // Cache
                    _cache.Set(cacheKey, list, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_cacheSeconds)
                    });
                    return list;
                }

                return new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recommendations: unexpected error for user {UserId}", userId);
                return new List<string>();
            }
        }
    }
}