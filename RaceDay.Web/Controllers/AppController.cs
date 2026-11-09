using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Web.Controllers;

/// <summary>Shared helpers: flash messages (shown once at the top of the next page) and redirects.</summary>
public abstract class AppController : Controller
{
    protected void FlashSuccess(string message) => TempData["Success"] = message;
    protected void FlashError(string message) => TempData["Error"] = message;
    protected void FlashInfo(string message) => TempData["Info"] = message;

    protected IActionResult RedirectToLogin(string? message = null)
    {
        FlashInfo(message ?? "Your session has expired. Please log in again.");
        return RedirectToAction("Login", "Account");
    }

    /// <summary>The first validation message, for forms that redirect back with a flash message.</summary>
    protected string FirstValidationError() =>
        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
        ?? "Please check the information you entered.";
}
