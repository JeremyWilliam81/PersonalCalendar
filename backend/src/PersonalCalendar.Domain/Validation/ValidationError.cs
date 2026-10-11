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

    // Recurring events (specs/003-recurring-events/data-model.md).
    public const string RecurrenceFrequencyInvalid = "recurrence.frequency.invalid";
    public const string RecurrenceIntervalOutOfRange = "recurrence.interval.outOfRange";
    public const string RecurrenceWeekdaysRequired = "recurrence.weekdays.required";
    public const string RecurrenceMonthlyInvalid = "recurrence.monthly.invalid";
    public const string RecurrenceUntilBeforeStart = "recurrence.until.beforeStart";
    public const string RecurrenceCountOutOfRange = "recurrence.count.outOfRange";
    public const string OccurrenceRequired = "occurrence.required";
    public const string OccurrenceInvalid = "occurrence.invalid";
    public const string ScopeRequired = "scope.required";
    public const string ScopeInvalid = "scope.invalid";
    public const string ScopeThisWithRepeatChange = "scope.thisWithRepeatChange";
    public const string ScopeDateChangeRequiresThis = "scope.dateChangeRequiresThis";
    public const string ScopeDateAndRepeatChanged = "scope.dateAndRepeatChanged";
}
