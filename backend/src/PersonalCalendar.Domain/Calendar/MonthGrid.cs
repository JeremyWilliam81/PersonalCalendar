using NodaTime;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Domain.Calendar;

/// <summary>Builds the month view: grid range, placing events on days, and ordering them (research R6).</summary>
public static class MonthGrid
{
    /// <summary>The Sunday on or before the 1st through the Saturday on or after the last day of the month.</summary>
    public static (LocalDate First, LocalDate Last) GridRange(int year, int month)
    {
        var firstOfMonth = new LocalDate(year, month, 1);
        var lastOfMonth = firstOfMonth.PlusMonths(1).PlusDays(-1);
        var first = firstOfMonth.PlusDays(-DaysSinceSunday(firstOfMonth));
        var last = lastOfMonth.PlusDays(6 - DaysSinceSunday(lastOfMonth));
        return (first, last);
    }

    public static MonthGridResult Build(
        int year, int month, DateTimeZone zone, LocalDate today, IEnumerable<CalendarEvent> events)
    {
        var (first, last) = GridRange(year, month);
        var eventsByDay = new Dictionary<LocalDate, List<CalendarEvent>>();

        foreach (var calendarEvent in events)
        {
            if (!TryGetCoveredDates(calendarEvent.Schedule, zone, out var startDate, out var endDate)) continue;

            var from = startDate > first ? startDate : first;
            var to = endDate < last ? endDate : last;
            for (var day = from; day <= to; day = day.PlusDays(1))
            {
                if (!eventsByDay.TryGetValue(day, out var list)) eventsByDay[day] = list = [];
                list.Add(calendarEvent);
            }
        }

        var weeks = new List<IReadOnlyList<DayCell>>();
        for (var weekStart = first; weekStart <= last; weekStart = weekStart.PlusWeeks(1))
        {
            var week = new List<DayCell>(7);
            for (var offset = 0; offset < 7; offset++)
            {
                var date = weekStart.PlusDays(offset);
                var dayEvents = eventsByDay.TryGetValue(date, out var list) ? EventOrdering.Order(list) : [];
                week.Add(new DayCell(date, date.Year == year && date.Month == month, date == today, dayEvents));
            }

            weeks.Add(week);
        }

        return new MonthGridResult(year, month, weeks);
    }

    private static bool TryGetCoveredDates(EventSchedule schedule, DateTimeZone zone, out LocalDate start, out LocalDate end)
    {
        switch (schedule)
        {
            case TimedSchedule timed:
                // An event ending exactly at midnight does not appear on the next day.
                start = timed.Start.InZone(zone).Date;
                end = (timed.End - Duration.Epsilon).InZone(zone).Date;
                return true;
            case AllDaySchedule allDay:
                // Plain dates: no zone or DST rule can move them (FR-016).
                start = allDay.StartDate;
                end = allDay.EndDate;
                return true;
            default:
                start = end = default;
                return false;
        }
    }

    private static int DaysSinceSunday(LocalDate date) => (int)date.DayOfWeek % 7;
}
