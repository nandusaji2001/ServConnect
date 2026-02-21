using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [Authorize]
    public class TransitConnectController : Controller
    {
        private readonly ITransportUpdateService _transportService;
        private readonly INotificationService _notificationService;
        private readonly UserManager<Users> _userManager;

        public TransitConnectController(
            ITransportUpdateService transportService,
            INotificationService notificationService,
            UserManager<Users> userManager)
        {
            _transportService = transportService;
            _notificationService = notificationService;
            _userManager = userManager;
        }

        // GET: Browse all transport routes
        [AllowAnonymous]
        public async Task<IActionResult> Index(string? transportType = null, string? from = null, string? to = null)
        {
            string? userDistrict = null;
            var isAuthenticated = User.Identity?.IsAuthenticated == true;
            
            if (isAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                userDistrict = user?.District;
            }

            List<TransportRoute> routes;
            
            // If search parameters provided, search; otherwise get all
            if (!string.IsNullOrWhiteSpace(from) || !string.IsNullOrWhiteSpace(to))
            {
                routes = await _transportService.SearchRoutesAsync(from, to, transportType, userDistrict);
            }
            else
            {
                routes = await _transportService.GetAllRoutesAsync(transportType, userDistrict);
            }

            ViewBag.TransportTypeFilter = transportType;
            ViewBag.FromFilter = from;
            ViewBag.ToFilter = to;
            ViewBag.TotalCount = routes.Count;
            ViewBag.PopularLocations = await _transportService.GetPopularLocationsAsync(userDistrict);
            ViewBag.IsAuthenticated = isAuthenticated;

            return View(routes);
        }

        // GET: Route details
        [AllowAnonymous]
        public async Task<IActionResult> Details(string id)
        {
            var route = await _transportService.GetRouteByIdAsync(id);
            if (route == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            
            ViewBag.IsContributor = user != null && route.ContributorId == user.Id;
            ViewBag.UserVoteType = user != null ? await _transportService.GetUserVoteTypeAsync(id, user.Id) : null;
            ViewBag.IsSaved = user != null && await _transportService.IsRouteSavedAsync(id, user.Id);
            ViewBag.UpdateRequests = await _transportService.GetUpdateRequestsForRouteAsync(id);

            return View(route);
        }

        // GET: Add new route form
        public async Task<IActionResult> Add()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            ViewBag.UserDistrict = user.District;
            return View();
        }

        // POST: Submit new route
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(TransportRoute route, string? intermediateStopsText)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Remove validation for fields we'll set
            ModelState.Remove("ContributorId");
            ModelState.Remove("ContributorName");
            ModelState.Remove("Id");
            ModelState.Remove("Status");
            ModelState.Remove("IntermediateStops");

            if (!ModelState.IsValid)
            {
                ViewBag.UserDistrict = user.District;
                return View(route);
            }

            // Parse intermediate stops
            if (!string.IsNullOrWhiteSpace(intermediateStopsText))
            {
                route.IntermediateStops = intermediateStopsText
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }

            // Set contributor info
            route.ContributorId = user.Id;
            route.ContributorName = user.FullName ?? user.UserName ?? "Anonymous";
            route.District = user.District ?? KeralaDistricts.Idukki;

            await _transportService.CreateRouteAsync(route);

            TempData["SuccessMessage"] = "Route added successfully! Thank you for contributing to the community.";
            return RedirectToAction(nameof(Details), new { id = route.Id });
        }

        // GET: Edit route
        public async Task<IActionResult> Edit(string id)
        {
            var route = await _transportService.GetRouteByIdAsync(id);
            if (route == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null || route.ContributorId != user.Id)
            {
                TempData["ErrorMessage"] = "You can only edit routes you've contributed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewBag.IntermediateStopsText = string.Join(", ", route.IntermediateStops);
            return View(route);
        }

        // POST: Update route
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, TransportRoute route, string? intermediateStopsText)
        {
            var existing = await _transportService.GetRouteByIdAsync(id);
            if (existing == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null || existing.ContributorId != user.Id)
            {
                TempData["ErrorMessage"] = "You can only edit routes you've contributed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Remove validation for fields we'll preserve
            ModelState.Remove("ContributorId");
            ModelState.Remove("ContributorName");
            ModelState.Remove("Id");
            ModelState.Remove("Status");
            ModelState.Remove("IntermediateStops");

            if (!ModelState.IsValid)
            {
                ViewBag.IntermediateStopsText = intermediateStopsText;
                return View(route);
            }

            // Parse intermediate stops
            if (!string.IsNullOrWhiteSpace(intermediateStopsText))
            {
                existing.IntermediateStops = intermediateStopsText
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }
            else
            {
                existing.IntermediateStops = new List<string>();
            }

            // Update fields
            existing.TransportName = route.TransportName;
            existing.RouteNumber = route.RouteNumber;
            existing.TransportType = route.TransportType;
            existing.StartLocation = route.StartLocation;
            existing.StartLocationDetails = route.StartLocationDetails;
            existing.EndLocation = route.EndLocation;
            existing.EndLocationDetails = route.EndLocationDetails;
            existing.DepartureTime = route.DepartureTime;
            existing.ArrivalTime = route.ArrivalTime;
            existing.Duration = route.Duration;
            existing.ServiceDays = route.ServiceDays;
            existing.Frequency = route.Frequency;
            existing.ApproxFare = route.ApproxFare;
            existing.FareDetails = route.FareDetails;
            existing.IsACAvailable = route.IsACAvailable;
            existing.IsWheelchairAccessible = route.IsWheelchairAccessible;
            existing.HasWifi = route.HasWifi;
            existing.IsExpressService = route.IsExpressService;
            existing.IsPeakHourService = route.IsPeakHourService;
            existing.AdditionalNotes = route.AdditionalNotes;

            await _transportService.UpdateRouteAsync(existing);

            TempData["SuccessMessage"] = "Route updated successfully!";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Delete route
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var result = await _transportService.DeleteRouteAsync(id, user.Id);
            if (result)
            {
                TempData["SuccessMessage"] = "Route deleted successfully.";
                return RedirectToAction(nameof(MyRoutes));
            }

            TempData["ErrorMessage"] = "Unable to delete the route. You can only delete your own routes.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: Upvote route
        [HttpPost]
        public async Task<IActionResult> Upvote(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Please login to vote" });

            var (success, message, newScore) = await _transportService.UpvoteRouteAsync(id, user.Id);
            return Json(new { success, message, newScore, voteType = "upvote" });
        }

        // POST: Downvote route
        [HttpPost]
        public async Task<IActionResult> Downvote(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Please login to vote" });

            var (success, message, newScore) = await _transportService.DownvoteRouteAsync(id, user.Id);
            
            // Check if route was removed due to downvotes
            var route = await _transportService.GetRouteByIdAsync(id);
            var wasRemoved = route?.Status == TransportRouteStatus.Removed;

            return Json(new { success, message, newScore, voteType = "downvote", wasRemoved });
        }

        // POST: Remove vote
        [HttpPost]
        public async Task<IActionResult> RemoveVote(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Please login" });

            var success = await _transportService.RemoveVoteAsync(id, user.Id);
            var route = await _transportService.GetRouteByIdAsync(id);
            
            return Json(new { success, newScore = route?.Score ?? 0 });
        }

        // POST: Confirm route accuracy
        [HttpPost]
        public async Task<IActionResult> ConfirmAccuracy(string id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Please login" });

            var success = await _transportService.ConfirmRouteAccuracyAsync(id, user.Id);
            return Json(new { success, message = success ? "Thank you for confirming!" : "Unable to confirm" });
        }

        // POST: Save route
        [HttpPost]
        public async Task<IActionResult> SaveRoute(string id, string? label)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Please login" });

            // Check if already saved
            if (await _transportService.IsRouteSavedAsync(id, user.Id))
            {
                return Json(new { success = false, message = "Route already saved" });
            }

            var savedRoute = new SavedRoute
            {
                UserId = user.Id,
                RouteId = id,
                CustomLabel = label
            };

            await _transportService.SaveRouteAsync(savedRoute);
            return Json(new { success = true, message = "Route saved successfully!" });
        }

        // POST: Remove saved route
        [HttpPost]
        public async Task<IActionResult> RemoveSavedRoute(string savedRouteId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Please login" });

            var success = await _transportService.RemoveSavedRouteAsync(savedRouteId, user.Id);
            return Json(new { success, message = success ? "Route removed from saved" : "Unable to remove" });
        }

        // GET: My routes (contributed by user)
        public async Task<IActionResult> MyRoutes()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var routes = await _transportService.GetRoutesByContributorAsync(user.Id);
            var savedRoutes = await _transportService.GetSavedRoutesAsync(user.Id);

            ViewBag.ContributedRoutes = routes;
            ViewBag.SavedRoutes = savedRoutes;

            return View();
        }

        // GET: Saved routes
        public async Task<IActionResult> SavedRoutes()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var savedRoutes = await _transportService.GetSavedRoutesAsync(user.Id);
            return View(savedRoutes);
        }

        // POST: Report route update/issue
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportUpdate(string routeId, string updateType, string description, string? proposedChange)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Please login" });

            var request = new RouteUpdateRequest
            {
                RouteId = routeId,
                UpdateType = updateType,
                Description = description,
                ProposedChange = proposedChange,
                ReporterId = user.Id,
                ReporterName = user.FullName ?? user.UserName ?? "Anonymous"
            };

            await _transportService.CreateUpdateRequestAsync(request);

            return Json(new { success = true, message = "Update report submitted. Thank you for helping keep information accurate!" });
        }

        // POST: Support an update request
        [HttpPost]
        public async Task<IActionResult> SupportUpdateRequest(string requestId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Please login" });

            var success = await _transportService.SupportUpdateRequestAsync(requestId, user.Id);
            return Json(new { success, message = success ? "Support recorded" : "Unable to support or already supported" });
        }

        // API: Get locations for autocomplete
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetLocations(string? district = null)
        {
            var locations = await _transportService.GetPopularLocationsAsync(district);
            return Json(locations);
        }

        // API: Search routes (for AJAX)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> SearchRoutes(string? from, string? to, string? transportType)
        {
            string? userDistrict = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                userDistrict = user?.District;
            }

            var routes = await _transportService.SearchRoutesAsync(from, to, transportType, userDistrict);
            return Json(routes.Select(r => new
            {
                r.Id,
                r.TransportName,
                r.RouteNumber,
                r.TransportType,
                r.StartLocation,
                r.EndLocation,
                r.DepartureTime,
                r.ArrivalTime,
                r.Score,
                r.ReliabilityPercentage,
                IntermediateStops = r.IntermediateStops.Count
            }));
        }

        // GET: Top rated routes widget
        [AllowAnonymous]
        public async Task<IActionResult> TopRated()
        {
            string? userDistrict = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                userDistrict = user?.District;
            }

            var routes = await _transportService.GetTopRatedRoutesAsync(userDistrict, 5);
            return PartialView("_TopRatedRoutes", routes);
        }
    }
}
