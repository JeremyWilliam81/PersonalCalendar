using NodaTime;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Application.Events;

/// <summary>Converts stored events to what the user sees in the requested zone (conversion at the boundary, Principle II).</summary>
internal static class EventMapping
{
    public static EventDetails ToDetails(CalendarEvent calendarEvent, DateTimeZone zone)
    {
        var (start, end, startDate, endDate) = Times(calendarEvent.Schedule, zone);
        return new EventDetails(
            calendarEvent.Id.Value,
            calendarEvent.Title,
            calendarEvent.Location,
            calendarEvent.Notes,
            calendarEvent.Schedule is AllDaySchedule,
            zone.Id,
            start,
            end,
            startDate,
            endDate,
            calendarEvent.Version);
    }

    public static EventSummary ToSummary(CalendarEvent calendarEvent, DateTimeZone zone)
    {
        var (start, end, startDate, endDate) = Times(calendarEvent.Schedule, zone);
        return new EventSummary(
            calendarEvent.Id.Value, calendarEvent.Title, calendarEvent.Schedule is AllDaySchedule, start, end, startDate, endDate);
    }

    private static (OffsetDateTime? Start, OffsetDateTime? End, LocalDate? StartDate, LocalDate? EndDate) Times(
        EventSchedule schedule, DateTimeZone zone) => schedule switch
    {
        TimedSchedule timed => (timed.Start.InZone(zone).ToOffsetDateTime(), timed.End.InZone(zone).ToOffsetDateTime(), null, null),
        AllDaySchedule allDay => (null, null, allDay.StartDate, allDay.EndDate),
        _ => throw new InvalidOperationException($"Unknown schedule type {schedule.GetType().Name}."),
    };
}
