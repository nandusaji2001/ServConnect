using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using ServConnect.Models;
using ServConnect.Services;
using ServConnect.ViewModels;

namespace ServConnect.Controllers
{
    [Authorize(Roles = RoleTypes.User)]
    public class GuardianController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly IMongoCollection<ElderRequest> _elderRequestsCollection;
        private readonly IMongoCollection<ElderCareInfo> _elderCareInfoCollection;
        private readonly INotificationService _notificationService;

        public GuardianController(
            UserManager<Users> userManager,
            IConfiguration configuration,
            INotificationService notificationService)
        {
            _userManager = userManager;
            _notificationService = notificationService;
            var connectionString = configuration["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var databaseName = configuration["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _elderRequestsCollection = database.GetCollection<ElderRequest>("ElderRequests");
            _elderCareInfoCollection = database.GetCollection<ElderCareInfo>("ElderCareInfo");
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            // Get all pending elder requests for this guardian (use filter builder to avoid ObjectId issues)
            var filter = Builders<ElderRequest>.Filter.And(
                Builders<ElderRequest>.Filter.Eq("GuardianUserId", currentUser.Id.ToString()),
                Builders<ElderRequest>.Filter.Eq("Status", "Pending")
            );
            var pendingRequests = await _elderRequestsCollection.Find(filter).ToListAsync();

            // Map to view models
            var viewModel = pendingRequests.Select(r => new ElderRequestViewModel
            {
                Id = r.Id,
                ElderName = r.ElderName,
                ElderPhone = r.ElderPhone,
                Status = r.Status,
                CreatedAt = r.CreatedAt
            }).ToList();

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRequest(string requestId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            // Find the request using filter builder
            var filter = Builders<ElderRequest>.Filter.And(
                Builders<ElderRequest>.Filter.Eq("_id", MongoDB.Bson.ObjectId.Parse(requestId)),
                Builders<ElderRequest>.Filter.Eq("GuardianUserId", currentUser.Id.ToString())
            );
            var request = await _elderRequestsCollection.Find(filter).FirstOrDefaultAsync();

            if (request == null)
            {
                TempData["ErrorMessage"] = "Request not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Update request status to Approved
            var update = Builders<ElderRequest>.Update
                .Set(r => r.Status, "Approved")
                .Set(r => r.UpdatedAt, DateTime.UtcNow);

            await _elderRequestsCollection.UpdateOneAsync(filter, update);

            // Update elder care info to assign guardian
            var elderFilter = Builders<ElderCareInfo>.Filter.Eq("UserId", request.ElderUserId);
            var elderUpdate = Builders<ElderCareInfo>.Update
                .Set("GuardianUserId", currentUser.Id.ToString())
                .Set("IsGuardianAssigned", true)
                .Set("UpdatedAt", DateTime.UtcNow);

            await _elderCareInfoCollection.UpdateOneAsync(elderFilter, elderUpdate);

            // Activate Elder mode on the elder user (IsElder = true)
            var elderUser = await _userManager.FindByIdAsync(request.ElderUserId);
            if (elderUser != null)
            {
                elderUser.IsElder = true;
                await _userManager.UpdateAsync(elderUser);

                // Send notification to elder user
                await _notificationService.CreateNotificationAsync(
                    request.ElderUserId,
                    "Guardian Request Approved",
                    $"{currentUser.FullName} has accepted your guardian request. Elder Care mode is now active!",
                    NotificationType.General,
                    requestId,
                    "/Home/Index"
                );
            }

            // Set IsGuardian = true on the guardian user
            if (!currentUser.IsGuardian)
            {
                currentUser.IsGuardian = true;
                await _userManager.UpdateAsync(currentUser);
            }

            TempData["SuccessMessage"] = $"You are now the guardian for {request.ElderName}. Elder Care mode has been activated for them.";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectRequest(string requestId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            // Find the request using filter builder
            var filter = Builders<ElderRequest>.Filter.And(
                Builders<ElderRequest>.Filter.Eq("_id", MongoDB.Bson.ObjectId.Parse(requestId)),
                Builders<ElderRequest>.Filter.Eq("GuardianUserId", currentUser.Id.ToString())
            );
            var request = await _elderRequestsCollection.Find(filter).FirstOrDefaultAsync();

            if (request == null)
            {
                TempData["ErrorMessage"] = "Request not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Update request status to Rejected
            var update = Builders<ElderRequest>.Update
                .Set(r => r.Status, "Rejected")
                .Set(r => r.UpdatedAt, DateTime.UtcNow);

            await _elderRequestsCollection.UpdateOneAsync(filter, update);

            // Delete the elder care info since request was rejected
            var elderFilter = Builders<ElderCareInfo>.Filter.Eq("UserId", request.ElderUserId);
            await _elderCareInfoCollection.DeleteOneAsync(elderFilter);

            // Send notification to elder user about rejection
            await _notificationService.CreateNotificationAsync(
                request.ElderUserId,
                "Guardian Request Rejected",
                $"{currentUser.FullName} has declined your guardian request. You can try adding a different guardian.",
                NotificationType.General,
                requestId,
                "/ElderCare/Confirm"
            );

            TempData["SuccessMessage"] = "Elder request rejected. The elder user has been notified.";
            return RedirectToAction(nameof(Dashboard));
        }

        // =============================================
        // GUARDIAN MONITORING DASHBOARD
        // =============================================
        [HttpGet]
        public async Task<IActionResult> MonitoringDashboard()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsGuardian)
            {
                return RedirectToAction("Index", "Home");
            }

            // Get all elders assigned to this guardian
            var elderFilter = Builders<ElderCareInfo>.Filter.And(
                Builders<ElderCareInfo>.Filter.Eq("GuardianUserId", currentUser.Id.ToString()),
                Builders<ElderCareInfo>.Filter.Eq("IsGuardianAssigned", true)
            );
            var assignedElders = await _elderCareInfoCollection.Find(elderFilter).ToListAsync();

            // Get check-in collection
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var client = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017");
            var database = client.GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb");
            var checkInCollection = database.GetCollection<ElderCheckIn>("ElderCheckIns");

            var elderMonitoringData = new List<ElderMonitorViewModel>();

            foreach (var elder in assignedElders)
            {
                var elderUser = await _userManager.FindByIdAsync(elder.UserId);
                
                // Get last check-in
                var lastCheckIn = await checkInCollection.Find(
                    Builders<ElderCheckIn>.Filter.Eq("UserId", elder.UserId)
                ).SortByDescending(c => c.CheckInTime).FirstOrDefaultAsync();

                // Determine status
                var status = "Unknown";
                var statusClass = "secondary";
                var hoursAgo = lastCheckIn != null ? (DateTime.UtcNow - lastCheckIn.CheckInTime).TotalHours : 999;

                if (lastCheckIn != null)
                {
                    if (hoursAgo < 12)
                    {
                        status = "Active";
                        statusClass = "success";
                    }
                    else if (hoursAgo < 24)
                    {
                        status = "Check Soon";
                        statusClass = "warning";
                    }
                    else
                    {
                        status = "Missed Check-in";
                        statusClass = "danger";
                    }
                }

                elderMonitoringData.Add(new ElderMonitorViewModel
                {
                    UserId = elder.UserId,
                    FullName = elderUser?.FullName ?? elder.FullName,
                    Age = elder.Age,
                    Phone = elderUser?.PhoneNumber ?? "",
                    LastCheckInTime = lastCheckIn?.CheckInTime,
                    LastMood = lastCheckIn?.Mood ?? "N/A",
                    Status = status,
                    StatusClass = statusClass,
                    MedicalConditions = elder.MedicalConditions ?? "None listed",
                    BloodGroup = elder.BloodGroup ?? "N/A"
                });
            }

            // Get pending requests count
            var pendingFilter = Builders<ElderRequest>.Filter.And(
                Builders<ElderRequest>.Filter.Eq("GuardianUserId", currentUser.Id.ToString()),
                Builders<ElderRequest>.Filter.Eq("Status", "Pending")
            );
            var pendingCount = await _elderRequestsCollection.CountDocumentsAsync(pendingFilter);

            // Get active SOS alerts
            var sosCollection = database.GetCollection<SOSAlert>("SOSAlerts");
            var sosAlerts = await sosCollection.Find(
                Builders<SOSAlert>.Filter.And(
                    Builders<SOSAlert>.Filter.Eq("GuardianId", currentUser.Id.ToString()),
                    Builders<SOSAlert>.Filter.Eq("IsAcknowledged", false)
                )
            ).SortByDescending(s => s.TriggeredAt).ToListAsync();

            ViewBag.PendingRequestsCount = pendingCount;
            ViewBag.GuardianName = currentUser.FullName;
            ViewBag.SOSAlerts = sosAlerts;

            return View(elderMonitoringData);
        }

        [HttpPost]
        public async Task<IActionResult> AcknowledgeSOS([FromBody] string sosId)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null || !currentUser.IsGuardian)
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var client = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017");
                var database = client.GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb");
                var sosCollection = database.GetCollection<SOSAlert>("SOSAlerts");

                var filter = Builders<SOSAlert>.Filter.And(
                    Builders<SOSAlert>.Filter.Eq("_id", ObjectId.Parse(sosId)),
                    Builders<SOSAlert>.Filter.Eq("GuardianId", currentUser.Id.ToString())
                );
                
                var update = Builders<SOSAlert>.Update
                    .Set("IsAcknowledged", true)
                    .Set("AcknowledgedAt", DateTime.UtcNow);

                var result = await sosCollection.UpdateOneAsync(filter, update);

                return Json(new { success = result.ModifiedCount > 0 });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =============================================
        // MONITOR INDIVIDUAL ELDER
        // =============================================
        [HttpGet]
        public async Task<IActionResult> ElderMonitor(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsGuardian)
            {
                return RedirectToAction("Index", "Home");
            }

            // Verify this elder is assigned to this guardian
            var elderFilter = Builders<ElderCareInfo>.Filter.And(
                Builders<ElderCareInfo>.Filter.Eq("UserId", id),
                Builders<ElderCareInfo>.Filter.Eq("GuardianUserId", currentUser.Id.ToString())
            );
            var elderInfo = await _elderCareInfoCollection.Find(elderFilter).FirstOrDefaultAsync();

            if (elderInfo == null)
            {
                TempData["ErrorMessage"] = "You are not authorized to view this elder's information.";
                return RedirectToAction(nameof(MonitoringDashboard));
            }

            var elderUser = await _userManager.FindByIdAsync(id);

            // Get check-in history
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var client = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017");
            var database = client.GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb");
            var checkInCollection = database.GetCollection<ElderCheckIn>("ElderCheckIns");
            var reminderCollection = database.GetCollection<MedicineReminder>("MedicineReminders");
            var messageCollection = database.GetCollection<GuardianMessage>("GuardianMessages");

            var checkIns = await checkInCollection.Find(
                Builders<ElderCheckIn>.Filter.Eq("UserId", id)
            ).SortByDescending(c => c.CheckInTime).Limit(10).ToListAsync();

            var reminders = await reminderCollection.Find(
                Builders<MedicineReminder>.Filter.Eq("UserId", id)
            ).ToListAsync();

            var messages = await messageCollection.Find(
                Builders<GuardianMessage>.Filter.Or(
                    Builders<GuardianMessage>.Filter.And(
                        Builders<GuardianMessage>.Filter.Eq("SenderId", currentUser.Id.ToString()),
                        Builders<GuardianMessage>.Filter.Eq("ReceiverId", id)
                    ),
                    Builders<GuardianMessage>.Filter.And(
                        Builders<GuardianMessage>.Filter.Eq("SenderId", id),
                        Builders<GuardianMessage>.Filter.Eq("ReceiverId", currentUser.Id.ToString())
                    )
                )
            ).SortByDescending(m => m.SentAt).Limit(20).ToListAsync();

            ViewBag.ElderInfo = elderInfo;
            ViewBag.ElderUser = elderUser;
            ViewBag.CheckIns = checkIns;
            ViewBag.Reminders = reminders;
            ViewBag.Messages = messages.OrderBy(m => m.SentAt).ToList();
            ViewBag.CurrentUserId = currentUser.Id.ToString();

            return View();
        }

        // =============================================
        // EDIT ELDER PROFILE (Guardian can edit all details)
        // =============================================
        [HttpGet]
        public async Task<IActionResult> EditElderProfile(string id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsGuardian)
            {
                return RedirectToAction("Index", "Home");
            }

            // Verify this elder is assigned to this guardian
            var elderFilter = Builders<ElderCareInfo>.Filter.And(
                Builders<ElderCareInfo>.Filter.Eq("UserId", id),
                Builders<ElderCareInfo>.Filter.Eq("GuardianUserId", currentUser.Id.ToString())
            );
            var elderInfo = await _elderCareInfoCollection.Find(elderFilter).FirstOrDefaultAsync();

            if (elderInfo == null)
            {
                TempData["ErrorMessage"] = "You are not authorized to edit this elder's information.";
                return RedirectToAction(nameof(MonitoringDashboard));
            }

            var viewModel = new GuardianEditElderViewModel
            {
                ElderId = id,
                FullName = elderInfo.FullName,
                DateOfBirth = elderInfo.DateOfBirth,
                Gender = elderInfo.Gender,
                BloodGroup = elderInfo.BloodGroup ?? "",
                MedicalConditions = elderInfo.MedicalConditions ?? "",
                Medications = elderInfo.Medications
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditElderProfile(GuardianEditElderViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsGuardian)
            {
                return RedirectToAction("Index", "Home");
            }

            // Verify this elder is assigned to this guardian
            var elderFilter = Builders<ElderCareInfo>.Filter.And(
                Builders<ElderCareInfo>.Filter.Eq("UserId", model.ElderId),
                Builders<ElderCareInfo>.Filter.Eq("GuardianUserId", currentUser.Id.ToString())
            );
            var elderInfo = await _elderCareInfoCollection.Find(elderFilter).FirstOrDefaultAsync();

            if (elderInfo == null)
            {
                TempData["ErrorMessage"] = "You are not authorized to edit this elder's information.";
                return RedirectToAction(nameof(MonitoringDashboard));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Guardian can update all fields including DOB
            var update = Builders<ElderCareInfo>.Update
                .Set("DateOfBirth", model.DateOfBirth)
                .Set("Gender", model.Gender)
                .Set("BloodGroup", model.BloodGroup ?? "")
                .Set("MedicalConditions", model.MedicalConditions ?? "")
                .Set("Medications", model.Medications ?? "")
                .Set("UpdatedAt", DateTime.UtcNow);

            await _elderCareInfoCollection.UpdateOneAsync(elderFilter, update);

            TempData["SuccessMessage"] = "Elder profile updated successfully!";
            return RedirectToAction("ElderMonitor", new { id = model.ElderId });
        }

        // =============================================
        // SEND MESSAGE TO ELDER
        // =============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(string elderId, string message)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsGuardian)
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                return Json(new { success = false, message = "Message cannot be empty" });
            }

            // Verify guardian relationship
            var elderFilter = Builders<ElderCareInfo>.Filter.And(
                Builders<ElderCareInfo>.Filter.Eq("UserId", elderId),
                Builders<ElderCareInfo>.Filter.Eq("GuardianUserId", currentUser.Id.ToString())
            );
            var elderInfo = await _elderCareInfoCollection.Find(elderFilter).FirstOrDefaultAsync();

            if (elderInfo == null)
            {
                return Json(new { success = false, message = "Not authorized" });
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var client = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017");
            var database = client.GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb");
            var messageCollection = database.GetCollection<GuardianMessage>("GuardianMessages");

            var newMessage = new GuardianMessage
            {
                SenderId = currentUser.Id.ToString(),
                SenderName = currentUser.FullName,
                ReceiverId = elderId,
                Message = message,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            await messageCollection.InsertOneAsync(newMessage);

            // Send notification to elder
            await _notificationService.CreateNotificationAsync(
                elderId,
                "💬 Message from Guardian",
                $"{currentUser.FullName}: {(message.Length > 50 ? message.Substring(0, 50) + "..." : message)}",
                NotificationType.General,
                null,
                "/ElderCare/GuardianInfo"
            );

            return Json(new { success = true, message = "Message sent!" });
        }

        // =============================================
        // ADD/EDIT MEDICINE REMINDER
        // =============================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReminder(string elderId, string medicineName, string dosage, string frequency, string instruction, [FromForm] List<string> times)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsGuardian)
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            // Verify guardian relationship
            var elderFilter = Builders<ElderCareInfo>.Filter.And(
                Builders<ElderCareInfo>.Filter.Eq("UserId", elderId),
                Builders<ElderCareInfo>.Filter.Eq("GuardianUserId", currentUser.Id.ToString())
            );
            var elderInfo = await _elderCareInfoCollection.Find(elderFilter).FirstOrDefaultAsync();

            if (elderInfo == null)
            {
                return Json(new { success = false, message = "Not authorized" });
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var client = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017");
            var database = client.GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb");
            var reminderCollection = database.GetCollection<MedicineReminder>("MedicineReminders");

            // Filter out empty times
            var timeList = times?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? new List<string>();

            var reminder = new MedicineReminder
            {
                UserId = elderId,
                MedicineName = medicineName,
                Dosage = dosage,
                Frequency = frequency,
                Times = timeList,
                Instruction = instruction ?? "After Food",
                IsActive = true
            };

            await reminderCollection.InsertOneAsync(reminder);

            // Notify elder
            await _notificationService.CreateNotificationAsync(
                elderId,
                "💊 New Medicine Reminder",
                $"Your guardian added a reminder for {medicineName}",
                NotificationType.General,
                null,
                "/ElderCare/MedicineReminders"
            );

            return Json(new { success = true, message = "Reminder added!" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReminder(string reminderId, string elderId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsGuardian)
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            // Verify guardian relationship
            var elderFilter = Builders<ElderCareInfo>.Filter.And(
                Builders<ElderCareInfo>.Filter.Eq("UserId", elderId),
                Builders<ElderCareInfo>.Filter.Eq("GuardianUserId", currentUser.Id.ToString())
            );
            var elderInfo = await _elderCareInfoCollection.Find(elderFilter).FirstOrDefaultAsync();

            if (elderInfo == null)
            {
                return Json(new { success = false, message = "Not authorized" });
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var client = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017");
            var database = client.GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb");
            var reminderCollection = database.GetCollection<MedicineReminder>("MedicineReminders");

            await reminderCollection.DeleteOneAsync(
                Builders<MedicineReminder>.Filter.Eq("_id", ObjectId.Parse(reminderId))
            );

            return Json(new { success = true, message = "Reminder deleted!" });
        }
    }

    // =============================================
    // Supporting Models
    // =============================================
    public class ElderMonitorViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Phone { get; set; } = string.Empty;
        public DateTime? LastCheckInTime { get; set; }
        public string LastMood { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusClass { get; set; } = string.Empty;
        public string MedicalConditions { get; set; } = string.Empty;
        public string BloodGroup { get; set; } = string.Empty;
    }

    public class GuardianMessage
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string ReceiverId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public bool IsRead { get; set; }
    }
}