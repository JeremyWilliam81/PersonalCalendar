using NodaTime;
using PersonalCalendar.Domain.Calendar;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Application.Events;

/// <summary>Turns stored events into what the layouts place: one-time events and expanded occurrences (research S8).</summary>
internal static class CalendarItems
{
    /// <summary>
    /// The layouts clip to the exact range themselves. Series are expanded two extra days on each side, because
    /// their dates are in the series zone and offsets span −12 to +14 hours.
    /// </summary>
    public static IReadOnlyList<CalendarItem> Build(IEnumerable<CalendarEvent> events, LocalDate fromDate, LocalDate toDate) =>
        events
            .SelectMany(e => e.Recurrence is null
                ? [CalendarItem.FromEvent(e)]
                : Recurrence.Expand(e, fromDate.PlusDays(-2), toDate.PlusDays(2)))
            .ToList();
}
