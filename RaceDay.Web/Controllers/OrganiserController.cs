using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// The Organiser interface: dashboard, events, categories, enrolments and results.
/// Every action calls the API, which enforces ownership and business rules; the checks here only
/// give friendlier navigation.
/// </summary>
[RequireLogin(Role = "Organiser")]
public class OrganiserController : AppController
{
    private static readonly string[] EventTypes = { "Run", "Walk", "Cycle" };

    private readonly ApiClient _api;
    private readonly UserSession _session;

    public OrganiserController(ApiClient api, UserSession session)
    {
        _api = api;
        _session = session;
    }

    // ====================== Dashboard & events list ======================

    public async Task<IActionResult> Dashboard()
    {
        var result = await _api.GetAsync<OrganiserDashboard>("/api/organiser/dashboard");
        if (result.IsUnauthorized) return RedirectToLogin();
        if (!result.Success || result.Data is null)
        {
            FlashError(result.Error ?? "We couldn't load your dashboard right now.");
            return View(EmptyDashboard());
        }
        return View(result.Data);
    }

    /// <summary>Event management page: all of the organiser's events with create, edit and delete.</summary>
    public async Task<IActionResult> Events()
    {
        var result = await _api.GetAsync<OrganiserDashboard>("/api/organiser/dashboard");
        if (result.IsUnauthorized) return RedirectToLogin();
        if (!result.Success || result.Data is null)
        {
            FlashError(result.Error ?? "We couldn't load your events right now.");
            return View(EmptyDashboard());
        }
        return View(result.Data);
    }

    // ====================== Create / edit / delete events ======================

