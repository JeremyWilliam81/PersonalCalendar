using NodaTime;
using PersonalCalendar.Domain.Calendar;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Domain.Tests;

/// <summary>Builds <see cref="CalendarItem"/> inputs for the layout tests (research S8).</summary>
internal static class Items
{
    public static CalendarItem OneTime(CalendarEvent calendarEvent) => CalendarItem.FromEvent(calendarEvent);

    public static CalendarItem Of(EventId id, string title, EventSchedule schedule, LocalDate? originalDate = null) =>
        new(id, title, schedule, originalDate is { } date ? new OccurrenceRef(date, IsException: false) : null);
}
