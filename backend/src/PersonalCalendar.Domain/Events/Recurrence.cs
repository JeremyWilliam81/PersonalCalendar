using NodaTime;
using NodaTime.TimeZones;
using PersonalCalendar.Domain.Calendar;

namespace PersonalCalendar.Domain.Events;

/// <summary>
/// Generates the occurrences of a series in its own zone (research S1, S3, S7; data-model "Recurrence.Expand").
/// Pure and deterministic: no clock, no storage.
/// </summary>
public static class Recurrence
{
    /// <summary>NodaTime's last full year; generation stops before it rather than overflowing.</summary>
    private const int MaxYear = 9998;

    /// <summary>
    /// The occurrences whose dates (in the series zone) touch <c>[fromDate, toDate]</c>, with changed and deleted
    /// occurrences applied. Changed occurrences moved into the range from outside it are included too.
    /// </summary>
    public static IEnumerable<CalendarItem> Expand(CalendarEvent series, LocalDate fromDate, LocalDate toDate)
    {
        var recurrence = series.Recurrence ?? throw new ArgumentException("The event is not a series.", nameof(series));
        var exceptions = series.Exceptions.ToDictionary(e => e.OriginalDate);
        var lower = fromDate.PlusDays(-SpanDays(series));

        foreach (var date in OriginalDates(recurrence.Rule, series.SeriesFirstDate!.Value, lower, toDate))
        {
            if (!exceptions.Remove(date, out var exception))
            {
                yield return new CalendarItem(series.Id, series.Title, ScheduleOn(series, date), new OccurrenceRef(date, IsException: false));
            }
            else if (!exception.IsDeleted)
            {
                yield return Changed(series, exception);
            }
        }

        // Occurrences moved here from a date outside the range (spec Edge Cases).
        foreach (var exception in exceptions.Values)
        {
            if (exception.IsDeleted || exception.Schedule is null) continue;
            var (start, end) = CoveredDates(exception.Schedule, recurrence.TimeZone);
            if (start <= toDate && end >= fromDate) yield return Changed(series, exception);
        }
    }

    /// <summary>True when the rule produces <paramref name="date"/> (deleted or not).</summary>
    public static bool Produces(CalendarEvent series, LocalDate date) =>
        series.Recurrence is { } recurrence
        && OriginalDates(recurrence.Rule, series.SeriesFirstDate!.Value, date, date).Any();

    /// <summary>1-based position of an occurrence among all the rule's dates, counting deleted ones (RFC 5545 COUNT).</summary>
    public static int PositionOf(RepeatRule rule, LocalDate firstDate, LocalDate date) =>
        Dates(rule, firstDate).TakeWhile(d => d <= date).Count();

    /// <summary>The series' first and last covered dates; the last is null when it never ends (research S7).</summary>
    public static (LocalDate First, LocalDate? Last) Range(RepeatRule rule, LocalDate firstDate, int spanDays)
    {
        if (rule.End is RepeatEnd.Never) return (firstDate, null);

        var last = OriginalDates(rule, firstDate, firstDate, new LocalDate(MaxYear, 12, 31)).Last();
        return (firstDate, last.PlusDays(spanDays));
    }

    /// <summary>
    /// Every date the rule produces from <paramref name="firstDate"/>, in order, ignoring its end. Used as the reference
    /// walk in tests; <see cref="Expand"/> jumps ahead instead of walking from the start.
    /// </summary>
    public static IEnumerable<LocalDate> Dates(RepeatRule rule, LocalDate firstDate) => Dates(rule, firstDate, firstPeriod: 0);

    /// <summary>How many days after its start date an occurrence ends (0 for one-day events).</summary>
    public static int SpanDays(CalendarEvent series) => series.Schedule switch
    {
        AllDaySchedule allDay => Period.Between(allDay.StartDate, allDay.EndDate, PeriodUnits.Days).Days,
        TimedSchedule when series.Recurrence is { StartLocal: { } start, EndLocal: { } end } =>
            Period.Between(start.Date, end.PlusTicks(-1).Date, PeriodUnits.Days).Days,
        _ => 0,
    };

    /// <summary>The rule's dates in <c>[lower, upper]</c>, honouring COUNT and UNTIL.</summary>
    internal static IEnumerable<LocalDate> OriginalDates(RepeatRule rule, LocalDate firstDate, LocalDate lower, LocalDate upper)
    {
        // COUNT is a position among all dates, so it is walked from the start (at most 999 dates).
        var source = rule.End is RepeatEnd.AfterCount
            ? Dates(rule, firstDate)
            : Dates(rule, firstDate, FirstPeriodNear(rule, firstDate, lower));

        var position = 0;
        foreach (var date in source)
        {
            position++;
            if (rule.End is RepeatEnd.AfterCount count && position > count.Count) yield break;
            if (rule.End is RepeatEnd.OnDate until && date > until.Date) yield break;
            if (date > upper) yield break;
            if (date >= lower) yield return date;
        }
    }

    /// <summary>The occurrence's schedule: all-day dates shifted, or local times resolved in the series zone.</summary>
    public static EventSchedule ScheduleOn(CalendarEvent series, LocalDate date)
    {
        var recurrence = series.Recurrence!;
        switch (series.Schedule)
        {
            case AllDaySchedule allDay:
                var shift = Period.Between(allDay.StartDate, date, PeriodUnits.Days);
                return AllDaySchedule.Create(date, allDay.EndDate + shift).Value!;
            default:
                return TimedOn(date, recurrence.StartLocal!.Value, recurrence.EndLocal!.Value, recurrence.TimeZone);
        }
    }

