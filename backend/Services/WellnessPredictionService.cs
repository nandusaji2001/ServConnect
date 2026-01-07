using System.Text;
using System.Text.Json;
using ServConnect.ViewModels;

namespace ServConnect.Services
{
    public interface IWellnessPredictionService
    {
        Task<WellnessPredictionResult> GetPredictionAsync(ElderHealthDetailsViewModel healthData, int age, string gender);
    }

    public class WellnessPredictionService : IWellnessPredictionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WellnessPredictionService> _logger;

        public WellnessPredictionService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<WellnessPredictionService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<WellnessPredictionResult> GetPredictionAsync(ElderHealthDetailsViewModel healthData, int age, string gender)
        {
            try
            {
                var apiUrl = _configuration["WellnessApi:BaseUrl"] ?? "http://localhost:5002";
                
                var requestData = new
                {
                    age = age,
                    gender = gender,
                    bmi = healthData.BMI,
                    systolic_bp = healthData.SystolicBP,
                    diastolic_bp = healthData.DiastolicBP,
                    cholesterol = healthData.Cholesterol,
                    triglycerides = healthData.Triglycerides,
                    family_history_t2d = healthData.FamilyHistoryT2D ? 1 : 0,
                    family_history_cvd = healthData.FamilyHistoryCVD ? 1 : 0,
                    sleep_hours = healthData.SleepHours,
                    sleep_quality = healthData.SleepQuality,
                    stress_level = healthData.StressLevel,
                    physical_activity_level = healthData.PhysicalActivityLevel,
                    diet_preference = healthData.DietPreference,
                    food_allergies = healthData.FoodAllergies ?? ""
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation($"Calling wellness API at {apiUrl}/predict");
                
                var response = await _httpClient.PostAsync($"{apiUrl}/predict", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Wellness API error: {responseContent}");
                    return new WellnessPredictionResult
                    {
                        Success = false,
                        Error = "Failed to get predictions from wellness API"
                    };
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                var result = new WellnessPredictionResult
                {
                    Success = root.GetProperty("success").GetBoolean()
                };

                if (result.Success)
                {
                    var predictions = root.GetProperty("predictions");
                    result.DietRecommendation = predictions.GetProperty("diet_recommendation").GetString() ?? "";
                    result.DietPlan = predictions.GetProperty("diet_plan").GetString() ?? "";
                    result.HeartRisk = predictions.GetProperty("heart_risk").GetString() ?? "";

                    // Parse detailed diet plan
                    if (root.TryGetProperty("detailed_diet_plan", out var dietPlan))
                    {
                        result.DetailedDiet = new DetailedDietPlan
                        {
                            Title = dietPlan.GetProperty("title").GetString() ?? "",
                            Description = dietPlan.GetProperty("description").GetString() ?? "",
                            Breakfast = ParseStringArray(dietPlan, "breakfast"),
                            Lunch = ParseStringArray(dietPlan, "lunch"),
                            Dinner = ParseStringArray(dietPlan, "dinner"),
                            Snacks = ParseStringArray(dietPlan, "snacks")
                        };
                    }

                    // Parse heart risk details
                    if (root.TryGetProperty("heart_risk_details", out var heartDetails))
                    {
                        result.HeartDetails = new HeartRiskDetails
                        {
                            RiskDescription = heartDetails.GetProperty("risk_description").GetString() ?? "",
                            DietaryTips = ParseStringArray(heartDetails, "dietary_tips"),
                            LifestyleTips = ParseStringArray(heartDetails, "lifestyle_tips"),
                            Warnings = ParseStringArray(heartDetails, "warnings"),
                            Exercises = ParseExercises(heartDetails)
                        };
                    }

                    // Parse input summary
                    if (root.TryGetProperty("input_summary", out var summary))
                    {
                        result.Summary = new InputSummary
                        {
                            Age = summary.TryGetProperty("age", out var ageVal) ? ageVal.GetInt32() : null,
                            BMI = summary.TryGetProperty("bmi", out var bmiVal) ? bmiVal.GetDouble() : null,
                            BloodPressure = summary.TryGetProperty("blood_pressure", out var bpVal) ? bpVal.GetString() : null,
                            Cholesterol = summary.TryGetProperty("cholesterol", out var cholVal) ? cholVal.GetDouble() : null,
                            PhysicalActivity = summary.TryGetProperty("physical_activity", out var paVal) ? paVal.GetString() : null
                        };
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling wellness prediction API");
                return new WellnessPredictionResult
                {
                    Success = false,
                    Error = $"Error: {ex.Message}"
                };
            }
        }

        private List<string> ParseStringArray(JsonElement element, string propertyName)
        {
            var list = new List<string>();
            if (element.TryGetProperty(propertyName, out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in array.EnumerateArray())
                {
                    var str = item.GetString();
                    if (!string.IsNullOrEmpty(str))
                        list.Add(str);
                }
            }
            return list;
        }

        private List<ExerciseRecommendation> ParseExercises(JsonElement element)
        {
            var exercises = new List<ExerciseRecommendation>();
            if (element.TryGetProperty("exercises", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in array.EnumerateArray())
                {
                    exercises.Add(new ExerciseRecommendation
                    {
                        Name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        Duration = item.TryGetProperty("duration", out var d) ? d.GetString() ?? "" : "",
                        Frequency = item.TryGetProperty("frequency", out var f) ? f.GetString() ?? "" : "",
                        Description = item.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : ""
                    });
                }
            }
            return exercises;
        }
    }
}
