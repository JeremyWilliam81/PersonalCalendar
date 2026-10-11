using NodaTime;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Domain.Calendar;

/// <summary>Display order shared by the month grid and the timelines (research R6).</summary>
internal static class EventOrdering
{
    /// <summary>All-day events first (by start date, then title), then timed events (by start, then title).</summary>
    /// <param name="dayStart">
    /// The start of the day being ordered. A timed event that began before it counts as starting at
    /// <paramref name="dayStart"/>, so it sorts among that day's events by when it shows on the day (003 FR-032).
    /// </param>
    public static IReadOnlyList<CalendarItem> Order(IEnumerable<CalendarItem> events, Instant? dayStart = null) =>
        events
            .OrderBy(e => e.Schedule is AllDaySchedule ? 0 : 1)
            .ThenBy(e => e.Schedule is AllDaySchedule allDay ? allDay.StartDate : LocalDate.MinIsoValue)
            .ThenBy(e => e.Schedule is TimedSchedule timed ? StartOnDay(timed.Start, dayStart) : Instant.MinValue)
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Id.Value)
            .ThenBy(e => e.Occurrence?.OriginalDate ?? LocalDate.MinIsoValue)
            .ToList();

    private static Instant StartOnDay(Instant start, Instant? dayStart) =>
        dayStart is { } day && day > start ? day : start;
}
