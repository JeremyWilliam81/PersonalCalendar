using NodaTime;

namespace PersonalCalendar.Domain.Events;

/// <summary>
/// One occurrence of a series that was deleted (RFC 5545 EXDATE) or changed on its own (RECURRENCE-ID), keyed by its
/// original date (research S4, S5). A changed occurrence holds a full snapshot, so it simply replaces the generated one.
/// </summary>
public sealed record OccurrenceException
{
    private OccurrenceException(LocalDate originalDate, bool isDeleted, string? title, string? location, string? notes, EventSchedule? schedule)
    {
        OriginalDate = originalDate;
        IsDeleted = isDeleted;
        Title = title;
        Location = location;
        Notes = notes;
        Schedule = schedule;
    }

    public LocalDate OriginalDate { get; }

    public bool IsDeleted { get; }

    /// <summary>Set when changed; null when deleted.</summary>
    public string? Title { get; }

    public string? Location { get; }

    public string? Notes { get; }

    /// <summary>Set when changed; may fall on any date (spec Edge Cases). Null when deleted.</summary>
    public EventSchedule? Schedule { get; }

    public static OccurrenceException Deleted(LocalDate originalDate) => new(originalDate, true, null, null, null, null);

    /// <summary>The values must already be validated (see <see cref="CalendarEvent.EditOccurrence"/>).</summary>
    public static OccurrenceException Changed(LocalDate originalDate, string title, string? location, string? notes, EventSchedule schedule) =>
        new(originalDate, false, title, location, notes, schedule);

    public OccurrenceException WithText(string title, string? location, string? notes) =>
        IsDeleted ? this : new(OriginalDate, false, title, location, notes, Schedule);
}
