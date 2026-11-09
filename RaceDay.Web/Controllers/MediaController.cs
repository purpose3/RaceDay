using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

/// <summary>
/// Serves images to the browser by fetching them from the API. The MVC application never talks
/// to Azure Blob Storage directly: all file handling goes through the Part 2 API.
/// </summary>
public class MediaController : Controller
{
    private readonly ApiClient _api;
    public MediaController(ApiClient api) => _api = api;

    [HttpGet("media/event/{id:int}")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> EventBanner(int id)
    {
        var image = await _api.GetImageAsync($"/api/events/{id}/banner");
        if (!image.Success || image.Data is null) return NotFound();
        return File(image.Data.Bytes, image.Data.ContentType);
    }

    [HttpGet("media/profile")]
    [RequireLogin]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> ProfilePicture()
    {
        var image = await _api.GetImageAsync("/api/users/me/picture");
        if (!image.Success || image.Data is null) return NotFound();
        return File(image.Data.Bytes, image.Data.ContentType);
    }
}
