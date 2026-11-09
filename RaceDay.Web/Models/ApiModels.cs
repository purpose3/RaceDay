namespace RaceDay.Web.Models;

// Shapes of the JSON the Part 2 API returns. The MVC app only displays this data; it never changes the rules.

public record LoginResponse(int UserId, string Role, string Token, DateTime ExpiresAt);

public record UserProfile(
    int UserId, string FirstName, string LastName, string Email,
    string PhoneNumber, string Role, DateTime CreatedAt, string? ProfilePictureUrl);

public record EventSummary(
    int EventId, string EventName, DateTime EventDate, string Location,
    string EventType, decimal DistanceKm, string? BannerUrl);

public record CategoryDto(
    int CategoryId, int EventId, string CategoryName,
    decimal DistanceKm, decimal EntryFee, int MaxParticipants);

public record EventDetail(
    int EventId, int OrganiserId, string EventName, DateTime EventDate, string Location,
    string Description, string? RouteInfo, string EventType, decimal DistanceKm,
    DateTime CreatedAt, List<CategoryDto> Categories, string? BannerUrl);

public record WeatherDto(int EventId, DateTime ForecastDate, double TempC, string Conditions, double WindSpeed);

public record EnrolmentCreated(int EnrolmentId, string BibNumber, string Status);

public record MyEnrolment(
    int EnrolmentId, int CategoryId, string CategoryName,
    int EventId, string EventName, DateTime EventDate, string Location,
    string BibNumber, DateTime EnrolmentDate, string Status);

public record MyResult(
    int ResultId, int EventId, string EventName, DateTime EventDate, string Location,
    int CategoryId, string CategoryName, string BibNumber,
    TimeSpan FinishTime, int? OverallPosition, int? CategoryPosition,
    int TotalFinishers, int CategoryFinishers);

public record ImageUploadResult(string ImageUrl);

public record OrganiserEventRow(
    int EventId, string EventName, DateTime EventDate, string Location,
    string EventType, decimal DistanceKm, string? BannerUrl,
    bool IsUpcoming, int CategoryCount, int TotalEnrolments, int ResultsCaptured);

public record OrganiserDashboard(
    int TotalEvents, int UpcomingEvents, int TotalEnrolments,
    DateTime? NextEventDate, string? NextEventName, List<OrganiserEventRow> Events);

public record ParticipantDto(int UserId, string FirstName, string LastName, string Email, string PhoneNumber);

public record EventEnrolment(
    int EnrolmentId, int CategoryId, string CategoryName, string BibNumber,
    DateTime EnrolmentDate, string Status, bool HasResult, ParticipantDto Participant);

public record EnrolmentStatusDto(int EnrolmentId, string Status);

public record LeaderboardEntry(
    int ResultId, string BibNumber, string ParticipantName, int CategoryId, string CategoryName,
    TimeSpan FinishTime, int? OverallPosition, int? CategoryPosition);

public record ResultDto(
    int ResultId, int EnrolmentId, TimeSpan FinishTime,
    int? OverallPosition, int? CategoryPosition, DateTime RecordedAt);
