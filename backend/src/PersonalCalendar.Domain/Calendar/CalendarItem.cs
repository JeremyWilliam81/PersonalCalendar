using NodaTime;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Domain.Calendar;

/// <summary>
/// Something to lay out on the calendar: a one-time event or one occurrence of a series (research S8).
/// Occurrences of the same series share <see cref="Id"/> and differ by <see cref="Occurrence"/>.
/// </summary>
public sealed record CalendarItem(EventId Id, string Title, EventSchedule Schedule, OccurrenceRef? Occurrence)
{
    public static CalendarItem FromEvent(CalendarEvent calendarEvent) =>
        new(calendarEvent.Id, calendarEvent.Title, calendarEvent.Schedule, null);
}

/// <summary>Identifies an occurrence by its original date in the series zone (research S4).</summary>
public sealed record OccurrenceRef(LocalDate OriginalDate, bool IsException);
