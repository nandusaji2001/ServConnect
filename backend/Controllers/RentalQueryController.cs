using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServConnect.Models;
using ServConnect.Services;

namespace ServConnect.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RentalQueryController : ControllerBase
    {
        private readonly IRentalQueryService _queryService;
        private readonly IRentalPropertyService _rentalService;
        private readonly UserManager<Users> _userManager;

        public RentalQueryController(
            IRentalQueryService queryService,
            IRentalPropertyService rentalService,
            UserManager<Users> userManager)
        {
            _queryService = queryService;
            _rentalService = rentalService;
            _userManager = userManager;
        }

        // Create a new query
        [HttpPost("create")]
        [Authorize]
        public async Task<IActionResult> CreateQuery([FromBody] CreateQueryRequest request)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return Unauthorized(new { success = false, message = "User not found" });

                var property = await _rentalService.GetByIdAsync(request.PropertyId);
                if (property == null)
                    return NotFound(new { success = false, message = "Property not found" });

                // Check if user is not the owner
                if (property.OwnerId == user.Id.ToString())
                    return BadRequest(new { success = false, message = "You cannot query your own property" });

                // Check if there's already an active query from this user for this property
                var existingQueries = await _queryService.GetQueriesForUserAsync(user.Id.ToString());
                var existingQuery = existingQueries.FirstOrDefault(q => q.PropertyId == request.PropertyId);
                
                if (existingQuery != null)
                {
                    // Add message to existing query instead of creating new one
                    var message = new QueryMessage
                    {
                        SenderId = user.Id.ToString(),
                        SenderName = user.FullName ?? user.Email ?? "User",
                        SenderRole = "User",
                        Message = request.Message
                    };

                    var updated = await _queryService.AddReplyAsync(existingQuery.Id, message, false);
                    return Ok(new { success = true, query = updated, isExisting = true });
                }

                // Create new query
                var query = new RentalQuery
                {
                    PropertyId = request.PropertyId,
                    PropertyTitle = property.Title,
                    UserId = user.Id.ToString(),
                    UserName = user.FullName ?? user.Email ?? "User",
                    UserEmail = user.Email ?? "",
                    OwnerId = property.OwnerId,
                    OwnerName = property.OwnerName,
                    Messages = new List<QueryMessage>
                    {
                        new QueryMessage
                        {
                            SenderId = user.Id.ToString(),
                            SenderName = user.FullName ?? user.Email ?? "User",
                            SenderRole = "User",
                            Message = request.Message
                        }
                    }
                };

                var created = await _queryService.CreateQueryAsync(query);
                return Ok(new { success = true, query = created, isExisting = false });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Reply to a query
        [HttpPost("reply")]
        [Authorize]
        public async Task<IActionResult> ReplyToQuery([FromBody] ReplyQueryRequest request)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return Unauthorized(new { success = false, message = "User not found" });

                var query = await _queryService.GetQueryByIdAsync(request.QueryId);
                if (query == null)
                    return NotFound(new { success = false, message = "Query not found" });

                // Verify user is either the owner or the user who created the query
                var isOwner = query.OwnerId == user.Id.ToString();
                var isUser = query.UserId == user.Id.ToString();

                if (!isOwner && !isUser)
                    return Forbid();

                var message = new QueryMessage
                {
                    SenderId = user.Id.ToString(),
                    SenderName = user.FullName ?? user.Email ?? "User",
                    SenderRole = isOwner ? "Owner" : "User",
                    Message = request.Message
                };

                var updated = await _queryService.AddReplyAsync(request.QueryId, message, isOwner);
                return Ok(new { success = true, query = updated });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Get queries for owner (property owner's dashboard)
        [HttpGet("owner")]
        [Authorize]
        public async Task<IActionResult> GetOwnerQueries()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return Unauthorized(new { success = false, message = "User not found" });

                var queries = await _queryService.GetQueriesForOwnerAsync(user.Id.ToString());
                var unreadCount = await _queryService.GetUnreadCountForOwnerAsync(user.Id.ToString());

                return Ok(new { success = true, queries, unreadCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Get queries for user (user's dashboard)
        [HttpGet("user")]
        [Authorize]
        public async Task<IActionResult> GetUserQueries()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return Unauthorized(new { success = false, message = "User not found" });

                var queries = await _queryService.GetQueriesForUserAsync(user.Id.ToString());
                var unreadCount = await _queryService.GetUnreadCountForUserAsync(user.Id.ToString());

                return Ok(new { success = true, queries, unreadCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Get single query details
        [HttpGet("{queryId}")]
        [Authorize]
        public async Task<IActionResult> GetQuery(string queryId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return Unauthorized(new { success = false, message = "User not found" });

                var query = await _queryService.GetQueryByIdAsync(queryId);
                if (query == null)
                    return NotFound(new { success = false, message = "Query not found" });

                // Verify user is either the owner or the user who created the query
                var isOwner = query.OwnerId == user.Id.ToString();
                var isUser = query.UserId == user.Id.ToString();

                if (!isOwner && !isUser)
                    return Forbid();

                // Mark as read
                if (isOwner)
                    await _queryService.MarkAsReadByOwnerAsync(queryId);
                else
                    await _queryService.MarkAsReadByUserAsync(queryId);

                return Ok(new { success = true, query, isOwner });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Delete a query (only by owner or user)
        [HttpDelete("{queryId}")]
        [Authorize]
        public async Task<IActionResult> DeleteQuery(string queryId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return Unauthorized(new { success = false, message = "User not found" });

                var query = await _queryService.GetQueryByIdAsync(queryId);
                if (query == null)
                    return NotFound(new { success = false, message = "Query not found" });

                // Verify user is either the owner or the user who created the query
                if (query.OwnerId != user.Id.ToString() && query.UserId != user.Id.ToString())
                    return Forbid();

                await _queryService.DeleteQueryAsync(queryId);
                return Ok(new { success = true, message = "Query deleted" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Get unread count for owner
        [HttpGet("unread/owner")]
        [Authorize]
        public async Task<IActionResult> GetOwnerUnreadCount()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return Unauthorized(new { success = false, message = "User not found" });

                var count = await _queryService.GetUnreadCountForOwnerAsync(user.Id.ToString());
                return Ok(new { success = true, unreadCount = count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Get unread count for user
        [HttpGet("unread/user")]
        [Authorize]
        public async Task<IActionResult> GetUserUnreadCount()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                    return Unauthorized(new { success = false, message = "User not found" });

                var count = await _queryService.GetUnreadCountForUserAsync(user.Id.ToString());
                return Ok(new { success = true, unreadCount = count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}
