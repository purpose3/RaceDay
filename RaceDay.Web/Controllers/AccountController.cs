using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController : AppController
{
    private readonly ApiClient _api;
    private readonly UserSession _session;

    public AccountController(ApiClient api, UserSession session)
    {
        _api = api;
        _session = session;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_session.IsAuthenticated) return RedirectToHome();
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _api.PostAsync<LoginResponse>("/api/auth/login",
            new { email = model.Email.Trim(), password = model.Password });

        if (!result.Success || result.Data is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't log you in. Please try again.");
            return View(model);
        }

        await SignInAsync(result.Data);
        return RedirectAfterLogin(model.ReturnUrl);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (_session.IsAuthenticated) return RedirectToHome();
        return View(new RegisterViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (model.Role != "Participant" && model.Role != "Organiser")
            ModelState.AddModelError(nameof(model.Role), "Please choose Participant or Organiser.");
        if (model.Role == "Organiser" && string.IsNullOrWhiteSpace(model.InviteCode))
            ModelState.AddModelError(nameof(model.InviteCode), "Organisers need an invite code.");
        if (!ModelState.IsValid) return View(model);

        var result = await _api.PostAsync<object>("/api/auth/register", new
        {
            firstName = model.FirstName.Trim(),
            lastName = model.LastName.Trim(),
            email = model.Email.Trim(),
            password = model.Password,
            phoneNumber = model.PhoneNumber.Trim(),
            role = model.Role,
            inviteCode = model.Role == "Organiser" ? model.InviteCode?.Trim() : null
        });

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "We couldn't create your account. Please try again.");
            return View(model);
        }

        // Log in straight away so the user lands signed in
        var login = await _api.PostAsync<LoginResponse>("/api/auth/login",
            new { email = model.Email.Trim(), password = model.Password });
        if (!login.Success || login.Data is null)
        {
            FlashSuccess("Your account has been created. Please log in.");
            return RedirectToAction(nameof(Login));
        }

        await SignInAsync(login.Data);
        FlashSuccess($"Welcome to RaceDay, {model.FirstName.Trim()}!");
        return RedirectToHome();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        _session.SignOut();
        FlashSuccess("You have been logged out.");
        return RedirectToAction(nameof(Login));
    }

    // ---------- helpers ----------

    private async Task SignInAsync(LoginResponse login)
    {
        _session.SignIn(login.UserId, login.Role, login.Token, login.ExpiresAt);

        // Fetch the profile once so the navigation can greet the user by name and show their picture
        var profile = await _api.GetAsync<UserProfile>("/api/users/me");
        if (profile.Success && profile.Data is not null)
            _session.SetProfile(profile.Data.FirstName, profile.Data.ProfilePictureUrl is not null);
        else
            _session.SetProfile("", false);
    }

    private IActionResult RedirectAfterLogin(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToHome();
    }

    private IActionResult RedirectToHome() =>
        _session.IsOrganiser
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Index", "Home");
}
