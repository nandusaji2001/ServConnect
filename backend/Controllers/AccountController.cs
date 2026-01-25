using AspNetCore.Identity.MongoDbCore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.ViewModels;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    public class AccountController : Controller
{
    private readonly UserManager<Users> _userManager;
    private readonly SignInManager<Users> _signInManager;
    private readonly RoleManager<MongoIdentityRole> _roleManager;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AccountController> _logger;
    private readonly ISmsService _smsService;
    private readonly IOtpService _otpService;
    private readonly IFirebaseAuthService _firebaseAuthService;
    private readonly IIdVerificationService _idVerificationService;

    public AccountController(
        UserManager<Users> userManager,
        SignInManager<Users> signInManager,
        RoleManager<MongoIdentityRole> roleManager,
        IWebHostEnvironment env,
        IConfiguration configuration,
        ILogger<AccountController> logger,
        ISmsService smsService,
        IOtpService otpService,
        IFirebaseAuthService firebaseAuthService,
        IIdVerificationService idVerificationService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _env = env;
        _configuration = configuration;
        _logger = logger;
        _smsService = smsService;
        _otpService = otpService;
        _firebaseAuthService = firebaseAuthService;
        _idVerificationService = idVerificationService;
    }

    #region Registration
    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (ModelState.IsValid)
        {
            // Prevent admin registration through the form
            if (model.Role == RoleTypes.Admin)
            {
                ModelState.AddModelError(string.Empty, "Admin accounts cannot be created through registration.");
                return View(model);
            }

            var user = new Users
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.Name
            };

            // Handle profile image upload if provided
            if (model.Image != null && model.Image.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "images", "users");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"user_{Guid.NewGuid()}" + Path.GetExtension(model.Image.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.Image.CopyToAsync(fileStream);
                }

                var relativePath = $"/images/users/{uniqueFileName}";
                user.ProfileImageUrl = relativePath;
            }

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                // Create role if it doesn't exist
                if (!await _roleManager.RoleExistsAsync(model.Role))
                {
                    await _roleManager.CreateAsync(new MongoIdentityRole(model.Role));
                }

                // Assign role to user
                await _userManager.AddToRoleAsync(user, model.Role);

                // Redirect to login page after successful registration
                TempData["SuccessMessage"] = "Registration successful! Please login with your credentials.";
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        // If we got this far, something failed, redisplay form
        return View(model);
    }


    #endregion

    #region Login/Logout
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user != null)
            {
                // Check if user is suspended (lockout end in the future or MaxValue)
                if (await _userManager.GetLockoutEnabledAsync(user))
                {
                    var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                    if (lockoutEnd.HasValue && lockoutEnd.Value > DateTimeOffset.UtcNow)
                    {
                        ModelState.AddModelError(string.Empty, "Your account has been suspended by the administrator.");
                        return View(model);
                    }
                }

                var result = await _signInManager.PasswordSignInAsync(
                    model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    // Enforce profile completion and admin approval
                    if (!user.IsProfileCompleted)
                    {
                        return RedirectToAction("Profile");
                    }
                    if (!user.IsAdminApproved)
                    {
                        TempData["SuccessMessage"] = "Your profile is pending admin approval. Some features are disabled until approval.";
                    }

                    // If a valid local returnUrl is provided, honor it
                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    // Get user roles and redirect to role dashboards by default
                    var roles = await _userManager.GetRolesAsync(user);
                    if (roles.Contains(RoleTypes.Admin))
                        return RedirectToAction("Dashboard", "Admin");
                    if (roles.Contains(RoleTypes.ServiceProvider))
                        return RedirectToAction("Dashboard", "ServiceProvider");
                    if (roles.Contains(RoleTypes.Vendor))
                        return RedirectToAction("Dashboard", "Vendor");

                    // Regular users go to the user dashboard view
                    return RedirectToAction("Dashboard", "Home");
                }

                if (result.RequiresTwoFactor)
                {
                    return RedirectToAction("LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
                }

                if (result.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty, "Your account has been suspended by the administrator.");
                    return View(model);
                }
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }


    #endregion

    #region Helper Methods
    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public async Task<IActionResult> IsEmailAvailable(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return Json(user == null);
    }

    // When editing profile, allow the current user's email to be considered available
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> IsEmailAvailableForEdit(string email)
    {
        var current = await _userManager.GetUserAsync(User);
        if (current == null) return Json(false);
        var other = await _userManager.FindByEmailAsync(email);
        var ok = other == null || other.Id == current.Id;
        return Json(ok);
    }

    // Phone uniqueness checks
    [HttpGet]
    public async Task<IActionResult> IsPhoneAvailable(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return Json(false);
        var formatted = FormatPhoneNumber(phone);
        // Identity stores phone in User.PhoneNumber
        var allUsers = _userManager.Users.ToList();
        var exists = allUsers.Any(u => !string.IsNullOrEmpty(u.PhoneNumber) && FormatPhoneNumber(u.PhoneNumber) == formatted);
        return Json(!exists);
    }

    // When editing profile, allow the current user's phone to be considered available
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> IsPhoneAvailableForEdit(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return Json(false);
        var current = await _userManager.GetUserAsync(User);
        if (current == null) return Json(false);
        var formatted = FormatPhoneNumber(phone);
        var allUsers = _userManager.Users.ToList();
        var exists = allUsers.Any(u => !string.IsNullOrEmpty(u.PhoneNumber) && FormatPhoneNumber(u.PhoneNumber) == formatted && u.Id != current.Id);
        return Json(!exists);
    }
    #endregion

    #region Password Change (separate from profile)
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ChangePassword()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");
        var vm = new ChangePasswordViewModel { Email = user.Email ?? string.Empty };
        return View(vm);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        // Since this is a dedicated page, do NOT toggle profile approval flags here
        // Only set the new password using a reset token pathway or ChangePassword with current password if you require it
        // Here we will generate a reset token and apply it silently for the signed-in user
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }
        TempData["SuccessMessage"] = "Password changed successfully.";
        return RedirectToAction("Profile");
    }

    #endregion

    #region Additional Actions
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        if (user.IsAdminApproved && user.IsProfileCompleted)
        {
            // Approved users go to dashboard, not profile page
            return RedirectToAction("Index", "Home");
        }

        // Restrict navigation while profile is incomplete
        ViewBag.RestrictNav = !user.IsProfileCompleted;

        var model = new ProfileViewModel
        {
            Name = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            IsProfileCompleted = user.IsProfileCompleted,
            IsAdminApproved = user.IsAdminApproved,
            CanUploadIdentityProof = !user.IsAdminApproved,
            IdentityProofRequired = !user.IsAdminApproved && string.IsNullOrWhiteSpace(user.IdentityProofUrl),
            ProfileImageRequired = !user.IsAdminApproved && string.IsNullOrWhiteSpace(user.ProfileImageUrl),
            ExistingProfileImageUrl = user.ProfileImageUrl,
            ExistingIdentityProofUrl = user.IdentityProofUrl,
            // ID Verification status
            IsIdVerified = user.IsIdVerified,
            IsIdAutoApproved = user.IsIdAutoApproved,
            IdVerificationScore = user.IdVerificationScore,
            IdVerificationMessage = user.IdVerificationMessage,
            ExtractedNameFromId = user.ExtractedNameFromId
        };

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // Keep navbar restricted while profile incomplete
        ViewBag.RestrictNav = !user.IsProfileCompleted;

        if (!user.IsAdminApproved)
        {
            if (model.Image == null && string.IsNullOrWhiteSpace(user.ProfileImageUrl))
            {
                ModelState.AddModelError(nameof(model.Image), "Profile image is required.");
            }
            if (model.IdentityProof == null && string.IsNullOrWhiteSpace(user.IdentityProofUrl))
            {
                ModelState.AddModelError(nameof(model.IdentityProof), "Identity proof is required.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.IsProfileCompleted = user.IsProfileCompleted;
            model.IsAdminApproved = user.IsAdminApproved;
            model.CanUploadIdentityProof = !user.IsAdminApproved;
            model.IdentityProofRequired = !user.IsAdminApproved && string.IsNullOrWhiteSpace(user.IdentityProofUrl);
            model.ProfileImageRequired = !user.IsAdminApproved && string.IsNullOrWhiteSpace(user.ProfileImageUrl);
            model.ExistingProfileImageUrl = user.ProfileImageUrl;
            model.ExistingIdentityProofUrl = user.IdentityProofUrl;
            return View(model);
        }

        var adminApprovedBeforeUpdate = user.IsAdminApproved;
        var profileImageBeforeUpdate = user.ProfileImageUrl;
        var identityProofBeforeUpdate = user.IdentityProofUrl;

        // Update user properties
        user.FullName = model.Name;
        user.Email = model.Email;
        user.UserName = model.Email;
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : FormatPhoneNumber(model.PhoneNumber);
        user.Address = model.Address;

        // Handle profile image upload (allowed for all, optional after approval)
        if (model.Image != null && model.Image.Length > 0)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "images", "users");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = $"user_{Guid.NewGuid()}" + Path.GetExtension(model.Image.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await model.Image.CopyToAsync(fileStream);
            }
            user.ProfileImageUrl = $"/images/users/{uniqueFileName}";
        }
        else if (user.IsAdminApproved && string.IsNullOrWhiteSpace(model.ExistingProfileImageUrl) && string.IsNullOrWhiteSpace(user.ProfileImageUrl))
        {
            // Preserve existing image requirement status when none provided post-approval
            user.ProfileImageUrl = profileImageBeforeUpdate;
        }

        // Handle identity proof upload with OCR-based verification
        bool newIdUploaded = false;
        if (!user.IsAdminApproved && model.IdentityProof != null && model.IdentityProof.Length > 0)
        {
            var proofsFolder = Path.Combine(_env.WebRootPath, "uploads", "identity");
            if (!Directory.Exists(proofsFolder)) Directory.CreateDirectory(proofsFolder);
            var ext = Path.GetExtension(model.IdentityProof.FileName);
            var uniqueProofName = $"proof_{Guid.NewGuid()}{ext}";
            var proofPath = Path.Combine(proofsFolder, uniqueProofName);
            using (var proofStream = new FileStream(proofPath, FileMode.Create))
            {
                await model.IdentityProof.CopyToAsync(proofStream);
            }
            user.IdentityProofUrl = $"/uploads/identity/{uniqueProofName}";
            newIdUploaded = true;
            
            // Perform OCR-based ID verification (only for image files, not PDF)
            var isImageFile = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" }
                .Contains(ext.ToLowerInvariant());
            
            if (isImageFile)
            {
                try
                {
                    // Read the file and convert to base64 for OCR verification
                    using var memoryStream = new MemoryStream();
                    await model.IdentityProof.OpenReadStream().CopyToAsync(memoryStream);
                    var imageBase64 = Convert.ToBase64String(memoryStream.ToArray());
                    
                    _logger.LogInformation("Performing OCR verification for user: {UserName}", model.Name);
                    
                    var verificationResult = await _idVerificationService.VerifyIdentityBase64Async(
                        model.Name, 
                        imageBase64
                    );
                    
                    // Store verification results
                    user.IsIdVerified = verificationResult.Verified;
                    user.IsIdAutoApproved = verificationResult.AutoApproved;
                    user.IdVerificationScore = verificationResult.SimilarityScore;
                    user.IdVerificationMessage = verificationResult.Message;
                    user.ExtractedNameFromId = verificationResult.BestMatch?.ExtractedName;
                    user.IdVerifiedAtUtc = DateTime.UtcNow;
                    
                    _logger.LogInformation(
                        "ID verification result for {UserName}: Verified={Verified}, AutoApproved={AutoApproved}, Score={Score}, Message={Message}",
                        model.Name,
                        verificationResult.Verified,
                        verificationResult.AutoApproved,
                        verificationResult.SimilarityScore,
                        verificationResult.Message
                    );
                    
                    // If auto-approved, set admin approval
                    if (verificationResult.AutoApproved)
                    {
                        user.IsAdminApproved = true;
                        user.AdminReviewNote = $"Auto-approved via OCR verification. Name match: {Math.Round(verificationResult.SimilarityScore * 100, 1)}%. Extracted name: {verificationResult.BestMatch?.ExtractedName ?? "N/A"}";
                        user.AdminReviewedAtUtc = DateTime.UtcNow;
                        TempData["SuccessMessage"] = "Your ID has been verified successfully! Your account is now approved.";
                    }
                    else
                    {
                        TempData["InfoMessage"] = verificationResult.Message ?? "Your ID could not be auto-verified. It will be reviewed by an admin.";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during OCR verification for user: {UserName}", model.Name);
                    user.IsIdVerified = false;
                    user.IsIdAutoApproved = false;
                    user.IdVerificationMessage = "OCR verification failed. Your ID will be reviewed by an admin.";
                    TempData["InfoMessage"] = "ID verification service unavailable. Your ID will be reviewed by an admin.";
                }
            }
            else
            {
                // PDF files cannot be OCR processed, require admin review
                user.IsIdVerified = false;
                user.IsIdAutoApproved = false;
                user.IdVerificationMessage = "PDF documents require admin review for verification.";
                TempData["InfoMessage"] = "PDF documents require admin review. Please wait for approval.";
            }
        }

        var hasIdentityProof = !string.IsNullOrWhiteSpace(user.IdentityProofUrl);

        // Mark profile completed when mandatory fields present
        user.IsProfileCompleted = !string.IsNullOrWhiteSpace(user.Address) && 
                                  !string.IsNullOrWhiteSpace(user.PhoneNumber) &&
                                  !string.IsNullOrWhiteSpace(user.ProfileImageUrl) &&
                                  hasIdentityProof;
        // Reset approval only when identity proof is updated or approval not granted yet
        // Skip reset if the new ID was auto-approved
        if (!adminApprovedBeforeUpdate && !user.IsIdAutoApproved)
        {
            if (user.IsProfileCompleted)
            {
                user.IsAdminApproved = false;
                user.AdminReviewNote = null;
                user.AdminReviewedAtUtc = null;
            }
        }
        else if (identityProofBeforeUpdate != user.IdentityProofUrl && !string.IsNullOrWhiteSpace(user.IdentityProofUrl) && !user.IsIdAutoApproved)
        {
            user.IsAdminApproved = false;
            user.AdminReviewNote = null;
            user.AdminReviewedAtUtc = null;
        }

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            // Handle password change if provided
            if (!string.IsNullOrEmpty(model.NewPassword) && !string.IsNullOrEmpty(model.CurrentPassword))
            {
                var passwordResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
                if (!passwordResult.Succeeded)
                {
                    foreach (var error in passwordResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return View(model);
                }
            }

            // If user was auto-approved via OCR, redirect to dashboard
            if (user.IsAdminApproved && user.IsProfileCompleted)
            {
                TempData["SuccessMessage"] = "Your profile has been verified and approved! Welcome to ServConnect.";
                return RedirectToAction("Index", "Home");
            }
            
            TempData["SuccessMessage"] = "Profile updated successfully!";
            return RedirectToAction("Profile");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ProfileUpdate()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        if (!user.IsAdminApproved || !user.IsProfileCompleted)
        {
            return RedirectToAction(nameof(Profile));
        }

        var model = new ProfileUpdateViewModel
        {
            Name = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = NormalizePhoneNumberForForm(user.PhoneNumber),
            Address = user.Address,
            ExistingProfileImageUrl = user.ProfileImageUrl
        };

        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProfileUpdate(ProfileUpdateViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        if (!user.IsAdminApproved || !user.IsProfileCompleted)
        {
            return RedirectToAction(nameof(Profile));
        }

        if (!ModelState.IsValid)
        {
            model.ExistingProfileImageUrl = user.ProfileImageUrl;
            return View(model);
        }

        var normalizedPhone = FormatPhoneNumber(model.PhoneNumber);
        var emailOwner = await _userManager.FindByEmailAsync(model.Email);
        if (emailOwner != null && emailOwner.Id != user.Id)
        {
            ModelState.AddModelError(nameof(model.Email), "This email address is already in use.");
        }

        var otherUsers = _userManager.Users.ToList();
        var duplicatePhoneUser = otherUsers
            .Where(u => !string.IsNullOrWhiteSpace(u.PhoneNumber) && u.Id != user.Id)
            .FirstOrDefault(u => FormatPhoneNumber(u.PhoneNumber) == normalizedPhone);

        if (duplicatePhoneUser != null)
        {
            ModelState.AddModelError(nameof(model.PhoneNumber), "This phone number is already registered with another account.");
        }

        if (!ModelState.IsValid)
        {
            model.ExistingProfileImageUrl = user.ProfileImageUrl;
            return View(model);
        }

        user.FullName = model.Name;
        user.Email = model.Email;
        user.UserName = model.Email;
        user.PhoneNumber = normalizedPhone;
        user.Address = model.Address;

        if (model.Image != null && model.Image.Length > 0)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "images", "users");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"user_{Guid.NewGuid()}" + Path.GetExtension(model.Image.FileName);
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await model.Image.CopyToAsync(fileStream);
            }

            user.ProfileImageUrl = $"/images/users/{uniqueFileName}";
            model.ExistingProfileImageUrl = user.ProfileImageUrl;
        }

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Profile updated successfully.";
            return RedirectToAction(nameof(ProfileUpdate));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        model.ExistingProfileImageUrl = user.ProfileImageUrl;
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Lockout()
    {
        return View();
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> PendingApproval()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login");
        }
        if (!user.IsProfileCompleted)
        {
            return RedirectToAction("Profile");
        }
        if (user.IsAdminApproved)
        {
            return RedirectToAction("Dashboard", "Home");
        }
        ViewBag.RestrictNav = true; // Lock down navigation while pending
        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult GoogleAuthDiagnostic()
    {
        var clientId = _configuration["Authentication:Google:ClientId"];
        var clientSecret = _configuration["Authentication:Google:ClientSecret"];
        var hasClientId = !string.IsNullOrEmpty(clientId) && clientId != "YOUR_NEW_CLIENT_ID_HERE";
        var hasClientSecret = !string.IsNullOrEmpty(clientSecret) && clientSecret != "YOUR_NEW_CLIENT_SECRET_HERE";
        
        var diagnosticInfo = new
        {
            ClientIdConfigured = hasClientId,
            ClientSecretConfigured = hasClientSecret,
            ClientIdValue = hasClientId ? $"{clientId[..10]}..." : "Not configured",
            ExpectedRedirectUris = new[]
            {
                "https://localhost:7213/signin-google",
                "http://localhost:5227/signin-google"
            },
            CurrentUrl = $"{Request.Scheme}://{Request.Host}",
            GoogleCallbackPath = "/signin-google"
        };
        
        return Json(diagnosticInfo);
    }
    #endregion

    #region Forgot Password & OTP Reset
    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Format the input phone number to match the stored format
        var formattedInputNumber = FormatPhoneNumber(model.PhoneNumber);
        _logger.LogInformation("Looking for user with phone number: {InputNumber} (formatted: {FormattedNumber})", 
            model.PhoneNumber, formattedInputNumber);
        
        var user = _userManager.Users.FirstOrDefault(u => u.PhoneNumber == formattedInputNumber);
        if (user == null)
        {
            _logger.LogWarning("No user found with phone number: {FormattedNumber}", formattedInputNumber);
            // Don't reveal that the user does not exist
            TempData["SuccessMessage"] = "If a user with this phone number exists, an OTP has been sent.";
            return RedirectToAction("ResetPassword");
        }
        
        _logger.LogInformation("Found user: {UserId} with phone number: {PhoneNumber}", user.Id, user.PhoneNumber);

        // Check if user has requested OTP recently (rate limiting)
        if (user.LastOtpRequestTime.HasValue && 
            DateTime.UtcNow.Subtract(user.LastOtpRequestTime.Value).TotalMinutes < 1)
        {
            ModelState.AddModelError(string.Empty, "Please wait at least 1 minute before requesting another OTP.");
            return View(model);
        }

        // Generate OTP
        var otp = _otpService.GenerateOtp();
        var expiryTime = _otpService.GetOtpExpiryTime();

        // Update user with OTP details
        user.PasswordResetOtp = otp;
        user.OtpExpiryTime = expiryTime;
        user.OtpAttempts = 0;
        user.LastOtpRequestTime = DateTime.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Failed to generate OTP. Please try again.");
            return View(model);
        }

        // Debug logging
        _logger.LogInformation("OTP stored in database for user {UserId}: {Otp}, Expiry: {ExpiryTime}", 
            user.Id, otp, expiryTime);

        // Send OTP via SMS
        _logger.LogInformation("Attempting to send OTP to phone number: {PhoneNumber}", model.PhoneNumber);
        
        // Format phone number for SMS
        var formattedPhoneNumber = FormatPhoneNumber(model.PhoneNumber);
        _logger.LogInformation("Formatted phone number for SMS: {FormattedPhoneNumber}", formattedPhoneNumber);
        
        var smsResult = await _smsService.SendOtpAsync(formattedPhoneNumber, otp);
        if (!smsResult)
        {
            _logger.LogError("Failed to send OTP SMS to {PhoneNumber} (formatted: {FormattedPhoneNumber})", model.PhoneNumber, formattedPhoneNumber);
            ModelState.AddModelError(string.Empty, "Failed to send OTP. Please try again.");
            return View(model);
        }

        TempData["SuccessMessage"] = "OTP has been sent to your phone number.";
        return RedirectToAction("ResetPassword", new { phoneNumber = model.PhoneNumber });
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string phoneNumber = "")
    {
        var model = new ResetPasswordViewModel
        {
            PhoneNumber = phoneNumber
        };
        return View(model);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = _userManager.Users.FirstOrDefault(u => u.PhoneNumber == model.PhoneNumber);
        if (user == null)
        {
            // Try with formatted phone number as fallback
            var formattedPhoneNumber = FormatPhoneNumber(model.PhoneNumber);
            user = _userManager.Users.FirstOrDefault(u => u.PhoneNumber == formattedPhoneNumber);
            
            if (user == null)
            {
                _logger.LogWarning("User not found with phone number: {PhoneNumber} or formatted: {FormattedPhoneNumber}", 
                    model.PhoneNumber, formattedPhoneNumber);
                ModelState.AddModelError(string.Empty, "Invalid phone number or OTP.");
                return View(model);
            }
        }
        
        _logger.LogInformation("Found user {UserId} with phone number: {PhoneNumber} for OTP verification", 
            user.Id, user.PhoneNumber);

        // Check OTP attempts (max 3 attempts)
        if (user.OtpAttempts >= 3)
        {
            ModelState.AddModelError(string.Empty, "Too many failed attempts. Please request a new OTP.");
            return View(model);
        }

        // Validate OTP - try database first, then Fast2SMS cache as fallback
        bool isOtpValid = _otpService.ValidateOtp(model.Otp, user.PasswordResetOtp, user.OtpExpiryTime);
        
        // If database OTP validation fails, try Fast2SMS cache (for simulation mode)
        if (!isOtpValid && _smsService is Fast2SmsOtpService fast2SmsService)
        {
            var formattedPhoneNumber = FormatPhoneNumber(model.PhoneNumber);
            isOtpValid = fast2SmsService.VerifyOtp(formattedPhoneNumber, model.Otp);
            _logger.LogInformation("Tried Fast2SMS cache validation for {PhoneNumber}: {IsValid}", formattedPhoneNumber, isOtpValid);
        }
        
        if (!isOtpValid)
        {
            user.OtpAttempts++;
            await _userManager.UpdateAsync(user);
            
            var remainingAttempts = 3 - user.OtpAttempts;
            if (remainingAttempts > 0)
            {
                ModelState.AddModelError(string.Empty, $"Invalid OTP. {remainingAttempts} attempts remaining.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Too many failed attempts. Please request a new OTP.");
            }
            
            // Debug logging
            _logger.LogWarning("OTP validation failed for user {UserId}. Provided: {ProvidedOtp}, Stored: {StoredOtp}, Expiry: {ExpiryTime}", 
                user.Id, model.Otp, user.PasswordResetOtp, user.OtpExpiryTime);
            
            return View(model);
        }

        // Reset password
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

        if (result.Succeeded)
        {
            // Clear OTP data
            user.PasswordResetOtp = null;
            user.OtpExpiryTime = null;
            user.OtpAttempts = 0;
            user.LastOtpRequestTime = null;
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = "Password has been reset successfully. Please login with your new password.";
            return RedirectToAction("Login");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendOtp(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber))
        {
            return Json(new { success = false, message = "Phone number is required." });
        }

        // Format the input phone number to match the stored format
        var formattedInputNumber = FormatPhoneNumber(phoneNumber);
        _logger.LogInformation("Looking for user to resend OTP: {InputNumber} (formatted: {FormattedNumber})", 
            phoneNumber, formattedInputNumber);
        
        var user = _userManager.Users.FirstOrDefault(u => u.PhoneNumber == formattedInputNumber);
        if (user == null)
        {
            _logger.LogWarning("No user found for resend OTP with phone number: {FormattedNumber}", formattedInputNumber);
            return Json(new { success = false, message = "User not found." });
        }

        // Check rate limiting
        if (user.LastOtpRequestTime.HasValue && 
            DateTime.UtcNow.Subtract(user.LastOtpRequestTime.Value).TotalMinutes < 1)
        {
            return Json(new { success = false, message = "Please wait at least 1 minute before requesting another OTP." });
        }

        // Generate new OTP
        var otp = _otpService.GenerateOtp();
        var expiryTime = _otpService.GetOtpExpiryTime();

        // Update user
        user.PasswordResetOtp = otp;
        user.OtpExpiryTime = expiryTime;
        user.OtpAttempts = 0;
        user.LastOtpRequestTime = DateTime.UtcNow;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Json(new { success = false, message = "Failed to generate OTP." });
        }

        // Send OTP
        _logger.LogInformation("Attempting to resend OTP to phone number: {PhoneNumber}", phoneNumber);
        
        // Format phone number for SMS
        var formattedPhoneNumber = FormatPhoneNumber(phoneNumber);
        _logger.LogInformation("Formatted phone number for SMS: {FormattedPhoneNumber}", formattedPhoneNumber);
        
        var smsResult = await _smsService.SendOtpAsync(formattedPhoneNumber, otp);
        if (!smsResult)
        {
            _logger.LogError("Failed to resend OTP SMS to {PhoneNumber} (formatted: {FormattedPhoneNumber})", phoneNumber, formattedPhoneNumber);
            return Json(new { success = false, message = "Failed to send OTP." });
        }

        return Json(new { success = true, message = "New OTP has been sent." });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> TestFast2SMS(string phoneNumber = "+919876543210")
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            // Format phone number using the comprehensive formatter
            phoneNumber = FormatPhoneNumber(phoneNumber);
            
            // Log the test attempt
            _logger.LogInformation("Testing Fast2SMS OTP with phone number: {PhoneNumber}", phoneNumber);
            
            // Generate a test OTP
            var testOtp = _otpService.GenerateOtp();
            var result = await _smsService.SendOtpAsync(phoneNumber, testOtp);
            
            var response = new { 
                success = result, 
                message = result ? "Test OTP sent successfully via Fast2SMS! Check your phone." : "Failed to send test OTP. Check logs for details.",
                phoneNumber = phoneNumber,
                testOtp = result ? testOtp : "N/A", // Include OTP in response for testing
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                service = "Fast2SMS OTP Service"
            };
            
            _logger.LogInformation("Fast2SMS test result: {Result}, OTP: {Otp}", result, testOtp);
            return Json(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Fast2SMS OTP");
            return Json(new { 
                success = false, 
                message = $"Error: {ex.Message}",
                phoneNumber = phoneNumber,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                service = "Fast2SMS OTP Service"
            });
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> TestFast2SMSSimple()
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            // Test with a hardcoded number
            var phoneNumber = "+919744892806";
            
            _logger.LogInformation("Testing Fast2SMS OTP with hardcoded phone number: {PhoneNumber}", phoneNumber);
            
            // Generate a test OTP
            var testOtp = _otpService.GenerateOtp();
            var result = await _smsService.SendOtpAsync(phoneNumber, testOtp);
            
            var response = new { 
                success = result, 
                message = result ? "Test OTP sent successfully via Fast2SMS! Check your phone." : "Failed to send test OTP. Check logs for details.",
                phoneNumber = phoneNumber,
                testOtp = result ? testOtp : "N/A", // Include OTP in response for testing
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                service = "Fast2SMS OTP Service"
            };
            
            _logger.LogInformation("Fast2SMS test result: {Result}, OTP: {Otp}", result, testOtp);
            return Json(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Fast2SMS OTP");
            return Json(new { 
                success = false, 
                message = $"Error: {ex.Message}",
                phoneNumber = "+919744892806",
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                service = "Fast2SMS OTP Service"
            });
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult TestPhoneFormatting(string phoneNumber = "9744892806")
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        var formatted = FormatPhoneNumber(phoneNumber);
        
        // Also check if a user exists with this formatted number
        var userExists = _userManager.Users.Any(u => u.PhoneNumber == formatted);
        
        return Json(new { 
            original = phoneNumber,
            formatted = formatted,
            userExists = userExists,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult TestUserLookup(string phoneNumber = "9744892806")
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        var formatted = FormatPhoneNumber(phoneNumber);
        var user = _userManager.Users.FirstOrDefault(u => u.PhoneNumber == formatted);
        
        return Json(new { 
            inputNumber = phoneNumber,
            formattedNumber = formatted,
            userFound = user != null,
            storedPhoneNumber = user?.PhoneNumber,
            userEmail = user?.Email,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult TestOtpVerification(string phoneNumber = "9744892806", string otp = "")
    {
        if (!_env.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            var formattedPhoneNumber = FormatPhoneNumber(phoneNumber);
            var user = _userManager.Users.FirstOrDefault(u => u.PhoneNumber == formattedPhoneNumber);
            
            if (user == null)
            {
                return Json(new { 
                    success = false,
                    message = "User not found",
                    phoneNumber = phoneNumber,
                    formattedPhoneNumber = formattedPhoneNumber,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
            }

            // Check database OTP
            bool dbOtpValid = !string.IsNullOrEmpty(otp) && 
                             _otpService.ValidateOtp(otp, user.PasswordResetOtp, user.OtpExpiryTime);

            // Check Fast2SMS cache OTP
            bool cacheOtpValid = false;
            if (!dbOtpValid && !string.IsNullOrEmpty(otp) && _smsService is Fast2SmsOtpService fast2SmsService)
            {
                cacheOtpValid = fast2SmsService.VerifyOtp(formattedPhoneNumber, otp);
            }

            return Json(new { 
                success = dbOtpValid || cacheOtpValid,
                message = dbOtpValid ? "Database OTP valid" : (cacheOtpValid ? "Cache OTP valid" : "OTP invalid or not provided"),
                phoneNumber = phoneNumber,
                formattedPhoneNumber = formattedPhoneNumber,
                userFound = true,
                storedOtp = user.PasswordResetOtp,
                otpExpiry = user.OtpExpiryTime,
                providedOtp = otp,
                dbOtpValid = dbOtpValid,
                cacheOtpValid = cacheOtpValid,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }
        catch (Exception ex)
        {
            return Json(new { 
                success = false,
                message = $"Error: {ex.Message}",
                phoneNumber = phoneNumber,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }
    }
    #endregion

    #region Helper Methods
    
    /// <summary>
    /// Formats phone number to E.164 format for international SMS
    /// </summary>
    /// <param name="phoneNumber">Input phone number</param>
    /// <returns>Formatted phone number in E.164 format</returns>
    private string FormatPhoneNumber(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return phoneNumber ?? string.Empty;

        // Clean the phone number - remove spaces, dashes, parentheses
        var cleaned = phoneNumber.Trim()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace(".", "");

        // If already in E.164 format (starts with +), return as is
        if (cleaned.StartsWith("+"))
        {
            return cleaned;
        }

        // Remove leading zeros or country exit codes
        cleaned = cleaned.TrimStart('0');
        if (cleaned.StartsWith("00"))
        {
            cleaned = cleaned.Substring(2);
        }

        // Indian numbers (10 digits, can start with 6, 7, 8, 9)
        if (cleaned.Length == 10 && (cleaned.StartsWith("6") || cleaned.StartsWith("7") || 
                                     cleaned.StartsWith("8") || cleaned.StartsWith("9")))
        {
            return "+91" + cleaned;
        }

        // US/Canada numbers (10 digits, typically start with 2-9)
        if (cleaned.Length == 10 && char.IsDigit(cleaned[0]) && cleaned[0] >= '2')
        {
            return "+1" + cleaned;
        }

        // US/Canada numbers with country code (11 digits starting with 1)
        if (cleaned.Length == 11 && cleaned.StartsWith("1"))
        {
            return "+" + cleaned;
        }

        // UK numbers (10-11 digits)
        if (cleaned.Length >= 10 && cleaned.Length <= 11 && 
            (cleaned.StartsWith("44") || cleaned.Length == 10))
        {
            if (cleaned.StartsWith("44"))
                return "+" + cleaned;
            else
                return "+44" + cleaned;
        }

        // Australia numbers (9 digits starting with 4)
        if (cleaned.Length == 9 && cleaned.StartsWith("4"))
        {
            return "+61" + cleaned;
        }

        // Germany numbers (10-12 digits)
        if (cleaned.Length >= 10 && cleaned.Length <= 12 && 
            (cleaned.StartsWith("49") || cleaned.StartsWith("1") || cleaned.StartsWith("3")))
        {
            if (cleaned.StartsWith("49"))
                return "+" + cleaned;
            else
                return "+49" + cleaned;
        }

        // France numbers (9-10 digits)
        if (cleaned.Length >= 9 && cleaned.Length <= 10 && 
            (cleaned.StartsWith("33") || cleaned.StartsWith("1") || cleaned.StartsWith("6") || cleaned.StartsWith("7")))
        {
            if (cleaned.StartsWith("33"))
                return "+" + cleaned;
            else
                return "+33" + cleaned;
        }

        // If we can't determine the country, assume it's already in international format
        // or add + if it looks like an international number
        if (cleaned.Length >= 10 && cleaned.Length <= 15)
        {
            return "+" + cleaned;
        }

        // Return original if we can't format it
        _logger?.LogWarning("Could not format phone number: {PhoneNumber}", phoneNumber);
        return phoneNumber;
    }

    private string? NormalizePhoneNumberForForm(string? storedPhoneNumber)
    {
        if (string.IsNullOrWhiteSpace(storedPhoneNumber))
        {
            return storedPhoneNumber;
        }

        // For now, strip the +91 prefix to keep the UI consistent with 10-digit entry
        if (storedPhoneNumber.StartsWith("+91") && storedPhoneNumber.Length == 13)
        {
            return storedPhoneNumber.Substring(3);
        }

        return storedPhoneNumber;
    }
    
    #endregion

    #region Firebase Authentication
    
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> FirebaseLogin([FromBody] FirebaseLoginViewModel model)
    {
        try
        {
            _logger.LogInformation("Firebase login attempt started");
            
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning("Firebase login model validation failed: {Errors}", string.Join(", ", errors));
                return Json(new { success = false, message = "Invalid request data: " + string.Join(", ", errors) });
            }

            _logger.LogInformation("Verifying Firebase token...");
            // Verify Firebase token
            var decodedToken = await _firebaseAuthService.VerifyTokenAsync(model.IdToken);
            _logger.LogInformation("Firebase token verified for email: {Email}", decodedToken.Claims["email"]);
            
            // Check if user exists in our system
            var userEmail = decodedToken.Claims["email"].ToString();
            var user = await _userManager.FindByEmailAsync(userEmail);
            _logger.LogInformation("User lookup result for {Email}: {Found}", userEmail, user != null ? "Found" : "Not Found");
            
            if (user == null)
            {
                // User doesn't exist, return registration required
                _logger.LogInformation("User not found, requiring registration for: {Email}", userEmail);
                return Json(new { 
                    success = false, 
                    requiresRegistration = true, 
                    email = userEmail,
                    name = decodedToken.Claims.ContainsKey("name") ? decodedToken.Claims["name"].ToString() : "",
                    message = "Account not found. Please complete registration." 
                });
            }

            // Block sign-in if account is suspended (lockout end in the future)
            if (await _userManager.GetLockoutEnabledAsync(user))
            {
                var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                if (lockoutEnd.HasValue && lockoutEnd.Value > DateTimeOffset.UtcNow)
                {
                    _logger.LogWarning("Firebase login blocked: user {Email} is suspended until {End}", userEmail, lockoutEnd);
                    return Json(new { success = false, message = "Your account has been suspended by the administrator." });
                }
            }

            // Sign in the user
            _logger.LogInformation("Signing in user: {Email}", userEmail);
            await _signInManager.SignInAsync(user, isPersistent: false);
            
            // Get user roles for redirection
            var roles = await _userManager.GetRolesAsync(user);
            _logger.LogInformation("User roles for {Email}: {Roles}", userEmail, string.Join(", ", roles));
            
            string redirectUrl;
            
            if (roles.Contains(RoleTypes.Admin))
            {
                redirectUrl = "/Admin/Dashboard";
            }
            else if (roles.Contains(RoleTypes.ServiceProvider))
            {
                redirectUrl = "/ServiceProvider/Dashboard";
            }
            else if (roles.Contains(RoleTypes.Vendor))
            {
                redirectUrl = "/Vendor/Dashboard";
            }
            else if (!string.IsNullOrEmpty(model.ReturnUrl))
            {
                redirectUrl = model.ReturnUrl;
            }
            else
            {
                redirectUrl = "/";
            }

            _logger.LogInformation("Firebase login successful for {Email}, redirecting to: {RedirectUrl}", userEmail, redirectUrl);
            return Json(new { success = true, redirectUrl = redirectUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Firebase login failed with exception");
            return Json(new { success = false, message = "Authentication failed. Please try again. Error: " + ex.Message });
        }
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> FirebaseRegister([FromBody] FirebaseRegisterViewModel model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return Json(new { success = false, message = string.Join(", ", errors) });
            }

            // Prevent admin registration through Firebase
            if (model.Role == RoleTypes.Admin)
            {
                return Json(new { success = false, message = "Admin accounts cannot be created through registration." });
            }

            // Verify Firebase token
            var decodedToken = await _firebaseAuthService.VerifyTokenAsync(model.IdToken);
            
            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                return Json(new { success = false, message = "An account with this email already exists." });
            }

            // Format phone number
            var formattedPhoneNumber = !string.IsNullOrEmpty(model.PhoneNumber) 
                ? FormatPhoneNumber(model.PhoneNumber) 
                : null;

            // Create user in our system
            var user = new Users
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.Name,
                PhoneNumber = formattedPhoneNumber,
                Address = model.Address,
                EmailConfirmed = true, // Firebase handles email verification
                FirebaseUid = decodedToken.Uid // Store Firebase UID for future reference
            };

            // Try to set profile image from Firebase token claims (picture)
            string? profilePicture = null;
            if (decodedToken.Claims.ContainsKey("picture"))
            {
                profilePicture = decodedToken.Claims["picture"]?.ToString();
            }
            if (string.IsNullOrWhiteSpace(profilePicture))
            {
                // Fallback to client-provided PhotoUrl
                profilePicture = model.PhotoUrl;
            }
            if (!string.IsNullOrWhiteSpace(profilePicture))
            {
                user.ProfileImageUrl = profilePicture;
            }

            var result = await _userManager.CreateAsync(user);

            if (result.Succeeded)
            {
                // Create role if it doesn't exist
                if (!await _roleManager.RoleExistsAsync(model.Role))
                {
                    await _roleManager.CreateAsync(new MongoIdentityRole(model.Role));
                }

                // Assign role to user
                await _userManager.AddToRoleAsync(user, model.Role);

                // Sign in the user
                await _signInManager.SignInAsync(user, isPersistent: false);

                string redirectUrl;
                
                if (model.Role == RoleTypes.ServiceProvider)
                {
                    redirectUrl = "/ServiceProvider/Dashboard";
                }
                else if (model.Role == RoleTypes.Vendor)
                {
                    redirectUrl = "/Vendor/Dashboard";
                }
                else if (!string.IsNullOrEmpty(model.ReturnUrl))
                {
                    redirectUrl = model.ReturnUrl;
                }
                else
                {
                    redirectUrl = "/";
                }

                return Json(new { success = true, redirectUrl = redirectUrl });
            }

            var errorMessages = result.Errors.Select(e => e.Description);
            return Json(new { success = false, message = string.Join(", ", errorMessages) });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Firebase registration failed");
            return Json(new { success = false, message = "Registration failed. Please try again." });
        }
    }

    [HttpGet]
    public IActionResult GetFirebaseConfig()
    {
        var config = new
        {
            apiKey = _configuration["Firebase:WebApiKey"],
            authDomain = _configuration["Firebase:AuthDomain"],
            projectId = _configuration["Firebase:ProjectId"],
            storageBucket = _configuration["Firebase:StorageBucket"],
            messagingSenderId = _configuration["Firebase:MessagingSenderId"],
            appId = _configuration["Firebase:AppId"],
            measurementId = _configuration["Firebase:MeasurementId"]
        };
        
        return Json(config);
    }
    
    [HttpGet]
    public IActionResult FirebaseTest()
    {
        return View();
    }
    
    #endregion
    }
}