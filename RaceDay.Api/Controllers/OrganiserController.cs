using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/organiser")]
[Authorize(Roles = "Organiser")]
public class OrganiserController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    public OrganiserController(RaceDayDbContext db) => _db = db;

    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    /// <summary>Dashboard for the logged-in Organiser: their events with enrolment totals and upcoming dates.</summary>
    /// <remarks>
    /// Upcoming events are listed first (soonest first), followed by past events (most recent first).
    /// Also returns overall totals and the next upcoming event.
    /// </remarks>
    /// <response code="200">Summary totals plus one row per event the Organiser created.</response>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(OrganiserDashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDashboard()
    {
        var me = CurrentUserId;
        var today = DateTime.UtcNow.Date;

        var events = await _db.Events.AsNoTracking()
            .Include(e => e.Categories)
            .Where(e => e.OrganiserId == me)
            .ToListAsync();

        var enrolmentCounts = await _db.Enrollments.AsNoTracking()
            .Where(en => en.Category.Event.OrganiserId == me)
            .GroupBy(en => en.Category.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count);

        var resultCounts = await _db.Results.AsNoTracking()
            .Where(r => r.Enrollment.Category.Event.OrganiserId == me)
            .GroupBy(r => r.Enrollment.Category.EventId)
            .Select(g => new { EventId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EventId, x => x.Count);

        var items = events.Select(e => new OrganiserEventSummary(
            e.EventId, e.EventName, e.EventDate, e.Location, e.EventType, e.DistanceKm,
            EventsController.BannerUrlFor(e),
            e.EventDate.Date >= today,
            e.Categories.Count,
            enrolmentCounts.GetValueOrDefault(e.EventId),
            resultCounts.GetValueOrDefault(e.EventId))).ToList();

        var ordered = items.Where(i => i.IsUpcoming).OrderBy(i => i.EventDate)
            .Concat(items.Where(i => !i.IsUpcoming).OrderByDescending(i => i.EventDate))
            .ToList();
        var next = ordered.FirstOrDefault(i => i.IsUpcoming);

        return Ok(new OrganiserDashboardResponse(
            items.Count,
            items.Count(i => i.IsUpcoming),
            items.Sum(i => i.TotalEnrolments),
            next?.EventDate,
            next?.EventName,
            ordered));
    }
}
