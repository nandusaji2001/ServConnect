using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

        public GuardianController(
            UserManager<Users> userManager,
            IMongoDatabase mongoDatabase)
        {
            _userManager = userManager;
            _elderRequestsCollection = mongoDatabase.GetCollection<ElderRequest>("ElderRequests");
            _elderCareInfoCollection = mongoDatabase.GetCollection<ElderCareInfo>("ElderCareInfo");
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            // Get all pending elder requests for this guardian
            var pendingRequests = await _elderRequestsCollection
                .Find(r => r.GuardianUserId == currentUser.Id.ToString() && r.Status == "Pending")
                .ToListAsync();

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
        public async Task<IActionResult> ApproveRequest(string requestId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            // Find the request
            var request = await _elderRequestsCollection
                .Find(r => r.Id == requestId && r.GuardianUserId == currentUser.Id.ToString())
                .FirstOrDefaultAsync();

            if (request == null)
            {
                TempData["ErrorMessage"] = "Request not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Update request status
            var update = Builders<ElderRequest>.Update
                .Set(r => r.Status, "Approved")
                .Set(r => r.UpdatedAt, DateTime.UtcNow);

            await _elderRequestsCollection.UpdateOneAsync(r => r.Id == requestId, update);

            // Update elder care info to assign guardian
            var elderUpdate = Builders<ElderCareInfo>.Update
                .Set(e => e.GuardianUserId, currentUser.Id.ToString())
                .Set(e => e.IsGuardianAssigned, true)
                .Set(e => e.UpdatedAt, DateTime.UtcNow);

            await _elderCareInfoCollection.UpdateOneAsync(
                e => e.UserId == request.ElderUserId, 
                elderUpdate);

            TempData["SuccessMessage"] = "Elder request approved successfully.";
            return RedirectToAction(nameof(Dashboard));
        }

        [HttpPost]
        public async Task<IActionResult> RejectRequest(string requestId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return NotFound();
            }

            // Find the request
            var request = await _elderRequestsCollection
                .Find(r => r.Id == requestId && r.GuardianUserId == currentUser.Id.ToString())
                .FirstOrDefaultAsync();

            if (request == null)
            {
                TempData["ErrorMessage"] = "Request not found.";
                return RedirectToAction(nameof(Dashboard));
            }

            // Update request status
            var update = Builders<ElderRequest>.Update
                .Set(r => r.Status, "Rejected")
                .Set(r => r.UpdatedAt, DateTime.UtcNow);

            await _elderRequestsCollection.UpdateOneAsync(r => r.Id == requestId, update);

            TempData["SuccessMessage"] = "Elder request rejected.";
            return RedirectToAction(nameof(Dashboard));
        }
    }
}