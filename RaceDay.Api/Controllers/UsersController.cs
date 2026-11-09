using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Organiser,Participant")]
public class UsersController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IBlobStorageService _blobs;
    private readonly ILogger<UsersController> _logger;

    public UsersController(RaceDayDbContext db, IBlobStorageService blobs, ILogger<UsersController> logger)
    {
        _db = db;
        _blobs = blobs;
        _logger = logger;
    }

    // The user id always comes from the token, never from the request,
    // so a user can only ever see or change their own profile.
    private int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private Task<User?> LoadCurrentUser() =>
        _db.Users.Include(u => u.Role).SingleOrDefaultAsync(u => u.UserId == CurrentUserId);

    private static UserProfileResponse ToResponse(User u) =>
        new(u.UserId, u.FirstName, u.LastName, u.Email, u.PhoneNumber, u.Role.RoleName, u.CreatedAt,
            u.ProfilePicture is null ? null : "/api/users/me/picture");

    /// <summary>Returns the logged-in user's own profile.</summary>
    /// <response code="200">The profile of the authenticated user.</response>
    /// <response code="401">Missing/invalid token, or the account no longer exists.</response>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe()
    {
        var user = await LoadCurrentUser();
        return user is null ? Unauthorized() : Ok(ToResponse(user));
    }

    /// <summary>Updates the logged-in user's first name, last name and phone number.</summary>
    /// <remarks>Email and role cannot be changed through this endpoint.</remarks>
    /// <response code="200">The updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Missing/invalid token, or the account no longer exists.</response>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest req)
    {
        var user = await LoadCurrentUser();
        if (user is null) return Unauthorized();

        user.FirstName = req.FirstName.Trim();
        user.LastName = req.LastName.Trim();
        user.PhoneNumber = req.PhoneNumber.Trim();
        await _db.SaveChangesAsync();

        return Ok(ToResponse(user));
    }

    /// <summary>Changes the logged-in user's password.</summary>
    /// <remarks>Requires the current password. The new password is stored as a BCrypt hash.</remarks>
    /// <response code="204">Password changed.</response>
    /// <response code="400">Current password incorrect, new password too short, or same as the current one.</response>
    /// <response code="401">Missing/invalid token, or the account no longer exists.</response>
    [HttpPut("me/password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null) return Unauthorized();

        // 400 (not 401): the caller IS authenticated, the supplied current password is just wrong.
        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            return BadRequest(new { error = "Current password is incorrect." });

        if (req.CurrentPassword == req.NewPassword)
            return BadRequest(new { error = "New password must be different from the current password." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>Uploads (or replaces) the logged-in user's profile picture.</summary>
    /// <remarks>Send multipart/form-data with one file field named "file" (JPEG, PNG or WebP, up to 5 MB). Stored in Azure Blob Storage.</remarks>
    /// <response code="200">Picture stored; returns the URL where it can be fetched from this API.</response>
    /// <response code="400">No file, file too large, or not a JPEG/PNG/WebP image.</response>
    /// <response code="401">Missing/invalid token, or the account no longer exists.</response>
    /// <response code="503">Image storage is temporarily unavailable.</response>
    [HttpPost("me/picture")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6_000_000)]
    [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadPicture([FromForm] UploadImageRequest req, CancellationToken ct)
    {
        var user = await _db.Users.SingleOrDefaultAsync(u => u.UserId == CurrentUserId, ct);
        if (user is null) return Unauthorized();

        var check = ImageValidator.Validate(req.File);
        if (!check.IsValid) return BadRequest(new { error = check.Error });

        string newBlob;
        try
        {
            await using var stream = req.File!.OpenReadStream();
            newBlob = await _blobs.UploadAsync($"users/{user.UserId}", stream, check.ContentType!, check.Extension!, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Profile picture upload failed for user {UserId}", user.UserId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Image storage is currently unavailable. Please try again later." });
        }

        var previous = user.ProfilePicture;
        user.ProfilePicture = newBlob;
        await _db.SaveChangesAsync(ct);

        if (previous is not null)
        {
            try { await _blobs.DeleteAsync(previous); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete blob {Blob}", previous); }
        }

        return Ok(new ImageUploadResponse("/api/users/me/picture"));
    }

    /// <summary>Returns the logged-in user's own profile picture.</summary>
    /// <response code="200">The image file.</response>
    /// <response code="404">The user has no profile picture.</response>
    /// <response code="503">Image storage is temporarily unavailable.</response>
    [HttpGet("me/picture")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetPicture(CancellationToken ct)
    {
        var blobName = await _db.Users.AsNoTracking()
            .Where(u => u.UserId == CurrentUserId)
            .Select(u => u.ProfilePicture)
            .SingleOrDefaultAsync(ct);
        if (blobName is null) return NotFound(new { error = "No profile picture has been uploaded." });

        BlobContent? blob;
        try
        {
            blob = await _blobs.OpenReadAsync(blobName, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Profile picture download failed for user {UserId}", CurrentUserId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Image storage is currently unavailable. Please try again later." });
        }
        if (blob is null) return NotFound(new { error = "No profile picture has been uploaded." });

        Response.Headers.CacheControl = "private, max-age=60";
        return File(blob.Content, blob.ContentType);
    }
}
