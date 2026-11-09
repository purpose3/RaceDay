using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

public static class EnrolmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public static readonly string[] All = { Pending, Confirmed };
}

public record EnrolmentCreatedResponse(int EnrolmentId, string BibNumber, string Status);

public record MyEnrolmentResponse(
    int EnrolmentId, int CategoryId, string CategoryName,
    int EventId, string EventName, DateTime EventDate, string Location,
    string BibNumber, DateTime EnrolmentDate, string Status);

public record ParticipantDetails(int UserId, string FirstName, string LastName, string Email, string PhoneNumber);

public record CategoryEnrolmentResponse(
    int EnrolmentId, int CategoryId, string BibNumber, DateTime EnrolmentDate, string Status,
    ParticipantDetails Participant);

public record EventEnrolmentResponse(
    int EnrolmentId, int CategoryId, string CategoryName, string BibNumber,
    DateTime EnrolmentDate, string Status, bool HasResult, ParticipantDetails Participant);

public class UpdateEnrolmentStatusRequest
{
    /// <summary>"Pending" or "Confirmed".</summary>
    [Required] public string Status { get; set; } = "";
}

public record EnrolmentStatusResponse(int EnrolmentId, string Status);
