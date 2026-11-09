using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>View and update personal information, change password and upload a profile picture (both roles).</summary>
[RequireLogin]
public class ProfileController : AppController
{
    private readonly ApiClient _api;
    private readonly UserSession _session;

    public ProfileController(ApiClient api, UserSession session)
    {
        _api = api;
        _session = session;
    }

    public async Task<IActionResult> Index()
    {
        var result = await _api.GetAsync<UserProfile>("/api/users/me");
        if (result.IsUnauthorized) return RedirectToLogin();
        if (!result.Success || result.Data is null)
        {
            FlashError(result.Error ?? "We couldn't load your profile right now.");
            return RedirectToAction("Index", "Home");
        }
        return View(result.Data);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateProfileModel model)
    {
        if (!ModelState.IsValid)
        {
            FlashError(FirstValidationError());
            return RedirectToAction(nameof(Index));
        }

        var result = await _api.PutAsync<UserProfile>("/api/users/me", new
        {
            firstName = model.FirstName.Trim(),
            lastName = model.LastName.Trim(),
            phoneNumber = model.PhoneNumber.Trim()
        });
        if (result.IsUnauthorized) return RedirectToLogin();

        if (result.Success && result.Data is not null)
        {
            _session.SetProfile(result.Data.FirstName, result.Data.ProfilePictureUrl is not null);
            FlashSuccess("Your profile has been updated.");
        }
        else FlashError(result.Error ?? "We couldn't update your profile.");

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordModel model)
    {
        if (!ModelState.IsValid)
        {
            FlashError(FirstValidationError());
            return RedirectToAction(nameof(Index));
        }

        var result = await _api.PutAsync<Unit>("/api/users/me/password",
            new { currentPassword = model.CurrentPassword, newPassword = model.NewPassword });
        if (result.IsUnauthorized) return RedirectToLogin();

        if (result.Success) FlashSuccess("Your password has been changed.");
        else FlashError(result.Error ?? "We couldn't change your password.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(8_000_000)]
    public async Task<IActionResult> UploadPicture(IFormFile? picture)
    {
        if (picture is null || picture.Length == 0)
        {
            FlashError("Please choose an image to upload.");
            return RedirectToAction(nameof(Index));
        }

        // The file goes to the API, which validates it and stores it in Azure Blob Storage
        var result = await _api.PostFileAsync<ImageUploadResult>("/api/users/me/picture", picture);
        if (result.IsUnauthorized) return RedirectToLogin();

        if (result.Success)
        {
            _session.SetProfile(_session.FirstName, true);
            _session.TouchPicture();
            FlashSuccess("Your profile picture has been updated.");
        }
        else FlashError(result.Error ?? "We couldn't upload that picture.");

        return RedirectToAction(nameof(Index));
    }
}
