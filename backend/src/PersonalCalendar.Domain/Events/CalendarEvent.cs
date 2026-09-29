using NodaTime;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Domain.Events;

/// <summary>An event on the single user's calendar (data-model.md).</summary>
public sealed class CalendarEvent
{
    public const int TitleMaxLength = 200;
    public const int LocationMaxLength = 200;
    public const int NotesMaxLength = 5000;

    private CalendarEvent(
        EventId id,
        string title,
        string? location,
        string? notes,
        EventSchedule schedule,
        string entryTimeZone,
        Instant createdUtc,
        Instant updatedUtc,
        int version)
    {
        Id = id;
        Title = title;
        Location = location;
        Notes = notes;
        Schedule = schedule;
        EntryTimeZone = entryTimeZone;
        CreatedUtc = createdUtc;
        UpdatedUtc = updatedUtc;
        Version = version;
    }

    public EventId Id { get; }

    public string Title { get; private set; }

    public string? Location { get; private set; }

    public string? Notes { get; private set; }

    public EventSchedule Schedule { get; private set; }

    /// <summary>IANA id of the zone the event was last entered in. Not used for display (clarification Q2).</summary>
    public string EntryTimeZone { get; private set; }

    public Instant CreatedUtc { get; }

    public Instant UpdatedUtc { get; private set; }

    public int Version { get; private set; }

    public static Validated<CalendarEvent> Create(
        string? title,
        string? location,
        string? notes,
        EventSchedule schedule,
        DateTimeZone entryZone,
        IClock clock)
    {
        var fields = ValidateFields(title, location, notes);
        if (!fields.Errors.IsValid) return Validated<CalendarEvent>.Failure(fields.Errors);

        var now = clock.GetCurrentInstant();
        return Validated<CalendarEvent>.Success(new CalendarEvent(
            EventId.New(), fields.Title, fields.Location, fields.Notes, schedule, entryZone.Id, now, now, version: 1));
    }

    /// <summary>Replaces every editable field. On invalid input nothing changes and every error is returned.</summary>
    public ValidationResult Update(
        string? title,
        string? location,
        string? notes,
        EventSchedule schedule,
        DateTimeZone entryZone,
        IClock clock)
    {
        var fields = ValidateFields(title, location, notes);
        if (!fields.Errors.IsValid) return fields.Errors;

        Title = fields.Title;
        Location = fields.Location;
        Notes = fields.Notes;
        Schedule = schedule;
        EntryTimeZone = entryZone.Id;
        UpdatedUtc = clock.GetCurrentInstant();
        Version++;
        return fields.Errors;
    }

    /// <summary>Recreates an event that was already validated and stored. Used by persistence only.</summary>
    public static CalendarEvent Rehydrate(
        EventId id,
        string title,
        string? location,
        string? notes,
        EventSchedule schedule,
        string entryTimeZone,
        Instant createdUtc,
        Instant updatedUtc,
        int version) =>
        new(id, title, location, notes, schedule, entryTimeZone, createdUtc, updatedUtc, version);

    /// <summary>Validates the text fields only, e.g. to report them alongside schedule errors.</summary>
    public static ValidationResult ValidateTextFields(string? title, string? location, string? notes) =>
        ValidateFields(title, location, notes).Errors;

    private static (string Title, string? Location, string? Notes, ValidationResult Errors) ValidateFields(
        string? title, string? location, string? notes)
    {
        var errors = new ValidationResult();

        var trimmedTitle = title?.Trim() ?? string.Empty;
        if (trimmedTitle.Length == 0) errors.Add("title", ErrorCodes.TitleRequired);
        else if (trimmedTitle.Length > TitleMaxLength) errors.Add("title", ErrorCodes.TitleTooLong);

        var trimmedLocation = NullIfEmpty(location);
        if (trimmedLocation?.Length > LocationMaxLength) errors.Add("location", ErrorCodes.LocationTooLong);

        var trimmedNotes = NullIfEmpty(notes);
        if (trimmedNotes?.Length > NotesMaxLength) errors.Add("notes", ErrorCodes.NotesTooLong);

        return (trimmedTitle, trimmedLocation, trimmedNotes, errors);
    }

    private static string? NullIfEmpty(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
