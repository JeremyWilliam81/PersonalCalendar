using NodaTime;
using PersonalCalendar.Domain.Calendar;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Application.Events;

/// <summary>Converts stored events to what the user sees in the requested zone (conversion at the boundary, Principle II).</summary>
internal static class EventMapping
{
    /// <param name="occurrenceDate">
    /// For a series, the occurrence to show (its original date); the first occurrence when null. The caller checks
    /// that the series produces it and that it is not deleted.
    /// </param>
    public static EventDetails ToDetails(CalendarEvent calendarEvent, DateTimeZone zone, LocalDate? occurrenceDate = null)
    {
        if (calendarEvent.Recurrence is not { } recurrence)
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

        var date = occurrenceDate ?? calendarEvent.SeriesFirstDate!.Value;
        var exception = calendarEvent.Exceptions.FirstOrDefault(e => e.OriginalDate == date && !e.IsDeleted);
        var schedule = exception?.Schedule ?? Recurrence.ScheduleOn(calendarEvent, date);
        var (occurrenceStart, occurrenceEnd, occurrenceStartDate, occurrenceEndDate) = Times(schedule, zone);
        var (seriesStart, _, seriesStartDate, _) = Times(calendarEvent.Schedule, zone);

        return new EventDetails(
            calendarEvent.Id.Value,
            exception?.Title ?? calendarEvent.Title,
            exception is null ? calendarEvent.Location : exception.Location,
            exception is null ? calendarEvent.Notes : exception.Notes,
            schedule is AllDaySchedule,
            zone.Id,
            occurrenceStart,
            occurrenceEnd,
            occurrenceStartDate,
            occurrenceEndDate,
            calendarEvent.Version,
            ToModel(recurrence),
            date,
            seriesStart,
            seriesStartDate,
            IsException: exception is not null,
            ExceptionCount: calendarEvent.Exceptions.Count);
    }

    public static RecurrenceModel ToModel(SeriesRecurrence recurrence)
    {
        var rule = recurrence.Rule;
        return new RecurrenceModel(
            rule.Frequency.ToString().ToLowerInvariant(),
            rule.Interval,
            rule.Weekdays.Select(d => d.ToString().ToLowerInvariant()).ToList(),
            rule.Monthly switch
            {
                MonthlyPattern.WeekdayPosition position => new MonthlyModel("weekdayPosition", position.Ordinal),
                MonthlyPattern.DayOfMonth => new MonthlyModel("dayOfMonth", null),
                _ => null,
            },
            rule.End switch
            {
                RepeatEnd.OnDate onDate => new RepeatEndModel("until", onDate.Date, null),
                RepeatEnd.AfterCount count => new RepeatEndModel("count", null, count.Count),
                _ => new RepeatEndModel("never", null, null),
            },
            recurrence.TimeZone.Id);
    }

    public static EventSummary ToSummary(CalendarItem item, DateTimeZone zone)
    {
        var (start, end, startDate, endDate) = Times(item.Schedule, zone);
        return new EventSummary(
            item.Id.Value,
            item.Title,
            item.Schedule is AllDaySchedule,
            start,
            end,
            startDate,
            endDate,
            IsRecurring: item.Occurrence is not null,
            OccurrenceDate: item.Occurrence?.OriginalDate);
    }

    private static (OffsetDateTime? Start, OffsetDateTime? End, LocalDate? StartDate, LocalDate? EndDate) Times(
        EventSchedule schedule, DateTimeZone zone) => schedule switch
    {
        TimedSchedule timed => (timed.Start.InZone(zone).ToOffsetDateTime(), timed.End.InZone(zone).ToOffsetDateTime(), null, null),
        AllDaySchedule allDay => (null, null, allDay.StartDate, allDay.EndDate),
        _ => throw new InvalidOperationException($"Unknown schedule type {schedule.GetType().Name}."),
    };
}
