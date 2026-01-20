using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;
using ServConnect.ViewModels;

namespace ServConnect.Controllers
{
    [Authorize]
    public class LostAndFoundController : Controller
    {
        private readonly ILostAndFoundService _lostFoundService;
        private readonly INotificationService _notificationService;
        private readonly IItemMatchingService _itemMatchingService;
        private readonly UserManager<Users> _userManager;
        private readonly IWebHostEnvironment _env;

        public LostAndFoundController(
            ILostAndFoundService lostFoundService,
            INotificationService notificationService,
            IItemMatchingService itemMatchingService,
            UserManager<Users> userManager,
            IWebHostEnvironment env)
        {
            _lostFoundService = lostFoundService;
            _notificationService = notificationService;
            _itemMatchingService = itemMatchingService;
            _userManager = userManager;
            _env = env;
        }

        // GET: Browse all available items
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? category = null)
        {
            var items = await _lostFoundService.GetAllItemsAsync(category, LostFoundItemStatus.Available);
            var vm = new LostFoundListViewModel
            {
                Items = items,
                CategoryFilter = category,
                TotalCount = items.Count
            };
            return View(vm);
        }

        // GET: Item details
        [AllowAnonymous]
        public async Task<IActionResult> Details(string id)
        {
            var item = await _lostFoundService.GetItemByIdAsync(id);
            if (item == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var vm = new LostFoundItemDetailsViewModel
            {
                Item = item,
                IsFoundUser = user != null && item.FoundByUserId == user.Id,
                CanClaim = false
            };

            if (user != null)
            {
                // Check if user is blocked
                var isBlocked = await _lostFoundService.IsUserBlockedForItemAsync(user.Id, id);
                if (isBlocked)
                {
                    vm.BlockedMessage = "You have been blocked from claiming this item due to repeated incorrect claims.";
                }
                else if (!vm.IsFoundUser && item.Status == LostFoundItemStatus.Available)
                {
                    // Check if user already has an active claim
                    var existingClaim = await _lostFoundService.GetActiveClaimForUserAndItemAsync(user.Id, id);
                    vm.UserClaim = existingClaim;
                    vm.CanClaim = existingClaim == null;
                }

                // If found user, load pending claims
                if (vm.IsFoundUser)
                {
                    vm.PendingClaims = await _lostFoundService.GetClaimsForItemAsync(id);
                }
            }

            return View(vm);
        }

        // GET: Report found item form
        public IActionResult ReportFound()
        {
            return View(new ReportFoundItemViewModel());
        }

        // POST: Submit found item
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportFound(ReportFoundItemViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (vm.Images == null || vm.Images.Count == 0)
            {
                ModelState.AddModelError("Images", "At least one image is required");
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var item = new LostFoundItem
            {
                Title = vm.Title,
                Category = vm.Category,
                Description = vm.Description,
                Condition = vm.Condition,
                FoundDate = vm.FoundDate,
                FoundLocation = vm.FoundLocation,
                FoundLocationDetails = vm.FoundLocationDetails,
                FoundByUserId = user.Id,
                FoundByUserName = user.FullName ?? user.UserName ?? "Unknown",
                FoundByUserEmail = user.Email ?? string.Empty,
                FoundByUserPhone = user.PhoneNumber
            };

            await _lostFoundService.CreateItemAsync(item);

            // Handle image uploads
            if (vm.Images?.Count > 0)
            {
                var baseFolder = Path.Combine(_env.WebRootPath, "uploads", "lostfound", item.Id);
                if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);

                foreach (var file in vm.Images)
                {
                    if (file.Length <= 0) continue;
                    var ext = Path.GetExtension(file.FileName);
                    var safeName = $"{Guid.NewGuid()}{ext}";
                    var savePath = Path.Combine(baseFolder, safeName);
                    using (var fs = System.IO.File.Create(savePath))
                    {
                        await file.CopyToAsync(fs);
                    }
                    var relUrl = $"/uploads/lostfound/{item.Id}/{safeName}";
                    await _lostFoundService.AddItemImageAsync(item.Id, relUrl);
                }
            }

            TempData["SuccessMessage"] = "Item reported successfully! It is now visible to others who may have lost it.";
            
            // Check for matching lost item reports using S-BERT ML model
            _ = Task.Run(async () =>
            {
                try
                {
                    var activeLostReports = await _lostFoundService.GetAllLostReportsAsync(status: LostItemStatus.Active);
                    if (activeLostReports.Any())
                    {
                        var matches = await _itemMatchingService.FindMatchingLostItemsAsync(item, activeLostReports, threshold: 0.5);
                        foreach (var match in matches)
                        {
                            // Notify the lost item owner about potential match
                            await _notificationService.CreateNotificationAsync(
                                match.UserId,
                                "Potential Match Found!",
                                $"A found item '{item.Title}' ({match.MatchPercentage}% match) might be your lost '{match.Title}'. Check it out!",
                                NotificationType.PotentialItemMatch,
                                item.Id,
                                $"/LostAndFound/Details/{item.Id}"
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail the main operation
                    Console.WriteLine($"Item matching error: {ex.Message}");
                }
            });
            
            return RedirectToAction(nameof(MyFoundItems));
        }

        // GET: Claim item form
        public async Task<IActionResult> Claim(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var item = await _lostFoundService.GetItemByIdAsync(id);
            if (item == null) return NotFound();

            // Check if user is the finder
            if (item.FoundByUserId == user.Id)
            {
                TempData["ErrorMessage"] = "You cannot claim an item you found.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Check if blocked
            if (await _lostFoundService.IsUserBlockedForItemAsync(user.Id, id))
            {
                TempData["ErrorMessage"] = "You have been blocked from claiming this item.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Check for existing claim
            var existingClaim = await _lostFoundService.GetActiveClaimForUserAndItemAsync(user.Id, id);
            if (existingClaim != null)
            {
                TempData["ErrorMessage"] = "You already have an active claim for this item.";
                return RedirectToAction(nameof(MyClaims));
            }

            var vm = new ClaimItemViewModel
            {
                ItemId = id,
                Item = item
            };

            return View(vm);
        }

        // POST: Submit claim
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Claim(ClaimItemViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var item = await _lostFoundService.GetItemByIdAsync(vm.ItemId);
            if (item == null) return NotFound();

            vm.Item = item;

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            // Validate again
            if (item.FoundByUserId == user.Id)
            {
                ModelState.AddModelError("", "You cannot claim an item you found.");
                return View(vm);
            }

            if (await _lostFoundService.IsUserBlockedForItemAsync(user.Id, vm.ItemId))
            {
                ModelState.AddModelError("", "You have been blocked from claiming this item.");
                return View(vm);
            }

            var claim = new ItemClaim
            {
                ItemId = vm.ItemId,
                ClaimantId = user.Id,
                ClaimantName = user.FullName ?? user.UserName ?? "Unknown",
                ClaimantEmail = user.Email ?? string.Empty,
                ClaimantPhone = user.PhoneNumber,
                PrivateOwnershipDetails = vm.PrivateOwnershipDetails
            };

            await _lostFoundService.CreateClaimAsync(claim);

            // Handle proof uploads
            if (vm.ProofImages?.Count > 0)
            {
                var baseFolder = Path.Combine(_env.WebRootPath, "uploads", "lostfound", "claims", claim.Id);
                if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);

                foreach (var file in vm.ProofImages)
                {
                    if (file.Length <= 0) continue;
                    var ext = Path.GetExtension(file.FileName);
                    var safeName = $"{Guid.NewGuid()}{ext}";
                    var savePath = Path.Combine(baseFolder, safeName);
                    using (var fs = System.IO.File.Create(savePath))
                    {
                        await file.CopyToAsync(fs);
                    }
                    var relUrl = $"/uploads/lostfound/claims/{claim.Id}/{safeName}";
                    await _lostFoundService.AddClaimProofImageAsync(claim.Id, relUrl);
                }
            }

            // Notify found user
            await _notificationService.CreateNotificationAsync(
                item.FoundByUserId.ToString(),
                "New Claim on Your Found Item",
                $"A user has claimed the item '{item.Title}' you found. Please verify the ownership details.",
                NotificationType.LostFoundNewClaim,
                claim.Id,
                $"/LostAndFound/VerifyClaim/{claim.Id}"
            );

            TempData["SuccessMessage"] = "Your claim has been submitted. The finder will verify your ownership details.";
            return RedirectToAction(nameof(MyClaims));
        }


        // GET: Verify claim (for found user)
        public async Task<IActionResult> VerifyClaim(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var claim = await _lostFoundService.GetClaimByIdAsync(id);
            if (claim == null) return NotFound();

            // Verify the current user is the found user
            if (claim.Item?.FoundByUserId != user.Id)
            {
                return Forbid();
            }

            var vm = new VerifyClaimViewModel
            {
                ClaimId = id,
                Claim = claim
            };

            return View(vm);
        }

        // POST: Submit verification
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyClaim(VerifyClaimViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var claim = await _lostFoundService.GetClaimByIdAsync(vm.ClaimId);
            if (claim == null) return NotFound();

            if (claim.Item?.FoundByUserId != user.Id)
            {
                return Forbid();
            }

            if (vm.IsCorrect)
            {
                // Mark as verified
                await _lostFoundService.UpdateClaimStatusAsync(vm.ClaimId, ClaimStatus.Verified, true, vm.Note);

                // Notify claimant
                await _notificationService.CreateNotificationAsync(
                    claim.ClaimantId.ToString(),
                    "Claim Verified!",
                    $"Your claim for '{claim.Item?.Title}' has been verified. Please coordinate with the finder for handover.",
                    NotificationType.LostFoundClaimVerified,
                    claim.ItemId,
                    $"/LostAndFound/Details/{claim.ItemId}"
                );

                TempData["SuccessMessage"] = "Claim verified successfully! You can now coordinate the handover.";
            }
            else
            {
                // Check attempt count
                if (claim.AttemptCount >= 2)
                {
                    // Block the claimant
                    await _lostFoundService.UpdateClaimStatusAsync(vm.ClaimId, ClaimStatus.Blocked, false, vm.Note);

                    // Notify claimant about block
                    await _notificationService.CreateNotificationAsync(
                        claim.ClaimantId.ToString(),
                        "Claim Blocked",
                        $"Your claim for '{claim.Item?.Title}' has been blocked due to repeated incorrect ownership details.",
                        NotificationType.LostFoundClaimBlocked,
                        claim.ItemId
                    );

                    // Notify found user
                    TempData["SuccessMessage"] = "Claimant has been blocked due to repeated incorrect claims.";
                }
                else
                {
                    // Allow retry
                    await _lostFoundService.UpdateClaimStatusAsync(vm.ClaimId, ClaimStatus.RetryAllowed, false, vm.Note);

                    // Notify claimant about rejection with retry
                    await _notificationService.CreateNotificationAsync(
                        claim.ClaimantId.ToString(),
                        "Claim Rejected - Retry Allowed",
                        $"Your claim for '{claim.Item?.Title}' was rejected. You have one final attempt. Please provide more detailed private description.",
                        NotificationType.LostFoundClaimRejected,
                        claim.Id,
                        $"/LostAndFound/RetryClaim/{claim.Id}"
                    );

                    TempData["SuccessMessage"] = "Claim rejected. The claimant has been notified and can retry once more.";
                }
            }

            return RedirectToAction(nameof(PendingVerifications));
        }

        // GET: Retry claim form
        public async Task<IActionResult> RetryClaim(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var claim = await _lostFoundService.GetClaimByIdAsync(id);
            if (claim == null) return NotFound();

            // Verify the current user is the claimant
            if (claim.ClaimantId != user.Id)
            {
                return Forbid();
            }

            // Check if retry is allowed
            if (claim.Status != ClaimStatus.RetryAllowed)
            {
                TempData["ErrorMessage"] = "Retry is not allowed for this claim.";
                return RedirectToAction(nameof(MyClaims));
            }

            var vm = new RetryClaimViewModel
            {
                ClaimId = id,
                Claim = claim
            };

            return View(vm);
        }

        // POST: Submit retry claim
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RetryClaim(RetryClaimViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var claim = await _lostFoundService.GetClaimByIdAsync(vm.ClaimId);
            if (claim == null) return NotFound();

            if (claim.ClaimantId != user.Id)
            {
                return Forbid();
            }

            if (claim.Status != ClaimStatus.RetryAllowed)
            {
                TempData["ErrorMessage"] = "Retry is not allowed for this claim.";
                return RedirectToAction(nameof(MyClaims));
            }

            vm.Claim = claim;

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            // Handle new proof uploads
            List<string>? newProofUrls = null;
            if (vm.NewProofImages?.Count > 0)
            {
                newProofUrls = new List<string>();
                var baseFolder = Path.Combine(_env.WebRootPath, "uploads", "lostfound", "claims", claim.Id);
                if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);

                foreach (var file in vm.NewProofImages)
                {
                    if (file.Length <= 0) continue;
                    var ext = Path.GetExtension(file.FileName);
                    var safeName = $"{Guid.NewGuid()}{ext}";
                    var savePath = Path.Combine(baseFolder, safeName);
                    using (var fs = System.IO.File.Create(savePath))
                    {
                        await file.CopyToAsync(fs);
                    }
                    newProofUrls.Add($"/uploads/lostfound/claims/{claim.Id}/{safeName}");
                }
            }

            await _lostFoundService.UpdateClaimDetailsAsync(vm.ClaimId, vm.NewPrivateOwnershipDetails, newProofUrls);

            // Notify found user
            await _notificationService.CreateNotificationAsync(
                claim.Item!.FoundByUserId.ToString(),
                "Claim Updated - Final Attempt",
                $"The claimant has submitted updated ownership details for '{claim.Item.Title}'. This is their final attempt.",
                NotificationType.LostFoundClaimRetry,
                claim.Id,
                $"/LostAndFound/VerifyClaim/{claim.Id}"
            );

            TempData["SuccessMessage"] = "Your updated claim has been submitted. This is your final attempt.";
            return RedirectToAction(nameof(MyClaims));
        }

        // GET: My found items dashboard
        public async Task<IActionResult> MyFoundItems()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var items = await _lostFoundService.GetItemsByFoundUserAsync(user.Id);
            var pendingCount = await _lostFoundService.GetPendingClaimsCountAsync(user.Id);

            var vm = new MyFoundItemsViewModel
            {
                Items = items,
                PendingClaimsCount = pendingCount
            };

            return View(vm);
        }

        // GET: My claims dashboard
        public async Task<IActionResult> MyClaims()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var claims = await _lostFoundService.GetClaimsByUserAsync(user.Id);

            var vm = new MyClaimsViewModel
            {
                Claims = claims
            };

            return View(vm);
        }

        // GET: Pending verifications for found user
        public async Task<IActionResult> PendingVerifications()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var pendingClaims = await _lostFoundService.GetPendingClaimsForFoundUserAsync(user.Id);

            var vm = new PendingVerificationsViewModel
            {
                PendingClaims = pendingClaims
            };

            return View(vm);
        }

        // POST: Mark item as returned
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsReturned(string itemId, string claimId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var item = await _lostFoundService.GetItemByIdAsync(itemId);
            if (item == null) return NotFound();

            if (item.FoundByUserId != user.Id)
            {
                return Forbid();
            }

            var claim = await _lostFoundService.GetClaimByIdAsync(claimId);
            if (claim == null || claim.Status != ClaimStatus.Verified)
            {
                TempData["ErrorMessage"] = "Invalid claim or claim not verified.";
                return RedirectToAction(nameof(Details), new { id = itemId });
            }

            await _lostFoundService.MarkAsReturnedAsync(itemId, claim.ClaimantId, claim.ClaimantName);

            // Notify claimant
            await _notificationService.CreateNotificationAsync(
                claim.ClaimantId.ToString(),
                "Item Returned",
                $"The item '{item.Title}' has been marked as returned. Thank you for using Lost & Found!",
                NotificationType.LostFoundItemReturned,
                itemId
            );

            TempData["SuccessMessage"] = "Item marked as returned successfully!";
            return RedirectToAction(nameof(MyFoundItems));
        }

        // API: Get categories
        [HttpGet]
        [Route("api/lostfound/categories")]
        [AllowAnonymous]
        public IActionResult GetCategories()
        {
            return Json(LostFoundItemCategory.All);
        }

        // API: Get conditions
        [HttpGet]
        [Route("api/lostfound/conditions")]
        [AllowAnonymous]
        public IActionResult GetConditions()
        {
            return Json(LostFoundItemCondition.All);
        }

        #region Lost Item Report Actions

        // GET: Browse all lost item reports
        [AllowAnonymous]
        public async Task<IActionResult> LostItems(string? category = null)
        {
            var reports = await _lostFoundService.GetAllLostReportsAsync(category, LostItemStatus.Active);
            var vm = new LostItemsListViewModel
            {
                Reports = reports,
                CategoryFilter = category,
                TotalCount = reports.Count
            };
            return View(vm);
        }

        // GET: Lost item details
        [AllowAnonymous]
        public async Task<IActionResult> LostItemDetails(string id)
        {
            var report = await _lostFoundService.GetLostReportByIdAsync(id);
            if (report == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var vm = new LostItemDetailsViewModel
            {
                Report = report,
                IsOwner = user != null && report.LostByUserId == user.Id,
                CanMarkAsFound = user != null && report.LostByUserId != user.Id && report.Status == LostItemStatus.Active,
                ShowFinderContact = user != null && report.LostByUserId == user.Id && report.Status == LostItemStatus.FoundByOther
            };

            return View(vm);
        }

        // GET: Report lost item form
        public IActionResult ReportLost()
        {
            return View(new ReportLostItemViewModel());
        }

        // POST: Submit lost item report
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportLost(ReportLostItemViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var report = new LostItemReport
            {
                Title = vm.Title,
                Category = vm.Category,
                Description = vm.Description,
                LostDate = vm.LostDate,
                LostLocation = vm.LostLocation,
                LostLocationDetails = vm.LostLocationDetails,
                LostByUserId = user.Id,
                LostByUserName = user.FullName ?? user.UserName ?? "Unknown",
                LostByUserEmail = user.Email ?? string.Empty,
                LostByUserPhone = user.PhoneNumber
            };

            await _lostFoundService.CreateLostReportAsync(report);

            // Handle image uploads
            if (vm.Images?.Count > 0)
            {
                var baseFolder = Path.Combine(_env.WebRootPath, "uploads", "lostfound", "lost", report.Id);
                if (!Directory.Exists(baseFolder)) Directory.CreateDirectory(baseFolder);

                foreach (var file in vm.Images)
                {
                    if (file.Length <= 0) continue;
                    var ext = Path.GetExtension(file.FileName);
                    var safeName = $"{Guid.NewGuid()}{ext}";
                    var savePath = Path.Combine(baseFolder, safeName);
                    using (var fs = System.IO.File.Create(savePath))
                    {
                        await file.CopyToAsync(fs);
                    }
                    var relUrl = $"/uploads/lostfound/lost/{report.Id}/{safeName}";
                    await _lostFoundService.AddLostReportImageAsync(report.Id, relUrl);
                }
            }

            TempData["SuccessMessage"] = "Lost item reported successfully! Others can now help you find it.";
            
            // Check for matching found items using S-BERT ML model
            _ = Task.Run(async () =>
            {
                try
                {
                    var availableFoundItems = await _lostFoundService.GetAllItemsAsync(status: LostFoundItemStatus.Available);
                    if (availableFoundItems.Any())
                    {
                        var matches = await _itemMatchingService.FindMatchingFoundItemsAsync(report, availableFoundItems, threshold: 0.5);
                        foreach (var match in matches)
                        {
                            // Notify the user who reported the lost item about potential match
                            await _notificationService.CreateNotificationAsync(
                                user.Id.ToString(),
                                "Potential Match Found!",
                                $"A found item '{match.Title}' ({match.MatchPercentage}% match) might be your lost '{report.Title}'. Check it out!",
                                NotificationType.PotentialItemMatch,
                                match.ItemId,
                                $"/LostAndFound/Details/{match.ItemId}"
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail the main operation
                    Console.WriteLine($"Item matching error: {ex.Message}");
                }
            });
            
            return RedirectToAction(nameof(MyLostItems));
        }

        // GET: My lost items dashboard
        public async Task<IActionResult> MyLostItems()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var reports = await _lostFoundService.GetLostReportsByUserAsync(user.Id);

            var vm = new MyLostItemsViewModel
            {
                Reports = reports
            };

            return View(vm);
        }

        // GET: Mark lost item as found form
        public async Task<IActionResult> MarkAsFound(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var report = await _lostFoundService.GetLostReportByIdAsync(id);
            if (report == null) return NotFound();

            // Cannot mark your own item as found
            if (report.LostByUserId == user.Id)
            {
                TempData["ErrorMessage"] = "You cannot mark your own lost item as found.";
                return RedirectToAction(nameof(LostItemDetails), new { id });
            }

            // Check if already found
            if (report.Status != LostItemStatus.Active)
            {
                TempData["ErrorMessage"] = "This item is no longer active.";
                return RedirectToAction(nameof(LostItemDetails), new { id });
            }

            var vm = new MarkAsFoundViewModel
            {
                ReportId = id,
                Report = report
            };

            return View(vm);
        }

        // POST: Submit mark as found
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsFound(MarkAsFoundViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var report = await _lostFoundService.GetLostReportByIdAsync(vm.ReportId);
            if (report == null) return NotFound();

            vm.Report = report;

            if (report.LostByUserId == user.Id)
            {
                ModelState.AddModelError("", "You cannot mark your own lost item as found.");
                return View(vm);
            }

            if (report.Status != LostItemStatus.Active)
            {
                ModelState.AddModelError("", "This item is no longer active.");
                return View(vm);
            }

            await _lostFoundService.MarkLostItemAsFoundAsync(
                vm.ReportId,
                user.Id,
                user.FullName ?? user.UserName ?? "Unknown",
                user.Email ?? string.Empty,
                user.PhoneNumber,
                vm.FoundLocation,
                vm.FoundNote
            );

            // Notify the lost item owner
            await _notificationService.CreateNotificationAsync(
                report.LostByUserId.ToString(),
                "Your Lost Item Has Been Found!",
                $"Someone has found your lost item '{report.Title}'. Check the details to contact them.",
                NotificationType.LostItemFound,
                report.Id,
                $"/LostAndFound/LostItemDetails/{report.Id}"
            );

            TempData["SuccessMessage"] = "Thank you for helping! The owner has been notified and can now contact you.";
            return RedirectToAction(nameof(LostItems));
        }

        // POST: Mark lost item as recovered (by owner)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRecovered(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var report = await _lostFoundService.GetLostReportByIdAsync(id);
            if (report == null) return NotFound();

            if (report.LostByUserId != user.Id)
            {
                return Forbid();
            }

            await _lostFoundService.MarkLostItemAsRecoveredAsync(id);

            // Notify the finder if there was one
            if (report.FoundByUserId.HasValue)
            {
                await _notificationService.CreateNotificationAsync(
                    report.FoundByUserId.Value.ToString(),
                    "Lost Item Recovered",
                    $"The owner of '{report.Title}' has marked the item as recovered. Thank you for your help!",
                    NotificationType.LostItemRecovered,
                    report.Id
                );
            }

            TempData["SuccessMessage"] = "Item marked as recovered! Thank you for using Lost & Found.";
            return RedirectToAction(nameof(MyLostItems));
        }

        // POST: Close lost report (by owner)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CloseLostReport(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var report = await _lostFoundService.GetLostReportByIdAsync(id);
            if (report == null) return NotFound();

            if (report.LostByUserId != user.Id)
            {
                return Forbid();
            }

            await _lostFoundService.CloseLostReportAsync(id);

            TempData["SuccessMessage"] = "Lost item report closed.";
            return RedirectToAction(nameof(MyLostItems));
        }

        // GET: Suggested matches for user's lost items using S-BERT ML
        public async Task<IActionResult> SuggestedMatches()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var vm = new SuggestedMatchesViewModel
            {
                Matches = new List<SuggestedMatchItem>(),
                IsServiceAvailable = _itemMatchingService.IsServiceAvailable
            };

            if (!_itemMatchingService.IsServiceAvailable)
            {
                vm.ErrorMessage = "The AI matching service is currently unavailable. Please try again later.";
                return View(vm);
            }

            try
            {
                // Get user's active lost reports
                var userLostReports = await _lostFoundService.GetLostReportsByUserAsync(user.Id);
                var activeLostReports = userLostReports.Where(r => r.Status == LostItemStatus.Active).ToList();

                if (!activeLostReports.Any())
                {
                    return View(vm);
                }

                // Get all available found items
                var availableFoundItems = await _lostFoundService.GetAllItemsAsync(status: LostFoundItemStatus.Available);

                if (!availableFoundItems.Any())
                {
                    return View(vm);
                }

                // Find matches for each lost report
                foreach (var lostReport in activeLostReports)
                {
                    var matches = await _itemMatchingService.FindMatchingFoundItemsAsync(lostReport, availableFoundItems, threshold: 0.4);

                    foreach (var match in matches)
                    {
                        var foundItem = availableFoundItems.FirstOrDefault(f => f.Id == match.ItemId);
                        if (foundItem != null)
                        {
                            vm.Matches.Add(new SuggestedMatchItem
                            {
                                LostReport = lostReport,
                                FoundItem = foundItem,
                                MatchPercentage = match.MatchPercentage
                            });
                        }
                    }
                }

                // Sort by match percentage descending
                vm.Matches = vm.Matches.OrderByDescending(m => m.MatchPercentage).ToList();
            }
            catch (Exception ex)
            {
                vm.ErrorMessage = "An error occurred while finding matches. Please try again later.";
                Console.WriteLine($"SuggestedMatches error: {ex.Message}");
            }

            return View(vm);
        }

        #endregion
    }
}
