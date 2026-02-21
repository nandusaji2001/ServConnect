using System.Text;
using System.Text.Json;
using ServConnect.Models;

namespace ServConnect.Services
{
    public interface IDepressionPredictionService
    {
        Task<DepressionPredictionResult> GetPredictionAsync(DepressionAssessmentRequest request);
        Task<List<SuggestedPlace>> GetNearbyPlacesAsync(double latitude, double longitude, string placeType);
    }

    public class DepressionPredictionService : IDepressionPredictionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DepressionPredictionService> _logger;

        public DepressionPredictionService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<DepressionPredictionService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<DepressionPredictionResult> GetPredictionAsync(DepressionAssessmentRequest request)
        {
            try
            {
                var apiUrl = _configuration["DepressionApi:BaseUrl"] ?? "http://localhost:5007";
                
                var requestData = new Dictionary<string, object>
                {
                    { "Gender", request.Gender },
                    { "Age", request.Age },
                    { "Sleep Duration", request.SleepDuration },
                    { "Dietary Habits", request.DietaryHabits },
                    { "Work/Study Hours", request.WorkStudyHours },
                    { "Financial Stress", request.FinancialStress },
                    { "Family History of Mental Illness", request.FamilyHistoryMentalIllness ? "Yes" : "No" },
                    { "Have you ever had suicidal thoughts ?", request.SuicidalThoughts ? "Yes" : "No" },
                    { "isStudent", request.IsStudent }
                };

                // Add student-specific fields
                if (request.IsStudent)
                {
                    requestData["Academic Pressure"] = request.AcademicPressure ?? 3;
                    requestData["CGPA"] = request.CGPA ?? 7.0;
                    requestData["Study Satisfaction"] = request.StudySatisfaction ?? 3;
                }
                else
                {
                    requestData["Work Pressure"] = request.WorkPressure ?? 3;
                    requestData["Job Satisfaction"] = request.JobSatisfaction ?? 3;
                }

                // Add location if available
                if (request.Latitude.HasValue && request.Longitude.HasValue)
                {
                    requestData["location"] = new
                    {
                        latitude = request.Latitude.Value,
                        longitude = request.Longitude.Value
                    };
                }

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation($"Calling depression API at {apiUrl}/predict");
                
                var response = await _httpClient.PostAsync($"{apiUrl}/predict", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Depression API error: {responseContent}");
                    return new DepressionPredictionResult
                    {
                        Success = false,
                        Error = "Failed to get predictions from depression API"
                    };
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                var result = new DepressionPredictionResult
                {
                    Success = root.GetProperty("success").GetBoolean()
                };

                if (result.Success)
                {
                    var prediction = root.GetProperty("prediction");
                    result.IsDepressed = prediction.GetProperty("isDepressed").GetBoolean();
                    result.DepressionProbability = prediction.GetProperty("depressionProbability").GetDouble();
                    result.Confidence = prediction.GetProperty("confidence").GetDouble();
                    result.SeverityLevel = prediction.GetProperty("severityLevel").GetString() ?? "low";
                    result.Message = root.GetProperty("message").GetString() ?? "";

                    // Parse wellness plan if user is depressed
                    if (result.IsDepressed && root.TryGetProperty("wellnessPlan", out var wellnessPlan))
                    {
                        result.WellnessPlan = ParseWellnessPlan(wellnessPlan);
                    }

                    // Parse tips for non-depressed users
                    if (!result.IsDepressed && root.TryGetProperty("tips", out var tips))
                    {
                        result.Tips = new List<string>();
                        foreach (var tip in tips.EnumerateArray())
                        {
                            result.Tips.Add(tip.GetString() ?? "");
                        }
                    }
                }
                else if (root.TryGetProperty("error", out var error))
                {
                    result.Error = error.GetString();
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling depression prediction API");
                return new DepressionPredictionResult
                {
                    Success = false,
                    Error = $"Service error: {ex.Message}"
                };
            }
        }

        private WellnessPlanData ParseWellnessPlan(JsonElement wellnessPlan)
        {
            var plan = new WellnessPlanData
            {
                StartDate = wellnessPlan.GetProperty("startDate").GetString() ?? "",
                EndDate = wellnessPlan.GetProperty("endDate").GetString() ?? "",
                TotalTasks = wellnessPlan.GetProperty("totalTasks").GetInt32(),
                Recommendations = new List<string>(),
                Days = new List<WellnessDayData>()
            };

            // Parse recommendations
            if (wellnessPlan.TryGetProperty("recommendations", out var recs))
            {
                foreach (var rec in recs.EnumerateArray())
                {
                    plan.Recommendations.Add(rec.GetString() ?? "");
                }
            }

            // Parse days
            if (wellnessPlan.TryGetProperty("days", out var days))
            {
                foreach (var day in days.EnumerateArray())
                {
                    var dayData = new WellnessDayData
                    {
                        Day = day.GetProperty("day").GetInt32(),
                        Date = day.GetProperty("date").GetString() ?? "",
                        DayName = day.GetProperty("dayName").GetString() ?? "",
                        Affirmation = day.GetProperty("affirmation").GetString() ?? "",
                        Tasks = new List<WellnessTaskData>()
                    };

                    if (day.TryGetProperty("tasks", out var tasks))
                    {
                        foreach (var task in tasks.EnumerateArray())
                        {
                            var taskData = new WellnessTaskData
                            {
                                Id = task.GetProperty("id").GetString() ?? "",
                                Day = task.GetProperty("day").GetInt32(),
                                Date = task.GetProperty("date").GetString() ?? "",
                                DayName = task.GetProperty("dayName").GetString() ?? "",
                                Category = task.GetProperty("category").GetString() ?? "",
                                CategoryName = task.GetProperty("categoryName").GetString() ?? "",
                                Icon = task.GetProperty("icon").GetString() ?? "",
                                Color = task.GetProperty("color").GetString() ?? "",
                                Title = task.GetProperty("title").GetString() ?? "",
                                Description = task.GetProperty("description").GetString() ?? "",
                                Duration = task.GetProperty("duration").GetString() ?? "",
                                RequiresLocation = task.GetProperty("requiresLocation").GetBoolean(),
                                IsCompleted = task.GetProperty("isCompleted").GetBoolean()
                            };

                            if (task.TryGetProperty("placeType", out var placeType) && placeType.ValueKind != JsonValueKind.Null)
                            {
                                taskData.PlaceType = placeType.GetString();
                            }

                            dayData.Tasks.Add(taskData);
                        }
                    }

                    plan.Days.Add(dayData);
                }
            }

            return plan;
        }

        public async Task<List<SuggestedPlace>> GetNearbyPlacesAsync(double latitude, double longitude, string placeType)
        {
            try
            {
                var apiKey = _configuration["GoogleMaps:ApiKey"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("Google Maps API key not configured");
                    return new List<SuggestedPlace>();
                }

                var radius = 10000; // 10km radius
                var allPlaces = new List<SuggestedPlace>();

                // Handle religious_place by fetching all three types
                if (placeType == "religious_place")
                {
                    var religiousTypes = new[] { "hindu_temple", "church", "mosque" };
                    foreach (var religType in religiousTypes)
                    {
                        var religiousPlaces = await FetchPlacesFromApi(latitude, longitude, religType, radius, apiKey);
                        allPlaces.AddRange(religiousPlaces);
                    }
                    // Sort by rating and return top results
                    return allPlaces.OrderByDescending(p => p.Rating ?? 0).Take(10).ToList();
                }
                else
                {
                    return await FetchPlacesFromApi(latitude, longitude, placeType, radius, apiKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching nearby places");
                return new List<SuggestedPlace>();
            }
        }

        private async Task<List<SuggestedPlace>> FetchPlacesFromApi(double latitude, double longitude, string placeType, int radius, string apiKey)
        {
            try
            {
                var url = $"https://maps.googleapis.com/maps/api/place/nearbysearch/json?location={latitude},{longitude}&radius={radius}&type={placeType}&key={apiKey}";

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Google Places API error: {content}");
                    return new List<SuggestedPlace>();
                }

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var places = new List<SuggestedPlace>();

                if (root.TryGetProperty("results", out var results))
                {
                    foreach (var result in results.EnumerateArray().Take(5))
                    {
                        var place = new SuggestedPlace
                        {
                            PlaceId = result.TryGetProperty("place_id", out var pid) ? pid.GetString() ?? "" : "",
                            Name = result.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                            Address = result.TryGetProperty("vicinity", out var addr) ? addr.GetString() ?? "" : ""
                        };

                        if (result.TryGetProperty("geometry", out var geo) && geo.TryGetProperty("location", out var loc))
                        {
                            place.Latitude = loc.TryGetProperty("lat", out var lat) ? lat.GetDouble() : 0;
                            place.Longitude = loc.TryGetProperty("lng", out var lng) ? lng.GetDouble() : 0;
                        }

                        if (result.TryGetProperty("rating", out var rating))
                        {
                            place.Rating = rating.GetDouble();
                        }

                        if (result.TryGetProperty("opening_hours", out var hours) && hours.TryGetProperty("open_now", out var open))
                        {
                            place.IsOpen = open.GetBoolean();
                        }

                        if (result.TryGetProperty("photos", out var photos) && photos.GetArrayLength() > 0)
                        {
                            var photo = photos[0];
                            if (photo.TryGetProperty("photo_reference", out var photoRef))
                            {
                                place.PhotoUrl = $"https://maps.googleapis.com/maps/api/place/photo?maxwidth=400&photo_reference={photoRef.GetString()}&key={apiKey}";
                            }
                        }

                        if (result.TryGetProperty("types", out var types))
                        {
                            place.PlaceTypes = new List<string>();
                            foreach (var type in types.EnumerateArray())
                            {
                                place.PlaceTypes.Add(type.GetString() ?? "");
                            }
                        }

                        // Calculate distance
                        var distance = CalculateDistance(latitude, longitude, place.Latitude, place.Longitude);
                        place.Distance = distance < 1 ? $"{(int)(distance * 1000)}m" : $"{distance:F1}km";

                        places.Add(place);
                    }
                }

                return places;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching nearby places");
                return new List<SuggestedPlace>();
            }
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Earth's radius in km
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double degrees) => degrees * Math.PI / 180;
    }

    // Request/Response DTOs
    public class DepressionAssessmentRequest
    {
        public string Gender { get; set; } = string.Empty;
        public int Age { get; set; }
        public bool IsStudent { get; set; }
        public string SleepDuration { get; set; } = string.Empty;
        public string DietaryHabits { get; set; } = string.Empty;
        public int WorkStudyHours { get; set; }
        public int FinancialStress { get; set; }
        public bool FamilyHistoryMentalIllness { get; set; }
        public bool SuicidalThoughts { get; set; }
        
        // Student-specific
        public int? AcademicPressure { get; set; }
        public double? CGPA { get; set; }
        public int? StudySatisfaction { get; set; }
        
        // Working Professional-specific
        public int? WorkPressure { get; set; }
        public int? JobSatisfaction { get; set; }
        
        // Location
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? City { get; set; }
    }

    public class DepressionPredictionResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public bool IsDepressed { get; set; }
        public double DepressionProbability { get; set; }
        public double Confidence { get; set; }
        public string SeverityLevel { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public WellnessPlanData? WellnessPlan { get; set; }
        public List<string>? Tips { get; set; }
    }

    public class WellnessPlanData
    {
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public List<WellnessDayData> Days { get; set; } = new();
    }

    public class WellnessDayData
    {
        public int Day { get; set; }
        public string Date { get; set; } = string.Empty;
        public string DayName { get; set; } = string.Empty;
        public string Affirmation { get; set; } = string.Empty;
        public List<WellnessTaskData> Tasks { get; set; } = new();
    }

    public class WellnessTaskData
    {
        public string Id { get; set; } = string.Empty;
        public int Day { get; set; }
        public string Date { get; set; } = string.Empty;
        public string DayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string? PlaceType { get; set; }
        public bool RequiresLocation { get; set; }
        public bool IsCompleted { get; set; }
    }
}
