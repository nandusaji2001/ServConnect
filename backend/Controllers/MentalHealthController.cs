using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [Authorize(Roles = RoleTypes.User)]
    public class MentalHealthController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly IMongoCollection<DepressionAssessment> _assessmentCollection;
        private readonly IMongoCollection<WellnessPlan> _wellnessPlanCollection;
        private readonly IMongoCollection<MoodEntry> _moodEntryCollection;
        private readonly IMongoCollection<RewardRedemption> _rewardRedemptionCollection;
        private readonly IDepressionPredictionService _predictionService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<MentalHealthController> _logger;
        private static readonly List<RewardItem> _rewardCatalog = new()
        {
            new RewardItem { Name = "Coffee Mug", StarsRequired = 1000, ImageUrl = "https://static.vecteezy.com/system/resources/thumbnails/029/942/867/small_2x/white-blank-coffee-mug-mockup-closeup-of-mug-in-sunshine-on-light-celebration-background-perfect-for-businesses-selling-mugs-overlay-quote-or-design-generative-ai-photo.jpg", Description = "A beautiful white coffee mug to brighten your mornings" },
            new RewardItem { Name = "Indoor Plant", StarsRequired = 2000, ImageUrl = "https://www.thespruce.com/thmb/iNPuZWHq08PmiVXduzMcO2m1BmQ=/1500x0/filters:no_upscale():max_bytes(150000):strip_icc()/grow-jade-plants-indoors-1902981-01-8f7707a296d3481585008307e61f5655.jpg", Description = "A lovely jade plant to bring nature into your space" },
            new RewardItem { Name = "Atomic Habits by James Clear", StarsRequired = 3500, ImageUrl = "https://jamesclear.com/wp-content/uploads/2019/04/atomicHC-flat-1-e1556572683325.jpg", Description = "Transform your life with tiny changes that deliver remarkable results" }
        };

        public MentalHealthController(
            UserManager<Users> userManager,
            IConfiguration configuration,
            IDepressionPredictionService predictionService,
            INotificationService notificationService,
            ILogger<MentalHealthController> logger)
        {
            _userManager = userManager;
            _predictionService = predictionService;
            _notificationService = notificationService;
            _logger = logger;

            var connectionString = configuration["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var databaseName = configuration["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _assessmentCollection = database.GetCollection<DepressionAssessment>("DepressionAssessments");
            _wellnessPlanCollection = database.GetCollection<WellnessPlan>("WellnessPlans");
            _moodEntryCollection = database.GetCollection<MoodEntry>("MoodEntries");
            _rewardRedemptionCollection = database.GetCollection<RewardRedemption>("RewardRedemptions");
        }

        /// <summary>
        /// Main Mental Health page - shows dashboard or assessment option
        /// </summary>
        [HttpGet]
        [Route("MentalHealth")]
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Check for active wellness plan
            var activePlan = await _wellnessPlanCollection
                .Find(w => w.UserId == currentUser.Id.ToString() && !w.IsCompleted && w.EndDate >= DateTime.UtcNow)
                .SortByDescending(w => w.CreatedAt)
                .FirstOrDefaultAsync();

            if (activePlan != null)
            {
                // Update progress
                activePlan.CompletedTasks = activePlan.Days.SelectMany(d => d.Tasks).Count(t => t.IsCompleted);
                activePlan.ProgressPercentage = activePlan.TotalTasks > 0 
                    ? Math.Round((double)activePlan.CompletedTasks / activePlan.TotalTasks * 100, 1) 
                    : 0;
                
                ViewBag.ActivePlan = activePlan;
            }

            // Get recent assessments
            var recentAssessments = await _assessmentCollection
                .Find(a => a.UserId == currentUser.Id.ToString())
                .SortByDescending(a => a.AssessmentDate)
                .Limit(5)
                .ToListAsync();

            ViewBag.RecentAssessments = recentAssessments;

            // Get recent mood entries
            var recentMoods = await _moodEntryCollection
                .Find(m => m.UserId == currentUser.Id.ToString())
                .SortByDescending(m => m.EntryDate)
                .Limit(7)
                .ToListAsync();

            ViewBag.RecentMoods = recentMoods;
            ViewBag.UserName = currentUser.FullName;

            return View();
        }

        /// <summary>
        /// Assessment form page
        /// </summary>
        [HttpGet]
        [Route("MentalHealth/Assessment")]
        public async Task<IActionResult> Assessment()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.UserName = currentUser.FullName;
            ViewBag.UserAge = 25; // Default age - user will select actual age in form
            ViewBag.UserGender = "Male"; // Default gender - user will select actual gender in form

            return View();
        }

        /// <summary>
        /// Submit assessment and get prediction
        /// </summary>
        [HttpPost]
        [Route("MentalHealth/Assessment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAssessment([FromForm] DepressionAssessmentRequest request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                // Get prediction from ML API
                var predictionResult = await _predictionService.GetPredictionAsync(request);

                if (!predictionResult.Success)
                {
                    return Json(new { success = false, message = predictionResult.Error ?? "Prediction failed" });
                }

                // Save assessment
                var assessment = new DepressionAssessment
                {
                    UserId = currentUser.Id.ToString(),
                    UserName = currentUser.FullName,
                    Gender = request.Gender,
                    Age = request.Age,
                    IsStudent = request.IsStudent,
                    SleepDuration = request.SleepDuration,
                    DietaryHabits = request.DietaryHabits,
                    WorkStudyHours = request.WorkStudyHours,
                    FinancialStress = request.FinancialStress,
                    FamilyHistoryMentalIllness = request.FamilyHistoryMentalIllness,
                    SuicidalThoughts = request.SuicidalThoughts,
                    AcademicPressure = request.AcademicPressure,
                    CGPA = request.CGPA,
                    StudySatisfaction = request.StudySatisfaction,
                    WorkPressure = request.WorkPressure,
                    JobSatisfaction = request.JobSatisfaction,
                    Latitude = request.Latitude,
                    Longitude = request.Longitude,
                    City = request.City,
                    IsDepressed = predictionResult.IsDepressed,
                    DepressionProbability = predictionResult.DepressionProbability,
                    SeverityLevel = predictionResult.SeverityLevel,
                    Confidence = predictionResult.Confidence
                };

                await _assessmentCollection.InsertOneAsync(assessment);

                // Deactivate any previous active plans (regardless of result)
                var deactivateAllFilter = Builders<WellnessPlan>.Filter.And(
                    Builders<WellnessPlan>.Filter.Eq(w => w.UserId, currentUser.Id.ToString()),
                    Builders<WellnessPlan>.Filter.Eq(w => w.IsCompleted, false)
                );
                var deactivateAllUpdate = Builders<WellnessPlan>.Update.Set(w => w.IsCompleted, true);
                await _wellnessPlanCollection.UpdateManyAsync(deactivateAllFilter, deactivateAllUpdate);

                // Create wellness plan (for both depressed and non-depressed users)
                string? wellnessPlanId = null;
                if (predictionResult.WellnessPlan != null)
                {
                    var wellnessPlan = new WellnessPlan
                    {
                        UserId = currentUser.Id.ToString(),
                        AssessmentId = assessment.Id,
                        StartDate = DateTime.Parse(predictionResult.WellnessPlan.StartDate),
                        EndDate = DateTime.Parse(predictionResult.WellnessPlan.EndDate),
                        TotalTasks = predictionResult.WellnessPlan.TotalTasks,
                        CompletedTasks = 0,
                        ProgressPercentage = 0,
                        IsCompleted = false,
                        PlanType = predictionResult.IsDepressed ? "recovery" : "maintenance",
                        Recommendations = predictionResult.WellnessPlan.Recommendations,
                        Days = predictionResult.WellnessPlan.Days.Select(d => new WellnessDay
                        {
                            Day = d.Day,
                            Date = d.Date,
                            DayName = d.DayName,
                            Affirmation = d.Affirmation,
                            IsCompleted = false,
                            Tasks = d.Tasks.Select(t => new WellnessTask
                            {
                                Id = t.Id,
                                Day = t.Day,
                                Date = t.Date,
                                DayName = t.DayName,
                                Category = t.Category,
                                CategoryName = t.CategoryName,
                                Icon = t.Icon,
                                Color = t.Color,
                                Title = t.Title,
                                Description = t.Description,
                                Duration = t.Duration,
                                PlaceType = t.PlaceType,
                                RequiresLocation = t.RequiresLocation,
                                IsCompleted = false
                            }).ToList()
                        }).ToList()
                    };

                    await _wellnessPlanCollection.InsertOneAsync(wellnessPlan);
                    wellnessPlanId = wellnessPlan.Id;

                    // Update assessment with wellness plan ID
                    var update = Builders<DepressionAssessment>.Update.Set(a => a.WellnessPlanId, wellnessPlanId);
                    await _assessmentCollection.UpdateOneAsync(a => a.Id == assessment.Id, update);

                    // Reactivate the new plan (since we deactivated all earlier)
                    var reactivateUpdate = Builders<WellnessPlan>.Update.Set(w => w.IsCompleted, false);
                    await _wellnessPlanCollection.UpdateOneAsync(w => w.Id == wellnessPlanId, reactivateUpdate);

                    // Send notification
                    var notificationTitle = predictionResult.IsDepressed 
                        ? "Mental Wellness Plan Created" 
                        : "Wellness Maintenance Plan Created";
                    var notificationMessage = predictionResult.IsDepressed
                        ? "Your personalized 7-day wellness plan is ready! Start your journey to better mental health today."
                        : "Great news! Your 7-day wellness maintenance plan is ready to help you stay healthy.";
                    
                    await _notificationService.CreateNotificationAsync(
                        currentUser.Id.ToString(),
                        notificationTitle,
                        notificationMessage,
                        NotificationType.General,
                        null,
                        "/MentalHealth/WellnessPlan"
                    );
                }

                return Json(new
                {
                    success = true,
                    isDepressed = predictionResult.IsDepressed,
                    depressionProbability = predictionResult.DepressionProbability,
                    severityLevel = predictionResult.SeverityLevel,
                    confidence = predictionResult.Confidence,
                    message = predictionResult.Message,
                    wellnessPlanId = wellnessPlanId,
                    tips = predictionResult.Tips,
                    assessmentId = assessment.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting assessment");
                return Json(new { success = false, message = "An error occurred while processing your assessment" });
            }
        }

        /// <summary>
        /// Wellness Plan dashboard
        /// </summary>
        [HttpGet]
        [Route("MentalHealth/WellnessPlan")]
        public async Task<IActionResult> WellnessPlan(string? id = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            WellnessPlan? plan;
            if (!string.IsNullOrEmpty(id))
            {
                plan = await _wellnessPlanCollection
                    .Find(w => w.Id == id && w.UserId == currentUser.Id.ToString())
                    .FirstOrDefaultAsync();
            }
            else
            {
                plan = await _wellnessPlanCollection
                    .Find(w => w.UserId == currentUser.Id.ToString() && !w.IsCompleted)
                    .SortByDescending(w => w.CreatedAt)
                    .FirstOrDefaultAsync();
            }

            if (plan == null)
            {
                TempData["InfoMessage"] = "No active wellness plan found. Please take an assessment first.";
                return RedirectToAction("Assessment");
            }

            // Calculate current day using IST (UTC+5:30)
            var istOffset = TimeSpan.FromHours(5.5);
            var istNow = DateTime.UtcNow.Add(istOffset).Date;
            var currentDay = (istNow - plan.StartDate.Date).Days + 1;
            ViewBag.CurrentDay = Math.Min(Math.Max(currentDay, 1), 7);

            // Update progress
            plan.CompletedTasks = plan.Days.SelectMany(d => d.Tasks).Count(t => t.IsCompleted);
            plan.ProgressPercentage = plan.TotalTasks > 0 
                ? Math.Round((double)plan.CompletedTasks / plan.TotalTasks * 100, 1) 
                : 0;

            ViewBag.UserName = currentUser.FullName;
            ViewBag.GoogleMapsApiKey = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["GoogleMaps:ApiKey"];

            return View(plan);
        }

        /// <summary>
        /// Complete a task
        /// </summary>
        [HttpPost]
        [Route("MentalHealth/CompleteTask")]
        public async Task<IActionResult> CompleteTask([FromBody] CompleteTaskRequest request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                var plan = await _wellnessPlanCollection
                    .Find(w => w.Id == request.PlanId && w.UserId == currentUser.Id.ToString())
                    .FirstOrDefaultAsync();

                if (plan == null)
                {
                    return Json(new { success = false, message = "Wellness plan not found" });
                }

                // Find and update the task
                var taskFound = false;
                var starsEarned = 0;
                foreach (var day in plan.Days)
                {
                    var task = day.Tasks.FirstOrDefault(t => t.Id == request.TaskId);
                    if (task != null)
                    {
                        task.IsCompleted = true;
                        task.CompletedAt = DateTime.UtcNow;
                        task.UserNotes = request.Notes;
                        task.Rating = request.Rating;
                        starsEarned = task.StarsAwarded;
                        taskFound = true;
                        break;
                    }
                }

                if (!taskFound)
                {
                    return Json(new { success = false, message = "Task not found" });
                }

                // Update day completion status
                foreach (var day in plan.Days)
                {
                    day.IsCompleted = day.Tasks.All(t => t.IsCompleted);
                }

                // Update plan progress
                plan.CompletedTasks = plan.Days.SelectMany(d => d.Tasks).Count(t => t.IsCompleted);
                plan.TotalStarsEarned = plan.Days.SelectMany(d => d.Tasks).Where(t => t.IsCompleted).Sum(t => t.StarsAwarded);
                plan.ProgressPercentage = plan.TotalTasks > 0 
                    ? Math.Round((double)plan.CompletedTasks / plan.TotalTasks * 100, 1) 
                    : 0;
                plan.UpdatedAt = DateTime.UtcNow;

                // Check if plan is completed
                if (plan.CompletedTasks == plan.TotalTasks)
                {
                    plan.IsCompleted = true;
                    await _notificationService.CreateNotificationAsync(
                        currentUser.Id.ToString(),
                        "Wellness Plan Completed! 🎉",
                        "Congratulations! You've completed your 7-day wellness journey. We're proud of you!",
                        NotificationType.General,
                        null,
                        "/MentalHealth"
                    );
                }

                // Save updates
                await _wellnessPlanCollection.ReplaceOneAsync(w => w.Id == plan.Id, plan);

                return Json(new
                {
                    success = true,
                    completedTasks = plan.CompletedTasks,
                    totalTasks = plan.TotalTasks,
                    progressPercentage = plan.ProgressPercentage,
                    isCompleted = plan.IsCompleted,
                    starsEarned = starsEarned,
                    totalStars = plan.TotalStarsEarned
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing task");
                return Json(new { success = false, message = "An error occurred" });
            }
        }

        /// <summary>
        /// Get nearby places for a place-visit task
        /// </summary>
        [HttpGet]
        [Route("MentalHealth/GetNearbyPlaces")]
        public async Task<IActionResult> GetNearbyPlaces(double latitude, double longitude, string placeType)
        {
            try
            {
                var places = await _predictionService.GetNearbyPlacesAsync(latitude, longitude, placeType);
                return Json(new { success = true, places });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching nearby places");
                return Json(new { success = false, message = "Failed to fetch nearby places" });
            }
        }

        /// <summary>
        /// Save place suggestion for a task
        /// </summary>
        [HttpPost]
        [Route("MentalHealth/SavePlaceSuggestion")]
        public async Task<IActionResult> SavePlaceSuggestion([FromBody] SavePlaceRequest request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                var plan = await _wellnessPlanCollection
                    .Find(w => w.Id == request.PlanId && w.UserId == currentUser.Id.ToString())
                    .FirstOrDefaultAsync();

                if (plan == null)
                {
                    return Json(new { success = false, message = "Wellness plan not found" });
                }

                foreach (var day in plan.Days)
                {
                    var task = day.Tasks.FirstOrDefault(t => t.Id == request.TaskId);
                    if (task != null)
                    {
                        task.SuggestedPlace = request.Place;
                        break;
                    }
                }

                plan.UpdatedAt = DateTime.UtcNow;
                await _wellnessPlanCollection.ReplaceOneAsync(w => w.Id == plan.Id, plan);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving place suggestion");
                return Json(new { success = false, message = "An error occurred" });
            }
        }

        /// <summary>
        /// Log mood entry
        /// </summary>
        [HttpPost]
        [Route("MentalHealth/LogMood")]
        public async Task<IActionResult> LogMood([FromBody] LogMoodRequest request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                var moodLabels = new[] { "Very Bad", "Bad", "Okay", "Good", "Great" };
                var moodEntry = new MoodEntry
                {
                    UserId = currentUser.Id.ToString(),
                    WellnessPlanId = request.WellnessPlanId,
                    Mood = request.Mood,
                    MoodLabel = moodLabels[Math.Clamp(request.Mood - 1, 0, 4)],
                    Notes = request.Notes,
                    Activities = request.Activities
                };

                await _moodEntryCollection.InsertOneAsync(moodEntry);

                return Json(new { success = true, moodEntry });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging mood");
                return Json(new { success = false, message = "An error occurred" });
            }
        }

        /// <summary>
        /// Get mood history
        /// </summary>
        [HttpGet]
        [Route("MentalHealth/MoodHistory")]
        public async Task<IActionResult> MoodHistory(int days = 30)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                var startDate = DateTime.UtcNow.AddDays(-days);
                var moods = await _moodEntryCollection
                    .Find(m => m.UserId == currentUser.Id.ToString() && m.EntryDate >= startDate)
                    .SortByDescending(m => m.EntryDate)
                    .ToListAsync();

                return Json(new { success = true, moods });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching mood history");
                return Json(new { success = false, message = "An error occurred" });
            }
        }

        /// <summary>
        /// Assessment results page
        /// </summary>
        [HttpGet]
        [Route("MentalHealth/Results/{id}")]
        public async Task<IActionResult> Results(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var assessment = await _assessmentCollection
                .Find(a => a.Id == id && a.UserId == currentUser.Id.ToString())
                .FirstOrDefaultAsync();

            if (assessment == null)
            {
                TempData["ErrorMessage"] = "Assessment not found";
                return RedirectToAction("Index");
            }

            ViewBag.UserName = currentUser.FullName;
            return View(assessment);
        }

        /// <summary>
        /// Get assessment history
        /// </summary>
        [HttpGet]
        [Route("MentalHealth/History")]
        public async Task<IActionResult> History()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var assessments = await _assessmentCollection
                .Find(a => a.UserId == currentUser.Id.ToString())
                .SortByDescending(a => a.AssessmentDate)
                .ToListAsync();

            ViewBag.UserName = currentUser.FullName;
            return View(assessments);
        }

        /// <summary>
        /// Mental Health Resources page
        /// </summary>
        [HttpGet]
        [Route("MentalHealth/Resources")]
        public IActionResult Resources()
        {
            return View();
        }

        /// <summary>
        /// Get user's total stars
        /// </summary>
        [HttpGet]
        [Route("MentalHealth/GetStars")]
        public async Task<IActionResult> GetStars()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                // Calculate total stars earned from all wellness plans
                var allPlans = await _wellnessPlanCollection
                    .Find(w => w.UserId == currentUser.Id.ToString())
                    .ToListAsync();

                var totalEarned = allPlans.Sum(p => p.TotalStarsEarned);

                // Calculate total stars spent on redemptions
                var redemptions = await _rewardRedemptionCollection
                    .Find(r => r.UserId == currentUser.Id.ToString())
                    .ToListAsync();

                var totalSpent = redemptions.Sum(r => r.StarsSpent);
                var availableStars = totalEarned - totalSpent;

                return Json(new
                {
                    success = true,
                    totalEarned,
                    totalSpent,
                    availableStars
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching stars");
                return Json(new { success = false, message = "An error occurred" });
            }
        }

        /// <summary>
        /// Rewards page
        /// </summary>
        [HttpGet]
        [Route("MentalHealth/Rewards")]
        public async Task<IActionResult> Rewards()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Calculate available stars
            var allPlans = await _wellnessPlanCollection
                .Find(w => w.UserId == currentUser.Id.ToString())
                .ToListAsync();

            var totalEarned = allPlans.Sum(p => p.TotalStarsEarned);

            var redemptions = await _rewardRedemptionCollection
                .Find(r => r.UserId == currentUser.Id.ToString())
                .ToListAsync();

            var totalSpent = redemptions.Sum(r => r.StarsSpent);
            var availableStars = totalEarned - totalSpent;

            ViewBag.AvailableStars = availableStars;
            ViewBag.TotalEarned = totalEarned;
            ViewBag.RewardCatalog = _rewardCatalog;
            ViewBag.UserName = currentUser.FullName;
            ViewBag.MyRedemptions = redemptions.OrderByDescending(r => r.RedeemedAt).ToList();

            return View();
        }

        /// <summary>
        /// Redeem a reward
        /// </summary>
        [HttpPost]
        [Route("MentalHealth/RedeemReward")]
        public async Task<IActionResult> RedeemReward([FromBody] RedeemRewardRequest request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                // Find the reward
                var reward = _rewardCatalog.FirstOrDefault(r => r.Name == request.RewardName);
                if (reward == null)
                {
                    return Json(new { success = false, message = "Reward not found" });
                }

                // Calculate available stars
                var allPlans = await _wellnessPlanCollection
                    .Find(w => w.UserId == currentUser.Id.ToString())
                    .ToListAsync();

                var totalEarned = allPlans.Sum(p => p.TotalStarsEarned);

                var redemptions = await _rewardRedemptionCollection
                    .Find(r => r.UserId == currentUser.Id.ToString())
                    .ToListAsync();

                var totalSpent = redemptions.Sum(r => r.StarsSpent);
                var availableStars = totalEarned - totalSpent;

                // Check if user has enough stars
                if (availableStars < reward.StarsRequired)
                {
                    return Json(new { success = false, message = "Insufficient stars" });
                }

                // Create redemption record
                var redemption = new RewardRedemption
                {
                    UserId = currentUser.Id.ToString(),
                    UserName = currentUser.FullName,
                    RewardName = reward.Name,
                    StarsSpent = reward.StarsRequired,
                    ImageUrl = reward.ImageUrl,
                    RecipientName = request.RecipientName,
                    Email = request.Email,
                    Phone = request.Phone,
                    Address = request.Address,
                    Status = "Pending"
                };

                await _rewardRedemptionCollection.InsertOneAsync(redemption);

                // Send notification
                await _notificationService.CreateNotificationAsync(
                    currentUser.Id.ToString(),
                    "Reward Redeemed! 🎁",
                    $"You've successfully redeemed {reward.Name} for {reward.StarsRequired} stars. We'll process your order soon!",
                    NotificationType.General,
                    null,
                    "/MentalHealth/Rewards"
                );

                return Json(new
                {
                    success = true,
                    message = "Reward redeemed successfully!",
                    remainingStars = availableStars - reward.StarsRequired
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error redeeming reward");
                return Json(new { success = false, message = "An error occurred" });
            }
        }
    }

    // Request DTOs
    public class CompleteTaskRequest
    {
        public string PlanId { get; set; } = string.Empty;
        public string TaskId { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public int? Rating { get; set; }
    }

    public class SavePlaceRequest
    {
        public string PlanId { get; set; } = string.Empty;
        public string TaskId { get; set; } = string.Empty;
        public SuggestedPlace Place { get; set; } = new();
    }

    public class LogMoodRequest
    {
        public string? WellnessPlanId { get; set; }
        public int Mood { get; set; }
        public string? Notes { get; set; }
        public List<string>? Activities { get; set; }
    }

    public class RedeemRewardRequest
    {
        public string RewardName { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }
}
