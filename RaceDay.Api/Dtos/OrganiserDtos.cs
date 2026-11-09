using RaceDay.Api.Data;

namespace RaceDay.Api.Dtos;

public record OrganiserEventSummary(
    int EventId, string EventName, DateTime EventDate, string Location,
    EventType EventType, decimal DistanceKm, string? BannerUrl,
    bool IsUpcoming, int CategoryCount, int TotalEnrolments, int ResultsCaptured);

public record OrganiserDashboardResponse(
    int TotalEvents, int UpcomingEvents, int TotalEnrolments,
    DateTime? NextEventDate, string? NextEventName,
    List<OrganiserEventSummary> Events);
