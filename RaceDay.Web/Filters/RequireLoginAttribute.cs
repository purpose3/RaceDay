using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Filters;

/// <summary>
/// Protects a controller or action. Unauthenticated users are sent to the login page (and back
/// afterwards); users with the wrong role are sent to an "access denied" page.
/// Usage: [RequireLogin] for any signed-in user, or [RequireLogin(Role = "Organiser")].
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireLoginAttribute : Attribute, IAsyncActionFilter
{
    public string? Role { get; set; }

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var session = context.HttpContext.RequestServices.GetRequiredService<UserSession>();

        if (!session.IsAuthenticated)
        {
            var wasExpired = session.IsExpired;
            session.SignOut();

            if (context.Controller is Controller c)
                c.TempData["Info"] = wasExpired
                    ? "Your session has expired. Please log in again."
                    : "Please log in to continue.";

            var request = context.HttpContext.Request;
            var returnUrl = request.Method == HttpMethods.Get ? request.Path + request.QueryString : null;
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return Task.CompletedTask;
        }

        if (Role is not null && !string.Equals(session.Role, Role, StringComparison.Ordinal))
        {
            context.Result = new RedirectToActionResult("AccessDenied", "Home", null);
            return Task.CompletedTask;
        }

        return next();
    }
}
