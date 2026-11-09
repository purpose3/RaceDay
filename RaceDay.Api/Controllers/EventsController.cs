using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IWeatherService _weather;
    private readonly IBlobStorageService _blobs;
    private readonly ILogger<EventsController> _logger;

    public EventsController(RaceDayDbContext db, IWeatherService weather,
        IBlobStorageService blobs, ILogger<EventsController> logger)
    {
        _db = db;
        _weather = weather;
        _blobs = blobs;
        _logger = logger;
    }

    /// <summary>Relative URL of the event's banner (served by this API), or null if it has none.</summary>
    internal static string? BannerUrlFor(Event e) =>
        e.BannerImage is null ? null : $"/api/events/{e.EventId}/banner";

    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private static EventSummaryResponse ToSummary(Event e) =>
        new(e.EventId, e.EventName, e.EventDate, e.Location, e.EventType, e.DistanceKm, BannerUrlFor(e));

    private static EventDetailResponse ToDetail(Event e) =>
        new(e.EventId, e.OrganiserId, e.EventName, e.EventDate, e.Location, e.Description, e.RouteInfo,
            e.EventType, e.DistanceKm, e.CreatedAt,
            e.Categories.Select(c => new CategoryResponse(
                c.CategoryId, c.EventId, c.CategoryName, c.DistanceKm, c.EntryFee, c.MaxParticipants)).ToList(),
            BannerUrlFor(e));

    /// <summary>Lists upcoming events, optionally filtered by location and/or date.</summary>
    /// <param name="location">Case-insensitive partial match on the event location.</param>
    /// <param name="date">Only events on this exact date (yyyy-MM-dd).</param>
    /// <response code="200">Array of upcoming event summaries, soonest first.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<EventSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents([FromQuery] string? location, [FromQuery] DateTime? date)
    {
        var query = _db.Events.AsNoTracking().Where(e => e.EventDate >= DateTime.UtcNow.Date);

        if (!string.IsNullOrWhiteSpace(location))
        {
            var loc = location.Trim();
            query = query.Where(e => e.Location.Contains(loc));
        }

        if (date.HasValue)
        {
            var start = date.Value.Date;
            var end = start.AddDays(1);
            query = query.Where(e => e.EventDate >= start && e.EventDate < end);
        }

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(ToSummary).ToList());
    }

    /// <summary>Returns full details for one event, including its categories.</summary>
    /// <response code="200">The event with nested categories.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("{eventId:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(int eventId)
    {
        var e = await _db.Events.AsNoTracking().Include(x => x.Categories)
            .SingleOrDefaultAsync(x => x.EventId == eventId);
        return e is null ? NotFound(new { error = "Event not found." }) : Ok(ToDetail(e));
    }

    /// <summary>Creates a new event owned by the logged-in Organiser.</summary>
    /// <response code="201">The created event.</response>
    /// <response code="400">Validation failed or the event date is in the past.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant.</response>
    [HttpPost]
    [Authorize(Roles = "Organiser")]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateEvent([FromBody] EventRequest req)
    {
        if (req.EventDate.Date < DateTime.UtcNow.Date)
            return BadRequest(new { error = "Event date cannot be in the past." });

        var e = new Event
        {
            OrganiserId = CurrentUserId,
            EventName = req.EventName.Trim(),
            EventDate = req.EventDate.Date,
            Location = req.Location.Trim(),
            Description = req.Description.Trim(),
            RouteInfo = req.RouteInfo?.Trim(),
            EventType = req.EventType,
            DistanceKm = req.DistanceKm,
            CreatedAt = DateTime.UtcNow
        };
        _db.Events.Add(e);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEvent), new { eventId = e.EventId }, ToDetail(e));
    }

    /// <summary>Updates an event owned by the logged-in Organiser.</summary>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">The event belongs to a different Organiser, or the caller is a Participant.</response>
    /// <response code="404">No event with that id.</response>
    [HttpPut("{eventId:int}")]
    [Authorize(Roles = "Organiser")]
    [ProducesResponseType(typeof(EventDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEvent(int eventId, [FromBody] EventRequest req)
    {
        var e = await _db.Events.Include(x => x.Categories).SingleOrDefaultAsync(x => x.EventId == eventId);
        if (e is null) return NotFound(new { error = "Event not found." });
        if (e.OrganiserId != CurrentUserId) return Forbid();

        e.EventName = req.EventName.Trim();
        e.EventDate = req.EventDate.Date;
        e.Location = req.Location.Trim();
        e.Description = req.Description.Trim();
        e.RouteInfo = req.RouteInfo?.Trim();
        e.EventType = req.EventType;
        e.DistanceKm = req.DistanceKm;
        await _db.SaveChangesAsync();

        return Ok(ToDetail(e));
    }

    /// <summary>Deletes an event owned by the logged-in Organiser.</summary>
    /// <remarks>Events that already have enrolments cannot be deleted.</remarks>
    /// <response code="204">Event (and its categories) deleted.</response>
    /// <response code="403">The event belongs to a different Organiser, or the caller is a Participant.</response>
    /// <response code="404">No event with that id.</response>
    /// <response code="409">The event has enrolments, so it cannot be deleted.</response>
    [HttpDelete("{eventId:int}")]
    [Authorize(Roles = "Organiser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(int eventId)
    {
        var e = await _db.Events.SingleOrDefaultAsync(x => x.EventId == eventId);
        if (e is null) return NotFound(new { error = "Event not found." });
        if (e.OrganiserId != CurrentUserId) return Forbid();

        // Enrollments -> Categories is Restrict, so block deletes that would orphan enrolments.
        var hasEnrolments = await _db.Enrollments.AnyAsync(en => en.Category.EventId == eventId);
        if (hasEnrolments)
            return Conflict(new { error = "This event has enrolments and cannot be deleted." });

        var bannerBlob = e.BannerImage;
        _db.Events.Remove(e);
        await _db.SaveChangesAsync();
        if (bannerBlob is not null) await TryDeleteBlobAsync(bannerBlob);
        return NoContent();
    }

    /// <summary>Returns the weather forecast for the event's location and date.</summary>
    /// <remarks>Forecasts only exist for roughly the next 16 days.</remarks>
    /// <response code="200">Forecast for the event day.</response>
    /// <response code="404">Event not found, or no forecast is available for its location/date.</response>
    [HttpGet("{eventId:int}/weather")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(WeatherResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWeather(int eventId, CancellationToken ct)
    {
        var e = await _db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.EventId == eventId, ct);
        if (e is null) return NotFound(new { error = "Event not found." });

        var w = await _weather.GetForecastAsync(e.Location, e.EventDate, ct);
        if (w is null)
            return NotFound(new { error = "No forecast available for this event's location and date yet." });

        return Ok(new WeatherResponse(e.EventId, w.ForecastDate, w.TempC, w.Conditions, w.WindSpeed));
    }

    /// <summary>Uploads (or replaces) the banner image of an event owned by the logged-in Organiser.</summary>
    /// <remarks>
    /// Send multipart/form-data with one file field named "file" (JPEG, PNG or WebP, up to 5 MB).
    /// The image is stored in Azure Blob Storage and replaces any previous banner.
    /// </remarks>
    /// <response code="200">Banner stored; returns the URL where it can be fetched from this API.</response>
    /// <response code="400">No file, file too large, or not a JPEG/PNG/WebP image.</response>
    /// <response code="403">The event belongs to a different Organiser, or the caller is a Participant.</response>
    /// <response code="404">No event with that id.</response>
    /// <response code="503">Image storage is temporarily unavailable.</response>
    [HttpPost("{eventId:int}/banner")]
    [Authorize(Roles = "Organiser")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6_000_000)]
    [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadBanner(int eventId, [FromForm] UploadImageRequest req, CancellationToken ct)
    {
        var e = await _db.Events.SingleOrDefaultAsync(x => x.EventId == eventId, ct);
        if (e is null) return NotFound(new { error = "Event not found." });
        if (e.OrganiserId != CurrentUserId) return Forbid();

        var check = ImageValidator.Validate(req.File);
        if (!check.IsValid) return BadRequest(new { error = check.Error });

        string newBlob;
        try
        {
            await using var stream = req.File!.OpenReadStream();
            newBlob = await _blobs.UploadAsync($"events/{eventId}", stream, check.ContentType!, check.Extension!, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Banner upload failed for event {EventId}", eventId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Image storage is currently unavailable. Please try again later." });
        }

        var previous = e.BannerImage;
        e.BannerImage = newBlob;
        await _db.SaveChangesAsync(ct);
        if (previous is not null) await TryDeleteBlobAsync(previous);

        return Ok(new ImageUploadResponse(BannerUrlFor(e)!));
    }

    /// <summary>Returns the event's banner image (streamed from Azure Blob Storage through this API).</summary>
    /// <response code="200">The image file.</response>
    /// <response code="404">The event does not exist or has no banner.</response>
    /// <response code="503">Image storage is temporarily unavailable.</response>
    [HttpGet("{eventId:int}/banner")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetBanner(int eventId, CancellationToken ct)
    {
        var blobName = await _db.Events.AsNoTracking()
            .Where(x => x.EventId == eventId)
            .Select(x => x.BannerImage)
            .SingleOrDefaultAsync(ct);
        if (blobName is null) return NotFound(new { error = "This event has no banner image." });

        BlobContent? blob;
        try
        {
            blob = await _blobs.OpenReadAsync(blobName, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Banner download failed for event {EventId}", eventId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Image storage is currently unavailable. Please try again later." });
        }
        if (blob is null) return NotFound(new { error = "This event has no banner image." });

        Response.Headers.CacheControl = "public, max-age=300";
        return File(blob.Content, blob.ContentType);
    }

    private async Task TryDeleteBlobAsync(string blobName)
    {
        try { await _blobs.DeleteAsync(blobName); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not delete blob {Blob}", blobName); }
    }
}
