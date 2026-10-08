using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;

namespace E_Greeting.Controllers
{
    /// <summary>
    /// Global Filter to prevent Admin users from accessing User-side pages/routes.
    /// Redirects any Admin attempt to access User features back to the Admin Dashboard.
    /// </summary>
    public class AdminAccessBlockerFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var user = context.HttpContext.User;

            // Check if current authenticated user holds Admin role
            if (user.Identity != null && user.Identity.IsAuthenticated)
            {
                if (user.IsInRole("Admin") || user.IsInRole("ADMIN"))
                {
                    // Get target Controller name
                    string controllerName = context.RouteData.Values["controller"]?.ToString() ?? "";

                    // EXCEPTION: Allow Admin to access ALL Admin Controllers (starting with "Admin") & AccountController (for Logout / Profile)
                    bool isAdminController = controllerName.StartsWith("Admin", StringComparison.OrdinalIgnoreCase);
                    bool isAccountController = controllerName.Equals("Account", StringComparison.OrdinalIgnoreCase);

                    if (!isAdminController && !isAccountController)
                    {
                        // Redirect immediately to Admin Dashboard if Admin tries to access User controllers
                        context.Result = new RedirectToActionResult("Dashboard", "Admin", null);
                    }
                }
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // No action required after execution
        }
    }
}