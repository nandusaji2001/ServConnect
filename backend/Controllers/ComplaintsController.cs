using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;
using ServConnect.ViewModels;

namespace ServConnect.Controllers
{
    [Authorize]
    public class ComplaintsController : Controller
    {
        private readonly IComplaintService _complaintService;
        private readonly UserManager<Users> _userManager;
        private readonly IWebHostEnvironment _env;

        public ComplaintsController(IComplaintService complaintService, UserManager<Users> userManager, IWebHostEnvironment env)
        {
            _complaintService = complaintService;
            _userManager = userManager;
            _env = env;
        }

        // Shared complaint form for all roles
        [HttpGet]
        public async Task<IActionResult> Create(string? role = null, Guid? providerId = null, string? providerName = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();
            var roles = await _userManager.GetRolesAsync(user);
            var effectiveRole = role ?? roles.FirstOrDefault() ?? RoleTypes.User;

            var vm = new ComplaintCreateViewModel
            {
                Name = user.FullName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                ComplainantId = user.Id,
                Role = effectiveRole,
                ServiceProviderId = providerId,
                ServiceProviderName = providerName
            };
            return View("~/Views/Complaints/Create.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ComplaintCreateViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/Complaints/Create.cshtml", vm);
            }

            // Build model
            var complaint = new Complaint
            {
                ComplainantId = vm.ComplainantId,
                ComplainantName = vm.Name,
                ComplainantEmail = vm.Email,
                ComplainantPhone = vm.Phone,
                ComplainantRole = vm.Role,
                ServiceProviderId = vm.ServiceProviderId,
                ServiceProviderName = vm.ServiceProviderName,
                ServiceType = vm.ServiceType,
                Category = vm.Category,
                OtherCategoryText = vm.OtherCategoryText,
                Description = vm.Description,
            };

            // Save first to get Id for evidence path
            await _complaintService.CreateAsync(complaint);

            // Handle evidence upload (store under wwwroot/uploads/complaints/{id}/)
            if (vm.EvidenceFiles?.Any() == true)
            {
                var baseFolder = Path.Combine(_env.WebRootPath, "uploads", "complaints", complaint.Id);
                if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);

                foreach (var file in vm.EvidenceFiles)
                {
                    if (file.Length <= 0) continue;
                    var safeName = Path.GetFileName(file.FileName);
                    var savePath = Path.Combine(baseFolder, safeName);
                    using (var fs = System.IO.File.Create(savePath))
                    {
                        await file.CopyToAsync(fs);
                    }
                    var relUrl = $"/uploads/complaints/{complaint.Id}/{safeName}";
                    await _complaintService.AddEvidenceAsync(complaint.Id, relUrl);
                }
            }

            TempData["ComplaintMessage"] = "Your complaint has been submitted successfully.";
            return RedirectToAction(nameof(ThankYou));
        }

        [HttpGet]
        public IActionResult ThankYou()
        {
            return View("~/Views/Complaints/ThankYou.cshtml");
        }
    }
}