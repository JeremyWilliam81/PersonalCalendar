using NodaTime;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Infrastructure.Persistence;

internal static class EventRowMapper
{
    public static CalendarEvent ToDomain(EventRow row)
    {
        var schedule = ToSchedule(row.IsAllDay, row.StartUtc, row.EndUtc, row.StartDate, row.EndDate);

        SeriesRecurrence? recurrence = null;
        if (row.RecurrenceRule is { } rrule)
        {
            var zone = DateTimeZoneProviders.Tzdb[row.RecurrenceTimeZone!];
            var rule = RepeatRule.ParseRRule(rrule, row.SeriesFirstDate!.Value, zone);
            recurrence = new SeriesRecurrence(rule, zone, row.StartLocal, row.EndLocal);
        }

        return CalendarEvent.Rehydrate(
            new EventId(row.Id), row.Title, row.Location, row.Notes, schedule,
            row.EntryTimeZone, row.CreatedUtc, row.UpdatedUtc, row.Version,
            recurrence, row.Exceptions.Select(ToDomain));
    }

    public static EventRow ToRow(CalendarEvent calendarEvent)
    {
        var row = new EventRow { Id = calendarEvent.Id.Value, CreatedUtc = calendarEvent.CreatedUtc };
        CopyTo(calendarEvent, row);
        return row;
    }

    /// <summary>Copies every field, and makes <see cref="EventRow.Exceptions"/> match the domain's (adds, updates, removes).</summary>
    public static void CopyTo(CalendarEvent calendarEvent, EventRow row)
    {
        row.Title = calendarEvent.Title;
        row.Location = calendarEvent.Location;
        row.Notes = calendarEvent.Notes;
        row.EntryTimeZone = calendarEvent.EntryTimeZone;
        row.UpdatedUtc = calendarEvent.UpdatedUtc;
        row.Version = calendarEvent.Version;

        (row.IsAllDay, row.StartUtc, row.EndUtc, row.StartDate, row.EndDate) = FromSchedule(calendarEvent.Schedule);

        var recurrence = calendarEvent.Recurrence;
        row.RecurrenceRule = recurrence?.Rule.ToRRule(calendarEvent.SeriesFirstDate!.Value, recurrence.TimeZone, row.IsAllDay);
        row.RecurrenceTimeZone = recurrence?.TimeZone.Id;
        row.StartLocal = recurrence?.StartLocal;
        row.EndLocal = recurrence?.EndLocal;
        row.SeriesFirstDate = calendarEvent.SeriesFirstDate;
        row.SeriesLastDate = calendarEvent.SeriesLastDate;

        var wanted = calendarEvent.Exceptions.ToDictionary(e => e.OriginalDate);
        row.Exceptions.RemoveAll(x => !wanted.ContainsKey(x.OriginalDate));
        foreach (var exception in wanted.Values)
        {
            var existing = row.Exceptions.FirstOrDefault(x => x.OriginalDate == exception.OriginalDate);
            if (existing is null)
            {
                existing = new OccurrenceExceptionRow { SeriesId = row.Id, OriginalDate = exception.OriginalDate };
                row.Exceptions.Add(existing);
            }

            CopyTo(exception, existing);
        }
    }

    private static OccurrenceException ToDomain(OccurrenceExceptionRow row) =>
        row.IsDeleted
            ? OccurrenceException.Deleted(row.OriginalDate)
            : OccurrenceException.Changed(
                row.OriginalDate, row.Title!, row.Location, row.Notes,
                ToSchedule(row.IsAllDay!.Value, row.StartUtc, row.EndUtc, row.StartDate, row.EndDate));

    private static void CopyTo(OccurrenceException exception, OccurrenceExceptionRow row)
    {
        row.IsDeleted = exception.IsDeleted;
        row.Title = exception.Title;
        row.Location = exception.Location;
        row.Notes = exception.Notes;
        if (exception.Schedule is null)
        {
            row.IsAllDay = null;
            row.StartUtc = row.EndUtc = null;
            row.StartDate = row.EndDate = null;
        }
        else
        {
            bool isAllDay;
            (isAllDay, row.StartUtc, row.EndUtc, row.StartDate, row.EndDate) = FromSchedule(exception.Schedule);
            row.IsAllDay = isAllDay;
        }
    }

    private static EventSchedule ToSchedule(bool isAllDay, Instant? startUtc, Instant? endUtc, LocalDate? startDate, LocalDate? endDate) =>
        isAllDay
            ? AllDaySchedule.Create(startDate!.Value, endDate!.Value).Value!
            : TimedSchedule.Create(startUtc!.Value, endUtc!.Value).Value!;

    private static (bool IsAllDay, Instant? StartUtc, Instant? EndUtc, LocalDate? StartDate, LocalDate? EndDate) FromSchedule(EventSchedule schedule) =>
        schedule switch
        {
            TimedSchedule timed => (false, timed.Start, timed.End, null, null),
            AllDaySchedule allDay => (true, null, null, allDay.StartDate, allDay.EndDate),
            _ => throw new InvalidOperationException($"Unknown schedule type {schedule.GetType().Name}."),
        };
}
