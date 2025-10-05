using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ServConnect.Models;

namespace ServConnect.Filters
{
    /// <summary>
    /// API-specific filter that ensures the current user has a completed profile and is admin-approved.
    /// Returns appropriate HTTP status codes instead of redirects for API endpoints.
    /// </summary>
    public class RequireApprovedUserApiFilter : IAsyncActionFilter
    {
        private readonly UserManager<Users> _userManager;

        public RequireApprovedUserApiFilter(UserManager<Users> userManager)
        {
            _userManager = userManager;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var principal = context.HttpContext.User;
            if (principal?.Identity == null || !principal.Identity.IsAuthenticated)
            {
                // Let [Authorize] handle unauthenticated cases
                await next();
                return;
            }

            var user = await _userManager.GetUserAsync(principal);
            if (user == null)
            {
                context.Result = new UnauthorizedObjectResult(new { error = "User not found" });
                return;
            }

            // Determine if the current user is an Admin once
            var isAdmin = principal.IsInRole(RoleTypes.Admin);

            // Skip profile completion requirement for Admins
            if (!isAdmin && !user.IsProfileCompleted)
            {
                context.Result = new BadRequestObjectResult(new 
                { 
                    error = "Profile incomplete", 
                    message = "Please complete your profile to continue using the app." 
                });
                return;
            }

            // Skip approval requirement for Admins
            if (!isAdmin && !user.IsAdminApproved)
            {
                context.Result = new ObjectResult(new 
                { 
                    error = "Approval pending", 
                    message = "Your profile is pending admin approval." 
                })
                {
                    StatusCode = 403 // Forbidden
                };
                return;
            }

            await next();
        }
    }
}