    /// <summary>
    /// The first occurrence's wall-clock times moved to <paramref name="date"/>. A gap shifts forward by its length,
    /// an overlap takes the earlier time (001 R4). If DST squeezes the end onto or before the start, the occurrence
    /// keeps the series' wall-clock length instead.
    /// </summary>
    internal static TimedSchedule TimedOn(LocalDate date, LocalDateTime startLocal, LocalDateTime endLocal, DateTimeZone zone)
    {
        var length = Period.Between(startLocal, endLocal, PeriodUnits.Days | PeriodUnits.AllTimeUnits);
        var localStart = date + startLocal.TimeOfDay;
        var start = zone.ResolveLocal(localStart, Resolvers.LenientResolver).ToInstant();
        var end = zone.ResolveLocal(localStart + length, Resolvers.LenientResolver).ToInstant();
        if (end <= start) end = start + length.ToDuration();
        return TimedSchedule.Create(start, end).Value!;
    }

    private static CalendarItem Changed(CalendarEvent series, OccurrenceException exception) =>
        new(series.Id, exception.Title!, exception.Schedule!, new OccurrenceRef(exception.OriginalDate, IsException: true));

    private static (LocalDate Start, LocalDate End) CoveredDates(EventSchedule schedule, DateTimeZone zone) => schedule switch
    {
        AllDaySchedule allDay => (allDay.StartDate, allDay.EndDate),
        TimedSchedule timed => (timed.Start.InZone(zone).Date, (timed.End - Duration.Epsilon).InZone(zone).Date),
        _ => throw new InvalidOperationException($"Unknown schedule type {schedule.GetType().Name}."),
    };

    private static IEnumerable<LocalDate> Dates(RepeatRule rule, LocalDate firstDate, long firstPeriod)
    {
        for (var period = firstPeriod; ; period++)
        {
            var dates = DatesInPeriod(rule, firstDate, period * rule.Interval);
            if (dates is null) yield break;
            foreach (var date in dates)
            {
                if (date >= firstDate) yield return date;
            }
        }
    }

    /// <summary>The dates in the period that starts <paramref name="units"/> days, weeks, months or years after the first. Null past the supported range.</summary>
    private static IEnumerable<LocalDate>? DatesInPeriod(RepeatRule rule, LocalDate first, long units)
    {
        switch (rule.Frequency)
        {
            case RepeatFrequency.Daily:
                return CanAddDays(first, units) ? [first.PlusDays((int)units)] : null;

            case RepeatFrequency.Weekly:
                var weekStart = first.PlusDays(-DaysSinceSunday(first));
                if (!CanAddDays(weekStart, units * 7 + 6)) return null;
                var week = weekStart.PlusWeeks((int)units);
                return rule.Weekdays.Select(day => week.PlusDays(DaysSinceSunday(day)));

            case RepeatFrequency.Monthly:
                var monthIndex = first.Year * 12L + first.Month - 1 + units;
                var year = (int)(monthIndex / 12);
                if (year > MaxYear) return null;
                var month = (int)(monthIndex % 12) + 1;
                return [MonthlyDate(rule.Monthly, first, year, month)];

            case RepeatFrequency.Yearly:
                var targetYear = first.Year + units;
                if (targetYear > MaxYear) return null;
                // Feb 29 occurs only in leap years (RFC 5545: an invalid date produces no instance).
                return first is { Month: 2, Day: 29 } && !CalendarSystem.Iso.IsLeapYear((int)targetYear)
                    ? []
                    : [new LocalDate((int)targetYear, first.Month, first.Day)];

            default:
                throw new InvalidOperationException($"Unknown frequency {rule.Frequency}.");
        }
    }

    private static LocalDate MonthlyDate(MonthlyPattern? pattern, LocalDate first, int year, int month)
    {
        var daysInMonth = CalendarSystem.Iso.GetDaysInMonth(year, month);
        switch (pattern)
        {
            case MonthlyPattern.WeekdayPosition { Ordinal: -1 }:
                var last = new LocalDate(year, month, daysInMonth);
                return last.PlusDays(-((DaysSinceSunday(last) - DaysSinceSunday(first.DayOfWeek) + 7) % 7));
            case MonthlyPattern.WeekdayPosition position:
                var firstOfMonth = new LocalDate(year, month, 1);
                var offset = (DaysSinceSunday(first.DayOfWeek) - DaysSinceSunday(firstOfMonth) + 7) % 7;
                return firstOfMonth.PlusDays(offset + (position.Ordinal - 1) * 7);
            default:
                // Day 29–31 falls on the last day of shorter months (clarification Q1, BYSETPOS=-1).
                return new LocalDate(year, month, Math.Min(first.Day, daysInMonth));
        }
    }

    /// <summary>The period index to start from so that no date on or after <paramref name="lower"/> is skipped.</summary>
    private static long FirstPeriodNear(RepeatRule rule, LocalDate first, LocalDate lower)
    {
        if (lower <= first) return 0;

        long elapsed = rule.Frequency switch
        {
            RepeatFrequency.Daily => Period.Between(first, lower, PeriodUnits.Days).Days,
            RepeatFrequency.Weekly => Period.Between(first.PlusDays(-DaysSinceSunday(first)), lower, PeriodUnits.Days).Days / 7,
            RepeatFrequency.Monthly => (lower.Year - first.Year) * 12L + lower.Month - first.Month,
            RepeatFrequency.Yearly => lower.Year - first.Year,
            _ => 0,
        };
        return Math.Max(0, elapsed / rule.Interval - 1);
    }

    private static bool CanAddDays(LocalDate date, long days) =>
        days <= Period.Between(date, new LocalDate(MaxYear, 12, 31), PeriodUnits.Days).Days;

    private static int DaysSinceSunday(LocalDate date) => DaysSinceSunday(date.DayOfWeek);

    private static int DaysSinceSunday(IsoDayOfWeek day) => (int)day % 7;
}