    [HttpGet]
    public IActionResult CreateEvent() => View("EventForm", new EventFormModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(EventFormModel model, IFormFile? banner)
    {
        ValidateEventType(model);
        if (!ModelState.IsValid) return View("EventForm", model);

        var created = await _api.PostAsync<EventDetail>("/api/events", EventBody(model));
        if (created.IsUnauthorized) return RedirectToLogin();
        if (!created.Success || created.Data is null)
        {
            ModelState.AddModelError(string.Empty, created.Error ?? "We couldn't create the event. Please try again.");
            return View("EventForm", model);
        }

        var id = created.Data.EventId;
        if (banner is { Length: > 0 })
        {
            var upload = await _api.PostFileAsync<ImageUploadResult>($"/api/events/{id}/banner", banner);
            if (!upload.Success)
            {
                FlashError($"Your event was created, but the banner couldn't be uploaded: {upload.Error} You can add it by editing the event.");
                return RedirectToAction(nameof(EditEvent), new { id });
            }
        }

        FlashSuccess("Event created. Now add categories so participants can enrol.");
        return RedirectToAction(nameof(Categories), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var (ev, fail) = await LoadOwnedEventAsync(id);
        if (fail is not null) return fail;

        return View("EventForm", new EventFormModel
        {
            EventId = ev!.EventId,
            HasBanner = ev.BannerUrl is not null,
            EventName = ev.EventName,
            Description = ev.Description,
            EventDate = ev.EventDate.Date,
            Location = ev.Location,
            RouteInfo = ev.RouteInfo,
            EventType = ev.EventType,
            DistanceKm = ev.DistanceKm
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEvent(int id, EventFormModel model, IFormFile? banner)
    {
        model.EventId = id;
        ValidateEventType(model);
        if (!ModelState.IsValid) return View("EventForm", model);

        var updated = await _api.PutAsync<EventDetail>($"/api/events/{id}", EventBody(model));
        if (updated.IsUnauthorized) return RedirectToLogin();
        if (!updated.Success)
        {
            ModelState.AddModelError(string.Empty, updated.Error ?? "We couldn't save your changes. Please try again.");
            return View("EventForm", model);
        }

        if (banner is { Length: > 0 })
        {
            var upload = await _api.PostFileAsync<ImageUploadResult>($"/api/events/{id}/banner", banner);
            if (!upload.Success)
            {
                FlashError($"Your changes were saved, but the banner couldn't be uploaded: {upload.Error}");
                return RedirectToAction(nameof(EditEvent), new { id });
            }
        }

        FlashSuccess("Event updated.");
        return RedirectToAction(nameof(Events));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var result = await _api.DeleteAsync($"/api/events/{id}");
        if (result.IsUnauthorized) return RedirectToLogin();

        if (result.Success) FlashSuccess("The event has been deleted.");
        else FlashError(result.Error ?? "We couldn't delete that event.");
        return RedirectToAction(nameof(Events));
    }

    // ====================== Categories ======================

    public async Task<IActionResult> Categories(int id)
    {
        var (ev, fail) = await LoadOwnedEventAsync(id);
        if (fail is not null) return fail;
        return View(new CategoriesViewModel { Event = ev! });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(int id, CategoryFormModel model)
    {
        if (!ModelState.IsValid)
        {
            FlashError(FirstValidationError());
            return RedirectToAction(nameof(Categories), new { id });
        }

        var result = await _api.PostAsync<CategoryDto>($"/api/events/{id}/categories", CategoryBody(model));
        if (result.IsUnauthorized) return RedirectToLogin();

        if (result.Success) FlashSuccess($"Category \"{model.CategoryName.Trim()}\" added.");
        else FlashError(result.Error ?? "We couldn't add that category.");
        return RedirectToAction(nameof(Categories), new { id });
    }

    /// <param name="id">The category id.</param>
    /// <param name="eventId">The event the category belongs to.</param>
    [HttpGet]
    public async Task<IActionResult> EditCategory(int id, int eventId)
    {
        var (ev, fail) = await LoadOwnedEventAsync(eventId);
        if (fail is not null) return fail;

        var category = ev!.Categories.FirstOrDefault(c => c.CategoryId == id);
        if (category is null) return NotFound();

        return View(new CategoryEditViewModel
        {
            EventId = ev.EventId,
            EventName = ev.EventName,
            CategoryId = category.CategoryId,
            Form = new CategoryFormModel
            {
                CategoryName = category.CategoryName,
                DistanceKm = category.DistanceKm,
                EntryFee = category.EntryFee,
                MaxParticipants = category.MaxParticipants
            }
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int id, int eventId, CategoryFormModel model)
    {
        if (!ModelState.IsValid)
        {
            FlashError(FirstValidationError());
            return RedirectToAction(nameof(EditCategory), new { id, eventId });
        }

        var result = await _api.PutAsync<CategoryDto>($"/api/categories/{id}", CategoryBody(model));
        if (result.IsUnauthorized) return RedirectToLogin();

        if (result.Success)
        {
            FlashSuccess("Category updated.");
            return RedirectToAction(nameof(Categories), new { id = eventId });
        }

        FlashError(result.Error ?? "We couldn't update that category.");
        return RedirectToAction(nameof(EditCategory), new { id, eventId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id, int eventId)
    {
        var result = await _api.DeleteAsync($"/api/categories/{id}");
        if (result.IsUnauthorized) return RedirectToLogin();

        if (result.Success) FlashSuccess("Category removed.");
        else FlashError(result.Error ?? "We couldn't remove that category.");
        return RedirectToAction(nameof(Categories), new { id = eventId });
    }

    // ====================== Enrolments ======================

    /// <summary>All participants enrolled in an event, with their category and enrolment status.</summary>
    public async Task<IActionResult> Enrolments(int id)
    {
        var (ev, fail) = await LoadOwnedEventAsync(id);
        if (fail is not null) return fail;

        var result = await _api.GetAsync<List<EventEnrolment>>($"/api/events/{id}/enrollments");
        if (result.IsUnauthorized) return RedirectToLogin();
        if (!result.Success)
            FlashError(result.Error ?? "We couldn't load the enrolments right now.");

        return View(new EnrolmentsViewModel { Event = ev!, Enrolments = result.Data ?? new() });
    }

    /// <param name="id">The enrolment id.</param>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetEnrolmentStatus(int id, int eventId, string status)
    {
        var result = await _api.PutAsync<EnrolmentStatusDto>($"/api/enrollments/{id}/status", new { status });
        if (result.IsUnauthorized) return RedirectToLogin();

        if (result.Success) FlashSuccess($"Enrolment marked as {result.Data?.Status ?? status}.");
        else FlashError(result.Error ?? "We couldn't change that enrolment's status.");
        return RedirectToAction(nameof(Enrolments), new { id = eventId });
    }

    // ====================== Results ======================

    /// <summary>Capture finish times and positions for enrolled participants. Saved results appear on the public leaderboard.</summary>
    public async Task<IActionResult> Results(int id)
    {
        var (ev, fail) = await LoadOwnedEventAsync(id);
        if (fail is not null) return fail;

        var enrolments = await _api.GetAsync<List<EventEnrolment>>($"/api/events/{id}/enrollments");
        if (enrolments.IsUnauthorized) return RedirectToLogin();
        var leaderboard = await _api.GetAsync<List<LeaderboardEntry>>($"/api/events/{id}/results");

        if (!enrolments.Success)
            FlashError(enrolments.Error ?? "We couldn't load the participants right now.");

        var results = (leaderboard.Data ?? new()).ToDictionary(r => r.BibNumber, r => r);
        var rows = (enrolments.Data ?? new())
            .OrderBy(e => e.CategoryName).ThenBy(e => e.BibNumber)
            .Select(e => new ResultRow
            {
                Enrolment = e,
                Result = results.GetValueOrDefault(e.BibNumber)
            })
            .ToList();

        return View(new ResultsViewModel
        {
            Event = ev!,
            Rows = rows,
            CanCapture = ev!.EventDate.Date <= DateTime.UtcNow.Date
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveResult(ResultFormModel model)
    {
        if (!ModelState.IsValid)
        {
            FlashError(FirstValidationError());
            return RedirectToAction(nameof(Results), new { id = model.EventId });
        }

        // Normalise "0:48:32" to "00:48:32" so the API always receives hh:mm:ss
        if (!TimeSpan.TryParseExact(model.FinishTime, @"h\:mm\:ss", CultureInfo.InvariantCulture, out var time))
        {
            FlashError("Finish time must look like hh:mm:ss, for example 00:48:32.");
            return RedirectToAction(nameof(Results), new { id = model.EventId });
        }

        var body = new
        {
            finishTime = Format.Time(time),
            overallPosition = model.OverallPosition,
            categoryPosition = model.CategoryPosition
        };

        ApiResult<ResultDto> result = model.ResultId is { } resultId
            ? await _api.PutAsync<ResultDto>($"/api/results/{resultId}", body)
            : await _api.PostAsync<ResultDto>($"/api/enrollments/{model.EnrolmentId}/result", body);
        if (result.IsUnauthorized) return RedirectToLogin();

        if (result.Success) FlashSuccess("Result saved and published to the leaderboard.");
        else FlashError(result.Error ?? "We couldn't save that result.");
        return RedirectToAction(nameof(Results), new { id = model.EventId });
    }

    // ====================== helpers ======================

    private static OrganiserDashboard EmptyDashboard() =>
        new(0, 0, 0, null, null, new List<OrganiserEventRow>());

    private void ValidateEventType(EventFormModel model)
    {
        if (!EventTypes.Contains(model.EventType))
            ModelState.AddModelError(nameof(model.EventType), "Please choose Run, Walk or Cycle.");
    }

    private static object EventBody(EventFormModel m) => new
    {
        eventName = m.EventName.Trim(),
        eventDate = m.EventDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        location = m.Location.Trim(),
        description = m.Description.Trim(),
        routeInfo = string.IsNullOrWhiteSpace(m.RouteInfo) ? null : m.RouteInfo.Trim(),
        eventType = m.EventType,
        distanceKm = m.DistanceKm
    };

    private static object CategoryBody(CategoryFormModel m) => new
    {
        categoryName = m.CategoryName.Trim(),
        distanceKm = m.DistanceKm,
        entryFee = m.EntryFee,
        maxParticipants = m.MaxParticipants
    };

    /// <summary>Loads an event and makes sure it belongs to the signed-in organiser (the API enforces this too).</summary>
    private async Task<(EventDetail? Event, IActionResult? Fail)> LoadOwnedEventAsync(int id)
    {
        var result = await _api.GetAsync<EventDetail>($"/api/events/{id}");
        if (result.IsNotFound) return (null, NotFound());
        if (!result.Success || result.Data is null)
        {
            FlashError(result.Error ?? "We couldn't load that event right now.");
            return (null, RedirectToAction(nameof(Events)));
        }
        if (result.Data.OrganiserId != _session.UserId)
            return (null, RedirectToAction("AccessDenied", "Home"));

        return (result.Data, null);
    }
}
