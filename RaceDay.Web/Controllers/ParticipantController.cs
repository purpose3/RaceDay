using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RequireLogin(Role = "Participant")]
public class ParticipantController : AppController
{
    private readonly ApiClient _api;
    public ParticipantController(ApiClient api) => _api = api;

    /// <summary>Enrol the logged-in participant in the chosen category. Recorded immediately by the API.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Enrol(int eventId, int? categoryId)
    {
        if (categoryId is null)
        {
            FlashError("Please choose a category before enrolling.");
            return RedirectToAction("Details", "Home", new { id = eventId });
        }

        var result = await _api.PostAsync<EnrolmentCreated>($"/api/categories/{categoryId}/enrollments");
        if (result.IsUnauthorized) return RedirectToLogin();
        if (!result.Success || result.Data is null)
        {
            FlashError(result.Error ?? "We couldn't complete your enrolment. Please try again.");
            return RedirectToAction("Details", "Home", new { id = eventId });
        }

        FlashSuccess($"You're in! Your bib number is {result.Data.BibNumber}. " +
                     $"Your enrolment is {result.Data.Status.ToLowerInvariant()}.");
        return RedirectToAction(nameof(MyEnrolments));
    }

    /// <summary>All events the participant has entered, with their enrolment status.</summary>
    public async Task<IActionResult> MyEnrolments()
    {
        var result = await _api.GetAsync<List<MyEnrolment>>("/api/users/me/enrollments");
        if (result.IsUnauthorized) return RedirectToLogin();
        if (!result.Success)
        {
            FlashError(result.Error ?? "We couldn't load your enrolments right now.");
            return View(new List<MyEnrolment>());
        }
        return View(result.Data ?? new List<MyEnrolment>());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        var result = await _api.DeleteAsync($"/api/enrollments/{id}");
        if (result.IsUnauthorized) return RedirectToLogin();

        if (result.Success) FlashSuccess("Your enrolment has been cancelled.");
        else FlashError(result.Error ?? "We couldn't cancel that enrolment.");
        return RedirectToAction(nameof(MyEnrolments));
    }

    /// <summary>Personal race history across completed events.</summary>
    public async Task<IActionResult> MyResults()
    {
        var result = await _api.GetAsync<List<MyResult>>("/api/users/me/results");
        if (result.IsUnauthorized) return RedirectToLogin();
        if (!result.Success)
        {
            FlashError(result.Error ?? "We couldn't load your results right now.");
            return View(new List<MyResult>());
        }
        return View(result.Data ?? new List<MyResult>());
    }
}
