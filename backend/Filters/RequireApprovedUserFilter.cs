using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ServConnect.Models;

namespace ServConnect.Filters
{
    // Ensures the current user has a completed profile and is admin-approved.
    // If not, redirects to Account/Profile with a helpful message.
    public class RequireApprovedUserFilter : IAsyncActionFilter
    {
        private readonly UserManager<Users> _userManager;

        public RequireApprovedUserFilter(UserManager<Users> userManager)
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

            // Allowlist key Account actions to prevent redirect loops and allow navigation
            var controllerName = (context.RouteData.Values["controller"] as string) ?? string.Empty;
            var actionName = (context.RouteData.Values["action"] as string) ?? string.Empty;
            var isAllowedAccountAction =
                controllerName.Equals("Account", StringComparison.OrdinalIgnoreCase) &&
                (actionName.Equals("Profile", StringComparison.OrdinalIgnoreCase)
                 || actionName.Equals("PendingApproval", StringComparison.OrdinalIgnoreCase)
                 || actionName.Equals("Logout", StringComparison.OrdinalIgnoreCase)
                 || actionName.Equals("AccessDenied", StringComparison.OrdinalIgnoreCase)
                 || actionName.Equals("Lockout", StringComparison.OrdinalIgnoreCase));

            if (isAllowedAccountAction)
            {
                await next();
                return;
            }

            var user = await _userManager.GetUserAsync(principal);
            if (user == null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Determine if the current user is an Admin once
            var isAdmin = principal.IsInRole(RoleTypes.Admin);

            // Skip profile completion requirement for Admins
            if (!isAdmin && !user.IsProfileCompleted)
            {
                if (context.Controller is Controller controllerA)
                {
                    controllerA.TempData["SuccessMessage"] = "Please complete your profile to continue using the app.";
                }
                context.Result = new RedirectToActionResult("Profile", "Account", null);
                return;
            }

            // Skip approval requirement for Admins
            if (!isAdmin && !user.IsAdminApproved)
            {
                if (context.Controller is Controller controllerB)
                {
                    controllerB.TempData["SuccessMessage"] = "Your profile is pending admin approval.";
                }
                context.Result = new RedirectToActionResult("PendingApproval", "Account", null);
                return;
            }

            await next();
        }
    }
}