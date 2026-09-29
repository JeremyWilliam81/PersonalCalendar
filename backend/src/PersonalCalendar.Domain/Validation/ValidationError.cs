namespace PersonalCalendar.Domain.Validation;

/// <summary>A single field-level validation failure, identified by a stable code the UI maps to a message.</summary>
public sealed record ValidationError(string Field, string Code);

public static class ErrorCodes
{
    public const string TitleRequired = "title.required";
    public const string TitleTooLong = "title.tooLong";
    public const string LocationTooLong = "location.tooLong";
    public const string NotesTooLong = "notes.tooLong";
    public const string StartRequired = "start.required";
    public const string EndRequired = "end.required";
    public const string StartDateRequired = "startDate.required";
    public const string EndDateRequired = "endDate.required";
    public const string EndNotAfterStart = "end.notAfterStart";
    public const string EndDateBeforeStart = "endDate.beforeStart";
    public const string TimeZoneUnknown = "timeZone.unknown";
}
