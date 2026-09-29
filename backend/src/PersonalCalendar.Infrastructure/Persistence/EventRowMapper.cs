using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Infrastructure.Persistence;

internal static class EventRowMapper
{
    public static CalendarEvent ToDomain(EventRow row)
    {
        EventSchedule schedule = row.IsAllDay
            ? AllDaySchedule.Create(row.StartDate!.Value, row.EndDate!.Value).Value!
            : TimedSchedule.Create(row.StartUtc!.Value, row.EndUtc!.Value).Value!;

        return CalendarEvent.Rehydrate(
            new EventId(row.Id), row.Title, row.Location, row.Notes, schedule,
            row.EntryTimeZone, row.CreatedUtc, row.UpdatedUtc, row.Version);
    }

    public static EventRow ToRow(CalendarEvent calendarEvent)
    {
        var row = new EventRow { Id = calendarEvent.Id.Value, CreatedUtc = calendarEvent.CreatedUtc };
        CopyTo(calendarEvent, row);
        return row;
    }

    public static void CopyTo(CalendarEvent calendarEvent, EventRow row)
    {
        row.Title = calendarEvent.Title;
        row.Location = calendarEvent.Location;
        row.Notes = calendarEvent.Notes;
        row.EntryTimeZone = calendarEvent.EntryTimeZone;
        row.UpdatedUtc = calendarEvent.UpdatedUtc;
        row.Version = calendarEvent.Version;

        switch (calendarEvent.Schedule)
        {
            case TimedSchedule timed:
                row.IsAllDay = false;
                row.StartUtc = timed.Start;
                row.EndUtc = timed.End;
                row.StartDate = null;
                row.EndDate = null;
                break;
            case AllDaySchedule allDay:
                row.IsAllDay = true;
                row.StartUtc = null;
                row.EndUtc = null;
                row.StartDate = allDay.StartDate;
                row.EndDate = allDay.EndDate;
                break;
            default:
                throw new InvalidOperationException($"Unknown schedule type {calendarEvent.Schedule.GetType().Name}.");
        }
    }
}
