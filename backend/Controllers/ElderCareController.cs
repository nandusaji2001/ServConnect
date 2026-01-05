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
    public class ElderCareController : Controller
    {
        private readonly UserManager<Users> _userManager;
        private readonly IMongoCollection<ElderCareInfo> _elderCareInfoCollection;
        private readonly IMongoCollection<ElderRequest> _elderRequestsCollection;
        private readonly INotificationService _notificationService;
        private readonly IEmailService _emailService;

        public ElderCareController(
            UserManager<Users> userManager,
            IConfiguration configuration,
            INotificationService notificationService,
            IEmailService emailService)
        {
            _userManager = userManager;
            _notificationService = notificationService;
            _emailService = emailService;
            var connectionString = configuration["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
            var databaseName = configuration["MongoDB:DatabaseName"] ?? "ServConnectDb";
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _elderCareInfoCollection = database.GetCollection<ElderCareInfo>("ElderCareInfo");
            _elderRequestsCollection = database.GetCollection<ElderRequest>("ElderRequests");
        }

        [HttpGet]
        public async Task<IActionResult> Confirm()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // If already an elder, redirect to dashboard
            if (currentUser.IsElder)
            {
                return RedirectToAction("Index", "Home");
            }

            // Check if there's already a pending guardian request
            var pendingFilter = Builders<ElderRequest>.Filter.And(
                Builders<ElderRequest>.Filter.Eq("ElderUserId", currentUser.Id.ToString()),
                Builders<ElderRequest>.Filter.Eq("Status", "Pending")
            );
            var pendingRequest = await _elderRequestsCollection.Find(pendingFilter).FirstOrDefaultAsync();
            
            if (pendingRequest != null)
            {
                // User already has a pending request, redirect to pending page
                return RedirectToAction("RequestPending", "ElderCare");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(bool confirmed)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (confirmed)
            {
                // Redirect to Elder Profile Setup
                return RedirectToAction("ProfileSetup", "ElderCare");
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public async Task<IActionResult> ProfileSetup()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // If already an elder, redirect to dashboard
            if (currentUser.IsElder)
            {
                return RedirectToAction("Index", "Home");
            }

            // Check if elder profile already exists
            var filter = Builders<ElderCareInfo>.Filter.Eq("UserId", currentUser.Id.ToString());
            var existingProfile = await _elderCareInfoCollection
                .Find(filter)
                .FirstOrDefaultAsync();

            if (existingProfile != null)
            {
                // Profile exists, redirect to guardian request step
                return RedirectToAction("GuardianRequest", "ElderCare");
            }

            var viewModel = new ElderProfileSetupViewModel
            {
                FullName = currentUser.FullName
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProfileSetup(ElderProfileSetupViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Ensure FullName is set from user profile
            model.FullName = currentUser.FullName;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check if guardian is confirmed
            if (!model.IsGuardianConfirmed || string.IsNullOrEmpty(model.GuardianCandidateId))
            {
                ModelState.AddModelError("EmergencyPhone", "Please verify and confirm the guardian before submitting.");
                return View(model);
            }

            // Check if this phone number is already used as emergency contact for another elder
            var phoneFilter = Builders<ElderCareInfo>.Filter.And(
                Builders<ElderCareInfo>.Filter.Eq("EmergencyPhone", model.EmergencyPhone),
                Builders<ElderCareInfo>.Filter.Ne("UserId", currentUser.Id.ToString())
            );
            var existingEmergencyPhone = await _elderCareInfoCollection
                .Find(phoneFilter)
                .FirstOrDefaultAsync();

            if (existingEmergencyPhone != null)
            {
                ModelState.AddModelError("EmergencyPhone", "This phone number is already registered as emergency contact for another elder.");
                return View(model);
            }

            // Check if guardian user exists (try both with and without +91 prefix)
            var formattedEmergencyPhone = "+91" + model.EmergencyPhone;
            var guardianUser = _userManager.Users.FirstOrDefault(u => 
                u.PhoneNumber == model.EmergencyPhone || u.PhoneNumber == formattedEmergencyPhone);
            if (guardianUser == null)
            {
                // Guardian not registered - show message
                TempData["GuardianNotRegistered"] = true;
                TempData["GuardianPhone"] = model.EmergencyPhone;
                return View(model);
            }

            // Create elder care info (do NOT activate elder mode yet)
            var elderCareInfo = new ElderCareInfo
            {
                UserId = currentUser.Id.ToString(),
                FullName = currentUser.FullName,
                Address = currentUser.Address ?? string.Empty,
                DateOfBirth = model.DateOfBirth,
                Gender = model.Gender,
                BloodGroup = model.BloodGroup,
                MedicalConditions = model.MedicalConditions,
                Medications = model.Medications ?? string.Empty,
                EmergencyPhone = model.EmergencyPhone,
                GuardianUserId = guardianUser.Id.ToString(),
                IsGuardianAssigned = false, // Will be true after guardian accepts
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _elderCareInfoCollection.InsertOneAsync(elderCareInfo);

            // Create guardian request
            var elderRequest = new ElderRequest
            {
                ElderUserId = currentUser.Id.ToString(),
                ElderName = currentUser.FullName,
                ElderPhone = currentUser.PhoneNumber ?? string.Empty,
                GuardianUserId = guardianUser.Id.ToString(),
                GuardianPhone = model.EmergencyPhone,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _elderRequestsCollection.InsertOneAsync(elderRequest);

            // Send notification to guardian
            await _notificationService.CreateNotificationAsync(
                guardianUser.Id.ToString(),
                "Guardian Request",
                $"{currentUser.FullName} has requested you to be their guardian for Elder Care services.",
                NotificationType.General,
                elderRequest.Id,
                "/Guardian/Dashboard"
            );

            // Redirect to pending status page
            return RedirectToAction("RequestPending", "ElderCare");
        }

        [HttpGet]
        public async Task<IActionResult> RequestPending()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // If already an elder, redirect to Elder Dashboard
            if (currentUser.IsElder)
            {
                return RedirectToAction("Dashboard", "ElderCare");
            }

            // Get the pending request
            var requestFilter = Builders<ElderRequest>.Filter.And(
                Builders<ElderRequest>.Filter.Eq("ElderUserId", currentUser.Id.ToString()),
                Builders<ElderRequest>.Filter.Eq("Status", "Pending")
            );
            var pendingRequest = await _elderRequestsCollection.Find(requestFilter).FirstOrDefaultAsync();

            if (pendingRequest == null)
            {
                // No pending request, check if already approved
                var approvedFilter = Builders<ElderRequest>.Filter.And(
                    Builders<ElderRequest>.Filter.Eq("ElderUserId", currentUser.Id.ToString()),
                    Builders<ElderRequest>.Filter.Eq("Status", "Approved")
                );
                var approvedRequest = await _elderRequestsCollection.Find(approvedFilter).FirstOrDefaultAsync();
                
                if (approvedRequest != null)
                {
                    return RedirectToAction("Dashboard", "ElderCare");
                }
                
                return RedirectToAction("ProfileSetup", "ElderCare");
            }

            // Get guardian name
            var guardian = await _userManager.FindByIdAsync(pendingRequest.GuardianUserId);
            ViewBag.GuardianName = guardian?.FullName ?? "Guardian";
            ViewBag.GuardianPhone = pendingRequest.GuardianPhone;
            ViewBag.RequestDate = pendingRequest.CreatedAt;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> LookupGuardian(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return Json(new { success = false, message = "Phone number is required" });
            }

            // Validate phone format
            if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^[6-9]\d{9}$"))
            {
                return Json(new { success = false, message = "Please enter a valid 10-digit Indian mobile number" });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            // Format phone number with country code for lookup
            var formattedPhone = "+91" + phone;

            // Cannot use own phone number
            if (currentUser.PhoneNumber == phone || currentUser.PhoneNumber == formattedPhone)
            {
                return Json(new { success = false, message = "You cannot use your own phone number as emergency contact" });
            }

            // Find user by phone number (try both with and without +91 prefix)
            var guardianCandidate = _userManager.Users.FirstOrDefault(u => 
                u.PhoneNumber == phone || u.PhoneNumber == formattedPhone);

            if (guardianCandidate == null)
            {
                return Json(new { success = false, message = "No registered user found with this phone number" });
            }

            // Check if this user is already a guardian for another elder
            var guardianFilter = Builders<ElderCareInfo>.Filter.And(
                Builders<ElderCareInfo>.Filter.Eq("EmergencyPhone", phone),
                Builders<ElderCareInfo>.Filter.Eq("IsGuardianAssigned", true)
            );
            var existingGuardianship = await _elderCareInfoCollection
                .Find(guardianFilter)
                .FirstOrDefaultAsync();

            if (existingGuardianship != null)
            {
                return Json(new { success = false, message = "This user is already assigned as guardian to another elder" });
            }

            return Json(new { 
                success = true, 
                guardianId = guardianCandidate.Id.ToString(),
                guardianName = guardianCandidate.FullName ?? guardianCandidate.Email
            });
        }

        // =============================================
        // ELDER DASHBOARD - Main entry point for elders
        // =============================================
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Only allow elder users to access this dashboard
            if (!currentUser.IsElder)
            {
                return RedirectToAction("Index", "Home");
            }

            // Get elder care info
            var filter = Builders<ElderCareInfo>.Filter.Eq("UserId", currentUser.Id.ToString());
            var elderInfo = await _elderCareInfoCollection.Find(filter).FirstOrDefaultAsync();

            if (elderInfo == null)
            {
                return RedirectToAction("ProfileSetup", "ElderCare");
            }

            // Get guardian info
            Users? guardian = null;
            if (!string.IsNullOrEmpty(elderInfo.GuardianUserId))
            {
                guardian = await _userManager.FindByIdAsync(elderInfo.GuardianUserId);
            }

            ViewBag.ElderInfo = elderInfo;
            ViewBag.GuardianName = guardian?.FullName ?? "Not Assigned";
            ViewBag.GuardianPhone = elderInfo.EmergencyPhone;
            ViewBag.UserName = currentUser.FullName;

            return View();
        }

        // =============================================
        // Emergency SOS
        // =============================================
        [HttpGet]
        public async Task<IActionResult> EmergencySOS()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return RedirectToAction("Index", "Home");
            }

            // Get elder care info for emergency contact
            var filter = Builders<ElderCareInfo>.Filter.Eq("UserId", currentUser.Id.ToString());
            var elderInfo = await _elderCareInfoCollection.Find(filter).FirstOrDefaultAsync();

            ViewBag.EmergencyPhone = elderInfo?.EmergencyPhone ?? "";
            ViewBag.ElderInfo = elderInfo;

            // Get guardian info
            if (elderInfo != null && !string.IsNullOrEmpty(elderInfo.GuardianUserId))
            {
                var guardian = await _userManager.FindByIdAsync(elderInfo.GuardianUserId);
                ViewBag.GuardianName = guardian?.FullName ?? "Guardian";
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TriggerSOS()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            // Get elder care info
            var filter = Builders<ElderCareInfo>.Filter.Eq("UserId", currentUser.Id.ToString());
            var elderInfo = await _elderCareInfoCollection.Find(filter).FirstOrDefaultAsync();

            if (elderInfo == null || string.IsNullOrEmpty(elderInfo.GuardianUserId))
            {
                return Json(new { success = false, message = "No guardian assigned" });
            }

            // Get guardian user info
            var guardian = await _userManager.FindByIdAsync(elderInfo.GuardianUserId);
            if (guardian == null)
            {
                return Json(new { success = false, message = "Guardian not found" });
            }

            // Store SOS alert in database
            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var client = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017");
            var database = client.GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb");
            var sosCollection = database.GetCollection<SOSAlert>("SOSAlerts");

            var elderName = elderInfo.FullName ?? currentUser.FullName ?? "Elder";
            var elderPhone = elderInfo.EmergencyPhone ?? currentUser.PhoneNumber ?? "";

            var sosAlert = new SOSAlert
            {
                ElderId = currentUser.Id.ToString(),
                ElderName = elderName,
                GuardianId = elderInfo.GuardianUserId,
                TriggeredAt = DateTime.UtcNow,
                IsAcknowledged = false,
                ElderPhone = elderPhone
            };
            await sosCollection.InsertOneAsync(sosAlert);

            // Send HIGH PRIORITY notification to guardian
            await _notificationService.CreateNotificationAsync(
                elderInfo.GuardianUserId,
                "🚨 EMERGENCY SOS ALERT 🚨",
                $"URGENT: {elderName} has triggered an emergency SOS! Phone: {elderPhone}. Please respond immediately!",
                NotificationType.SystemAlert,
                null,
                $"/Guardian/ElderMonitor?elderId={currentUser.Id}"
            );

            // Send email notification to guardian
            if (!string.IsNullOrEmpty(guardian.Email))
            {
                try
                {
                    var emailSubject = "🚨 EMERGENCY SOS ALERT - Immediate Action Required!";
                    var emailBody = $@"
<html>
<body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
    <div style='background: linear-gradient(135deg, #dc2626 0%, #ef4444 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0;'>
        <h1 style='margin: 0; font-size: 28px;'>🚨 EMERGENCY SOS ALERT 🚨</h1>
    </div>
    <div style='background: #fff; padding: 30px; border: 1px solid #ddd; border-top: none; border-radius: 0 0 10px 10px;'>
        <p style='font-size: 18px; color: #333; margin-bottom: 20px;'>
            <strong>{elderName}</strong> has triggered an emergency SOS and needs your immediate attention!
        </p>
        <div style='background: #fef2f2; border-left: 4px solid #dc2626; padding: 15px; margin: 20px 0;'>
            <p style='margin: 0; color: #991b1b;'><strong>Time:</strong> {DateTime.Now:MMMM dd, yyyy hh:mm tt}</p>
            <p style='margin: 10px 0 0 0; color: #991b1b;'><strong>Contact Phone:</strong> {elderPhone}</p>
        </div>
        <div style='text-align: center; margin: 30px 0;'>
            <a href='tel:{elderPhone}' style='background: #dc2626; color: white; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-size: 18px; font-weight: bold; display: inline-block;'>
                📞 Call {elderName} Now
            </a>
        </div>
        <p style='color: #666; font-size: 14px;'>
            Please respond immediately and check on {elderName}'s safety. You can also view their status on the ServConnect Guardian Dashboard.
        </p>
    </div>
    <div style='text-align: center; padding: 20px; color: #999; font-size: 12px;'>
        <p>This is an automated emergency alert from ServConnect Elder Care</p>
    </div>
</body>
</html>";

                    await _emailService.SendEmailAsync(guardian.Email, emailSubject, emailBody);
                }
                catch (Exception ex)
                {
                    // Log email error but don't fail the SOS
                    Console.WriteLine($"Failed to send SOS email: {ex.Message}");
                }
            }

            return Json(new { success = true, message = "SOS sent to your guardian!" });
        }

        // =============================================
        // Daily Check-in
        // =============================================
        [HttpGet]
        public async Task<IActionResult> DailyCheckIn()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return RedirectToAction("Index", "Home");
            }

            // Get check-in status for today
            var todayStart = DateTime.UtcNow.Date;
            var checkInCollection = new MongoClient(
                HttpContext.RequestServices.GetRequiredService<IConfiguration>()["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017")
                .GetDatabase(HttpContext.RequestServices.GetRequiredService<IConfiguration>()["MongoDB:DatabaseName"] ?? "ServConnectDb")
                .GetCollection<ElderCheckIn>("ElderCheckIns");

            var todayCheckIn = await checkInCollection.Find(
                Builders<ElderCheckIn>.Filter.And(
                    Builders<ElderCheckIn>.Filter.Eq("UserId", currentUser.Id.ToString()),
                    Builders<ElderCheckIn>.Filter.Gte("CheckInTime", todayStart)
                )
            ).FirstOrDefaultAsync();

            ViewBag.AlreadyCheckedIn = todayCheckIn != null;
            ViewBag.CheckInTime = todayCheckIn?.CheckInTime;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PerformCheckIn(string mood, string notes)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var checkInCollection = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017")
                .GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb")
                .GetCollection<ElderCheckIn>("ElderCheckIns");

            var checkIn = new ElderCheckIn
            {
                UserId = currentUser.Id.ToString(),
                CheckInTime = DateTime.UtcNow,
                Mood = mood,
                Notes = notes
            };

            await checkInCollection.InsertOneAsync(checkIn);

            // Notify guardian
            var elderFilter = Builders<ElderCareInfo>.Filter.Eq("UserId", currentUser.Id.ToString());
            var elderInfo = await _elderCareInfoCollection.Find(elderFilter).FirstOrDefaultAsync();

            if (elderInfo != null && !string.IsNullOrEmpty(elderInfo.GuardianUserId))
            {
                await _notificationService.CreateNotificationAsync(
                    elderInfo.GuardianUserId,
                    "✅ Daily Check-in",
                    $"{currentUser.FullName} has completed their daily check-in. Mood: {mood}",
                    NotificationType.General,
                    null,
                    "/Guardian/ElderMonitor/" + currentUser.Id.ToString()
                );
            }

            return Json(new { success = true, message = "Check-in recorded!" });
        }

        // =============================================
        // Medicine Reminders
        // =============================================
        [HttpGet]
        public async Task<IActionResult> MedicineReminders()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return RedirectToAction("Index", "Home");
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var reminderCollection = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017")
                .GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb")
                .GetCollection<MedicineReminder>("MedicineReminders");

            var reminders = await reminderCollection.Find(
                Builders<MedicineReminder>.Filter.Eq("UserId", currentUser.Id.ToString())
            ).ToListAsync();

            // Get elder care info for medication list
            var elderFilter = Builders<ElderCareInfo>.Filter.Eq("UserId", currentUser.Id.ToString());
            var elderInfo = await _elderCareInfoCollection.Find(elderFilter).FirstOrDefaultAsync();

            ViewBag.Medications = elderInfo?.Medications ?? "";
            ViewBag.Reminders = reminders;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkMedicineTaken(string medicineId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var logCollection = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017")
                .GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb")
                .GetCollection<MedicineLog>("MedicineLogs");

            var log = new MedicineLog
            {
                UserId = currentUser.Id.ToString(),
                MedicineId = medicineId,
                TakenAt = DateTime.UtcNow
            };

            await logCollection.InsertOneAsync(log);

            return Json(new { success = true, message = "Marked as taken!" });
        }

        // =============================================
        // Notes / Diary
        // =============================================
        [HttpGet]
        public async Task<IActionResult> Notes()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return RedirectToAction("Index", "Home");
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var notesCollection = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017")
                .GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb")
                .GetCollection<ElderNote>("ElderNotes");

            var notes = await notesCollection.Find(
                Builders<ElderNote>.Filter.Eq("UserId", currentUser.Id.ToString())
            ).SortByDescending(n => n.CreatedAt).Limit(20).ToListAsync();

            ViewBag.Notes = notes;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveNote(string title, string content)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var notesCollection = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017")
                .GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb")
                .GetCollection<ElderNote>("ElderNotes");

            var note = new ElderNote
            {
                UserId = currentUser.Id.ToString(),
                Title = title,
                Content = content,
                CreatedAt = DateTime.UtcNow
            };

            await notesCollection.InsertOneAsync(note);

            return Json(new { success = true, message = "Note saved!", noteId = note.Id });
        }

        // =============================================
        // Messages from Guardian
        // =============================================
        [HttpGet]
        public async Task<IActionResult> Messages()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return RedirectToAction("Index", "Home");
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var client = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017");
            var database = client.GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb");
            var messageCollection = database.GetCollection<GuardianMessage>("GuardianMessages");

            // Get elder info first to get guardian ID
            var elderFilter = Builders<ElderCareInfo>.Filter.Eq("UserId", currentUser.Id.ToString());
            var elderInfo = await _elderCareInfoCollection.Find(elderFilter).FirstOrDefaultAsync();

            Users? guardian = null;
            if (!string.IsNullOrEmpty(elderInfo?.GuardianUserId))
            {
                guardian = await _userManager.FindByIdAsync(elderInfo.GuardianUserId);
            }

            // Get messages where elder is sender OR receiver (conversation with guardian)
            var messages = await messageCollection.Find(
                Builders<GuardianMessage>.Filter.Or(
                    // Messages sent by elder to guardian
                    Builders<GuardianMessage>.Filter.And(
                        Builders<GuardianMessage>.Filter.Eq("SenderId", currentUser.Id.ToString()),
                        Builders<GuardianMessage>.Filter.Eq("ReceiverId", elderInfo?.GuardianUserId ?? "")
                    ),
                    // Messages sent by guardian to elder
                    Builders<GuardianMessage>.Filter.And(
                        Builders<GuardianMessage>.Filter.Eq("SenderId", elderInfo?.GuardianUserId ?? ""),
                        Builders<GuardianMessage>.Filter.Eq("ReceiverId", currentUser.Id.ToString())
                    )
                )
            ).SortByDescending(m => m.SentAt).Limit(50).ToListAsync();

            // Mark messages as read (only messages where elder is receiver)
            var unreadFilter = Builders<GuardianMessage>.Filter.And(
                Builders<GuardianMessage>.Filter.Eq("ReceiverId", currentUser.Id.ToString()),
                Builders<GuardianMessage>.Filter.Eq("IsRead", false)
            );
            var updateRead = Builders<GuardianMessage>.Update.Set("IsRead", true);
            await messageCollection.UpdateManyAsync(unreadFilter, updateRead);

            ViewBag.Messages = messages.OrderBy(m => m.SentAt).ToList();
            ViewBag.GuardianName = guardian?.FullName ?? "Guardian";
            ViewBag.GuardianId = elderInfo?.GuardianUserId;
            ViewBag.CurrentUserId = currentUser.Id.ToString();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessageToGuardian(string message)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return Json(new { success = false, message = "Unauthorized" });
            }

            var elderFilter = Builders<ElderCareInfo>.Filter.Eq("UserId", currentUser.Id.ToString());
            var elderInfo = await _elderCareInfoCollection.Find(elderFilter).FirstOrDefaultAsync();

            if (elderInfo == null || string.IsNullOrEmpty(elderInfo.GuardianUserId))
            {
                return Json(new { success = false, message = "No guardian assigned" });
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var client = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017");
            var database = client.GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb");
            var messageCollection = database.GetCollection<GuardianMessage>("GuardianMessages");

            var newMessage = new GuardianMessage
            {
                SenderId = currentUser.Id.ToString(),
                SenderName = elderInfo.FullName ?? currentUser.FullName ?? "Elder",
                ReceiverId = elderInfo.GuardianUserId,
                Message = message,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            await messageCollection.InsertOneAsync(newMessage);

            // Notify guardian
            await _notificationService.CreateNotificationAsync(
                elderInfo.GuardianUserId,
                "💬 New Message from Elder",
                $"{elderInfo.FullName}: {(message.Length > 50 ? message.Substring(0, 50) + "..." : message)}",
                NotificationType.General,
                null,
                $"/Guardian/ElderMonitor?elderId={currentUser.Id}"
            );

            return Json(new { success = true, message = "Message sent!" });
        }

        [HttpGet]
        public async Task<IActionResult> GetUnreadMessageCount()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return Json(new { count = 0 });
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var client = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017");
            var database = client.GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb");
            var messageCollection = database.GetCollection<GuardianMessage>("GuardianMessages");

            var unreadCount = await messageCollection.CountDocumentsAsync(
                Builders<GuardianMessage>.Filter.And(
                    Builders<GuardianMessage>.Filter.Eq("ReceiverId", currentUser.Id.ToString()),
                    Builders<GuardianMessage>.Filter.Eq("IsRead", false)
                )
            );

            return Json(new { count = unreadCount });
        }

        [HttpGet]
        public async Task<IActionResult> GetElderDashboardAlerts()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return Json(new { checkedInToday = true, unreadMessages = 0, pendingMedicines = 0 });
            }

            var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var client = new MongoClient(config["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017");
            var database = client.GetDatabase(config["MongoDB:DatabaseName"] ?? "ServConnectDb");

            // Check today's check-in
            var checkInCollection = database.GetCollection<ElderCheckIn>("ElderCheckIns");
            var todayStart = DateTime.UtcNow.Date;
            var checkedInToday = await checkInCollection.CountDocumentsAsync(
                Builders<ElderCheckIn>.Filter.And(
                    Builders<ElderCheckIn>.Filter.Eq("UserId", currentUser.Id.ToString()),
                    Builders<ElderCheckIn>.Filter.Gte("CheckInTime", todayStart)
                )
            ) > 0;

            // Unread messages
            var messageCollection = database.GetCollection<GuardianMessage>("GuardianMessages");
            var unreadMessages = await messageCollection.CountDocumentsAsync(
                Builders<GuardianMessage>.Filter.And(
                    Builders<GuardianMessage>.Filter.Eq("ReceiverId", currentUser.Id.ToString()),
                    Builders<GuardianMessage>.Filter.Eq("IsRead", false)
                )
            );

            // Get reminder count (total active reminders)
            var reminderCollection = database.GetCollection<MedicineReminder>("MedicineReminders");
            var reminderCount = await reminderCollection.CountDocumentsAsync(
                Builders<MedicineReminder>.Filter.And(
                    Builders<MedicineReminder>.Filter.Eq("UserId", currentUser.Id.ToString()),
                    Builders<MedicineReminder>.Filter.Eq("IsActive", true)
                )
            );

            return Json(new { 
                checkedInToday, 
                unreadMessages, 
                reminderCount
            });
        }

        // =============================================
        // Wellness Tips
        // =============================================
        [HttpGet]
        public async Task<IActionResult> WellnessTips()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // =============================================
        // Guardian Info
        // =============================================
        [HttpGet]
        public async Task<IActionResult> GuardianInfo()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return RedirectToAction("Index", "Home");
            }

            var elderFilter = Builders<ElderCareInfo>.Filter.Eq("UserId", currentUser.Id.ToString());
            var elderInfo = await _elderCareInfoCollection.Find(elderFilter).FirstOrDefaultAsync();

            if (elderInfo == null)
            {
                return RedirectToAction("ProfileSetup", "ElderCare");
            }

            Users? guardian = null;
            if (!string.IsNullOrEmpty(elderInfo.GuardianUserId))
            {
                guardian = await _userManager.FindByIdAsync(elderInfo.GuardianUserId);
            }

            ViewBag.GuardianName = guardian?.FullName ?? "Not Assigned";
            ViewBag.GuardianPhone = elderInfo.EmergencyPhone;
            ViewBag.GuardianEmail = guardian?.Email ?? "N/A";
            ViewBag.ElderInfo = elderInfo;

            return View();
        }

        // =============================================
        // Edit Profile (Limited)
        // =============================================
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return RedirectToAction("Index", "Home");
            }

            var elderFilter = Builders<ElderCareInfo>.Filter.Eq("UserId", currentUser.Id.ToString());
            var elderInfo = await _elderCareInfoCollection.Find(elderFilter).FirstOrDefaultAsync();

            if (elderInfo == null)
            {
                return RedirectToAction("ProfileSetup", "ElderCare");
            }

            var viewModel = new ElderProfileSetupViewModel
            {
                FullName = currentUser.FullName,
                DateOfBirth = elderInfo.DateOfBirth,
                Gender = elderInfo.Gender,
                BloodGroup = elderInfo.BloodGroup ?? "",
                MedicalConditions = elderInfo.MedicalConditions ?? "",
                Medications = elderInfo.Medications,
                EmergencyPhone = elderInfo.EmergencyPhone
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(ElderProfileSetupViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return RedirectToAction("Index", "Home");
            }

            // Limited fields can be edited - not emergency phone (guardian)
            var elderFilter = Builders<ElderCareInfo>.Filter.Eq("UserId", currentUser.Id.ToString());
            var elderInfo = await _elderCareInfoCollection.Find(elderFilter).FirstOrDefaultAsync();

            if (elderInfo == null)
            {
                return RedirectToAction("ProfileSetup", "ElderCare");
            }

            // Update allowed fields only (elders can only edit medical info)
            var update = Builders<ElderCareInfo>.Update
                .Set("MedicalConditions", model.MedicalConditions ?? "")
                .Set("Medications", model.Medications ?? "")
                .Set("UpdatedAt", DateTime.UtcNow);

            await _elderCareInfoCollection.UpdateOneAsync(elderFilter, update);

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction("Dashboard", "ElderCare");
        }

        // =============================================
        // Elder-Friendly News
        // =============================================
        [HttpGet]
        public async Task<IActionResult> News()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !currentUser.IsElder)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }
    }

    // =============================================
    // Supporting Models for Elder Care Features
    // =============================================
    public class ElderCheckIn
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public DateTime CheckInTime { get; set; }
        public string Mood { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }

    public class MedicineReminder
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string MedicineName { get; set; } = string.Empty;
        public string Dosage { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public List<string> Times { get; set; } = new();
        public string Instruction { get; set; } = string.Empty; // "Before Food" or "After Food"
        public bool IsActive { get; set; } = true;
    }

    public class MedicineLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string MedicineId { get; set; } = string.Empty;
        public DateTime TakenAt { get; set; }
    }

    public class ElderNote
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class SOSAlert
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string ElderId { get; set; } = string.Empty;
        public string ElderName { get; set; } = string.Empty;
        public string GuardianId { get; set; } = string.Empty;
        public string ElderPhone { get; set; } = string.Empty;
        public DateTime TriggeredAt { get; set; }
        public bool IsAcknowledged { get; set; } = false;
        public DateTime? AcknowledgedAt { get; set; }
    }
}
