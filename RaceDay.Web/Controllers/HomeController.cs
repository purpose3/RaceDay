using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : AppController
{
    private readonly ApiClient _api;
    public HomeController(ApiClient api) => _api = api;

    /// <summary>Home page: upcoming events with filters. Open to everyone.</summary>
    public async Task<IActionResult> Index(string? location, DateTime? date)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(location)) query.Add("location=" + Uri.EscapeDataString(location.Trim()));
        if (date.HasValue) query.Add("date=" + date.Value.ToString("yyyy-MM-dd"));
        var url = "/api/events" + (query.Count > 0 ? "?" + string.Join("&", query) : "");

        ViewBag.Location = location;
        ViewBag.Date = date?.ToString("yyyy-MM-dd");

        var result = await _api.GetAsync<List<EventSummary>>(url);
        if (!result.Success)
        {
            FlashError(result.Error ?? "We couldn't load the events right now.");
            return View(new List<EventSummary>());
        }
        return View(result.Data ?? new List<EventSummary>());
    }

    /// <summary>Full event information and available categories.</summary>
    public async Task<IActionResult> Details(int id)
    {
        var result = await _api.GetAsync<EventDetail>($"/api/events/{id}");
        if (result.IsNotFound) return NotFound();
        if (!result.Success || result.Data is null)
        {
            FlashError(result.Error ?? "We couldn't load that event right now.");
            return RedirectToAction(nameof(Index));
        }

        var model = new EventDetailViewModel { Event = result.Data };

        // Weather is a nice-to-have: only for upcoming events, and a failure is simply ignored
        if (result.Data.EventDate.Date >= DateTime.UtcNow.Date)
        {
            var weather = await _api.GetAsync<WeatherDto>($"/api/events/{id}/weather");
            if (weather.Success) model.Weather = weather.Data;
        }

        return View(model);
    }

    public IActionResult AccessDenied() => View();

    /// <summary>Shown for unhandled errors. Never displays exception details.</summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();

    /// <summary>Friendly page for 404 and other status codes.</summary>
    public IActionResult HttpStatus(int code)
    {
        Response.StatusCode = code;
        ViewBag.Code = code;
        return View();
    }
}
