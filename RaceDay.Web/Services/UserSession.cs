namespace RaceDay.Web.Services;

/// <summary>
/// The signed-in user's state, kept in server-side session: JWT, role, name and expiry.
/// This is what keeps the user's authenticated state and role across every page.
/// </summary>
public class UserSession
{
    private readonly IHttpContextAccessor _accessor;
    public UserSession(IHttpContextAccessor accessor) => _accessor = accessor;

    private ISession? Session => _accessor.HttpContext?.Session;

    public string? Token => Session?.GetString("token");
    public string? Role => Session?.GetString("role");
    public string FirstName => Session?.GetString("firstName") ?? "";
    public int UserId => Session?.GetInt32("userId") ?? 0;
    public bool HasPicture => Session?.GetInt32("hasPicture") == 1;
    public long PictureVersion => long.TryParse(Session?.GetString("pictureVersion"), out var v) ? v : 0;

    public DateTime? ExpiresAtUtc =>
        long.TryParse(Session?.GetString("expiresAt"), out var ticks) ? new DateTime(ticks, DateTimeKind.Utc) : null;

    public bool IsExpired => ExpiresAtUtc is { } e && e <= DateTime.UtcNow;
    public bool IsAuthenticated => !string.IsNullOrEmpty(Token) && !IsExpired;
    public bool IsOrganiser => IsAuthenticated && Role == "Organiser";
    public bool IsParticipant => IsAuthenticated && Role == "Participant";

    public void SignIn(int userId, string role, string token, DateTime expiresAt)
    {
        Session?.Clear();   // always start from a clean session
        Session?.SetInt32("userId", userId);
        Session?.SetString("role", role);
        Session?.SetString("token", token);
        Session?.SetString("expiresAt", expiresAt.ToUniversalTime().Ticks.ToString());
    }

    public void SetProfile(string firstName, bool hasPicture)
    {
        Session?.SetString("firstName", firstName);
        Session?.SetInt32("hasPicture", hasPicture ? 1 : 0);
    }

    /// <summary>Changes the image URL version so the browser shows a freshly uploaded picture.</summary>
    public void TouchPicture() => Session?.SetString("pictureVersion", DateTime.UtcNow.Ticks.ToString());

    public void SignOut() => Session?.Clear();
}
