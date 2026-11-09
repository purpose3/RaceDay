using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "That doesn't look like a valid email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please enter your password.")]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please enter your first name."), MaxLength(30)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Please enter your last name."), MaxLength(30)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "Please enter your email address."), MaxLength(100)]
    [EmailAddress(ErrorMessage = "That doesn't look like a valid email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Please enter your phone number."), MaxLength(30)]
    [Phone(ErrorMessage = "That doesn't look like a valid phone number.")]
    [Display(Name = "Phone number")]
    public string PhoneNumber { get; set; } = "";

    [Required(ErrorMessage = "Please choose a password.")]
    [MinLength(8, ErrorMessage = "Your password must be at least 8 characters.")]
    [MaxLength(100)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";

    [Required]
    public string Role { get; set; } = "Participant";

    [Display(Name = "Organiser invite code")]
    public string? InviteCode { get; set; }
}

public class UpdateProfileModel
{
    [Required(ErrorMessage = "Please enter your first name."), MaxLength(30)]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Please enter your last name."), MaxLength(30)]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "Please enter your phone number."), MaxLength(30)]
    [Phone(ErrorMessage = "That doesn't look like a valid phone number.")]
    public string PhoneNumber { get; set; } = "";
}

public class ChangePasswordModel
{
    [Required(ErrorMessage = "Please enter your current password.")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Please enter a new password.")]
    [MinLength(8, ErrorMessage = "Your new password must be at least 8 characters.")]
    [MaxLength(100)]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Please confirm your new password.")]
    [Compare(nameof(NewPassword), ErrorMessage = "The new passwords do not match.")]
    public string ConfirmNewPassword { get; set; } = "";
}

public class EventDetailViewModel
{
    public EventDetail Event { get; set; } = null!;
    public WeatherDto? Weather { get; set; }
}

public class EventFormModel
{
    public int? EventId { get; set; }
    public bool HasBanner { get; set; }

    [Required(ErrorMessage = "Please enter the event name."), MaxLength(100)]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = "";

    [Required(ErrorMessage = "Please describe the event."), MaxLength(300, ErrorMessage = "The description can be at most 300 characters.")]
    public string Description { get; set; } = "";

    [Required(ErrorMessage = "Please choose the event date.")]
    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Please enter the location."), MaxLength(100)]
    public string Location { get; set; } = "";

    [MaxLength(100, ErrorMessage = "Route information can be at most 100 characters.")]
    [Display(Name = "Route information")]
    public string? RouteInfo { get; set; }

    [Required(ErrorMessage = "Please choose the event type.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    [Required(ErrorMessage = "Please enter the distance.")]
    [Range(0.1, 1000, ErrorMessage = "The distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }
}

public class CategoryFormModel
{
    [Required(ErrorMessage = "Please enter a category name, for example 10km Road Race or Under 20."), MaxLength(100)]
    [Display(Name = "Category name")]
    public string CategoryName { get; set; } = "";

    [Required(ErrorMessage = "Please enter the distance.")]
    [Range(0.1, 1000, ErrorMessage = "The distance must be between 0.1 and 1000 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Required(ErrorMessage = "Please enter the entry fee (use 0 for free).")]
    [Range(0, 100000, ErrorMessage = "The entry fee must be between 0 and 100 000.")]
    [Display(Name = "Entry fee (R)")]
    public decimal? EntryFee { get; set; }

    [Required(ErrorMessage = "Please enter the maximum number of participants.")]
    [Range(1, 100000, ErrorMessage = "Maximum participants must be at least 1.")]
    [Display(Name = "Max participants")]
    public int? MaxParticipants { get; set; }
}

public class CategoriesViewModel
{
    public EventDetail Event { get; set; } = null!;
    public CategoryFormModel NewCategory { get; set; } = new();
}

public class CategoryEditViewModel
{
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public int CategoryId { get; set; }
    public CategoryFormModel Form { get; set; } = new();
}

public class EnrolmentsViewModel
{
    public EventDetail Event { get; set; } = null!;
    public List<EventEnrolment> Enrolments { get; set; } = new();
}

public class ResultFormModel
{
    public int EventId { get; set; }
    public int EnrolmentId { get; set; }
    public int? ResultId { get; set; }

    [Required(ErrorMessage = "Please enter the finish time as hh:mm:ss, for example 00:48:32.")]
    [RegularExpression(@"^\d{1,2}:[0-5]\d:[0-5]\d$", ErrorMessage = "Finish time must look like hh:mm:ss, for example 00:48:32.")]
    public string FinishTime { get; set; } = "";

    [Range(1, 100000, ErrorMessage = "Overall position must be 1 or more.")]
    public int? OverallPosition { get; set; }

    [Range(1, 100000, ErrorMessage = "Category position must be 1 or more.")]
    public int? CategoryPosition { get; set; }
}

public class ResultRow
{
    public EventEnrolment Enrolment { get; set; } = null!;
    public LeaderboardEntry? Result { get; set; }
}

public class ResultsViewModel
{
    public EventDetail Event { get; set; } = null!;
    public List<ResultRow> Rows { get; set; } = new();
    public bool CanCapture { get; set; }
}
