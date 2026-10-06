using NodaTime;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Domain.Calendar;

/// <summary>Display order shared by the month grid and the timelines (research R6).</summary>
internal static class EventOrdering
{
    /// <summary>All-day events first (by start date, then title), then timed events (by start, then title).</summary>
    public static IReadOnlyList<CalendarEvent> Order(IEnumerable<CalendarEvent> events) =>
        events
            .OrderBy(e => e.Schedule is AllDaySchedule ? 0 : 1)
            .ThenBy(e => e.Schedule is AllDaySchedule allDay ? allDay.StartDate : LocalDate.MinIsoValue)
            .ThenBy(e => e.Schedule is TimedSchedule timed ? timed.Start : Instant.MinValue)
            .ThenBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Id.Value)
            .ToList();
}
