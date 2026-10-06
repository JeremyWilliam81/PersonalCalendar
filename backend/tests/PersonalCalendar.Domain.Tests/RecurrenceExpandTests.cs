using System.Diagnostics;
using NodaTime;
using NodaTime.Testing;
using PersonalCalendar.Domain.Calendar;
using PersonalCalendar.Domain.Events;
using static PersonalCalendar.Domain.Tests.TestZones;

namespace PersonalCalendar.Domain.Tests;

/// <summary>Occurrence generation in the series zone (research S7, S13; Constitution I).</summary>
public class RecurrenceExpandTests
{
    private static readonly IClock Clock = new FakeClock(Instant.FromUtc(2026, 9, 29, 15, 0));
    private static readonly IsoDayOfWeek[] NoDays = [];

    private static RepeatRule Rule(
        RepeatFrequency frequency,
        string start,
        int interval = 1,
        IsoDayOfWeek[]? weekdays = null,
        MonthlyPattern? monthly = null,
        RepeatEnd? end = null)
    {
        monthly ??= frequency == RepeatFrequency.Monthly ? MonthlyPattern.DayOfMonth.Instance : null;
        var rule = RepeatRule.Create(frequency, interval, weekdays ?? NoDays, monthly, end ?? RepeatEnd.Never.Instance, D(start));
        Assert.True(rule.IsValid);
        return rule.Value!;
    }

    private static CalendarEvent Timed(string start, string end, RepeatRule rule, DateTimeZone? zone = null)
    {
        zone ??= Chicago;
        var schedule = TimedSchedule.Create(LDT(start).InZoneLeniently(zone).ToInstant(), LDT(end).InZoneLeniently(zone).ToInstant()).Value!;
        var created = CalendarEvent.Create("Series", null, null, schedule, zone, Clock, rule);
        Assert.True(created.IsValid);
        return created.Value!;
    }

    private static CalendarEvent AllDay(string start, string end, RepeatRule rule)
    {
        var created = CalendarEvent.Create("Series", null, null, AllDaySchedule.Create(D(start), D(end)).Value!, Chicago, Clock, rule);
        Assert.True(created.IsValid);
        return created.Value!;
    }

    private static List<LocalDate> Dates(CalendarEvent series, string from, string to) =>
        Recurrence.Expand(series, D(from), D(to)).Select(i => i.Occurrence!.OriginalDate).ToList();

    private static List<LocalDate> Ds(params string[] dates) => dates.Select(TestZones.D).ToList();

    private static (LocalDateTime Start, LocalDateTime End) LocalTimes(CalendarItem item, DateTimeZone zone)
    {
        var timed = Assert.IsType<TimedSchedule>(item.Schedule);
        return (timed.Start.InZone(zone).LocalDateTime, timed.End.InZone(zone).LocalDateTime);
    }

    private static CalendarItem On(CalendarEvent series, string date) =>
        Recurrence.Expand(series, TestZones.D(date), TestZones.D(date)).Single(i => i.Occurrence!.OriginalDate == TestZones.D(date));

    // ----- Daily and weekly -----

    [Fact]
    public void Daily_WithCount_StopsAfterThatMany()
    {
        var series = Timed("2026-10-06T09:00", "2026-10-06T10:00", Rule(RepeatFrequency.Daily, "2026-10-06", end: new RepeatEnd.AfterCount(5)));

        Assert.Equal(Ds("2026-10-06", "2026-10-07", "2026-10-08", "2026-10-09", "2026-10-10"), Dates(series, "2026-10-01", "2026-10-31"));
    }

    [Fact]
    public void Weekly_UntilDate_IsInclusive()
    {
        var series = Timed("2026-10-06T09:00", "2026-10-06T10:00",
            Rule(RepeatFrequency.Weekly, "2026-10-06", weekdays: [IsoDayOfWeek.Tuesday], end: new RepeatEnd.OnDate(TestZones.D("2026-10-27"))));

        Assert.Equal(Ds("2026-10-06", "2026-10-13", "2026-10-20", "2026-10-27"), Dates(series, "2026-10-01", "2026-12-31"));
    }

    [Fact]
    public void Weekly_OnSeveralDays_EveryWeek()
    {
        var series = Timed("2026-10-12T07:00", "2026-10-12T08:00",
            Rule(RepeatFrequency.Weekly, "2026-10-12", weekdays: [IsoDayOfWeek.Monday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Friday]));

        Assert.Equal(
            Ds("2026-10-12", "2026-10-14", "2026-10-16", "2026-10-19", "2026-10-21", "2026-10-23", "2026-10-26", "2026-10-28", "2026-10-30"),
            Dates(series, "2026-10-01", "2026-10-31"));
        Assert.All(Recurrence.Expand(series, TestZones.D("2026-10-12"), TestZones.D("2026-10-31")), item =>
            Assert.Equal(new LocalTime(7, 0), LocalTimes(item, Chicago).Start.TimeOfDay));
    }

    [Fact]
    public void Weekly_EveryTwoWeeks_CountsSundayToSaturdayWeeks()
    {
        var series = Timed("2026-10-06T09:00", "2026-10-06T10:00",
            Rule(RepeatFrequency.Weekly, "2026-10-06", 2, [IsoDayOfWeek.Tuesday, IsoDayOfWeek.Thursday], end: new RepeatEnd.AfterCount(6)));

        Assert.Equal(
            Ds("2026-10-06", "2026-10-08", "2026-10-20", "2026-10-22", "2026-11-03", "2026-11-05"),
            Dates(series, "2026-10-01", "2026-12-31"));
    }

    [Fact]
    public void Weekly_StartNotOnAChosenWeekday_BeginsOnTheFirstChosenDay()
    {
        var series = Timed("2026-10-06T09:00", "2026-10-06T10:00",
            Rule(RepeatFrequency.Weekly, "2026-10-06", weekdays: [IsoDayOfWeek.Thursday]));

        Assert.Equal(TestZones.D("2026-10-08"), series.Recurrence!.Rule.FirstOccurrence(TestZones.D("2026-10-06")));
        Assert.Equal(Ds("2026-10-08", "2026-10-15"), Dates(series, "2026-10-01", "2026-10-15"));
        Assert.Equal(new LocalDateTime(2026, 10, 8, 9, 0), LocalTimes(On(series, "2026-10-08"), Chicago).Start);
    }

    // ----- Monthly -----

    [Fact]
    public void Monthly_OnDay14_ForAYear()
    {
        var series = AllDay("2026-10-14", "2026-10-14", Rule(RepeatFrequency.Monthly, "2026-10-14"));

        var dates = Dates(series, "2026-10-01", "2027-09-30");

        Assert.Equal(12, dates.Count);
        Assert.All(dates, d => Assert.Equal(14, d.Day));
    }

    [Fact]
    public void Monthly_EveryThreeMonths_OnDay15()
    {
        var series = AllDay("2027-01-15", "2027-01-15", Rule(RepeatFrequency.Monthly, "2027-01-15", 3));

        Assert.Equal(Ds("2027-01-15", "2027-04-15", "2027-07-15", "2027-10-15"), Dates(series, "2027-01-01", "2027-12-31"));
    }

    [Fact]
    public void Monthly_OnDay31_FallsOnTheLastDayOfShorterMonths()
    {
        var series = AllDay("2027-01-31", "2027-01-31", Rule(RepeatFrequency.Monthly, "2027-01-31"));

        Assert.Equal(Ds("2027-01-31", "2027-02-28", "2027-03-31", "2027-04-30"), Dates(series, "2027-01-01", "2027-04-30"));
    }

    [Theory]
    [InlineData("2026-11-30")]
    [InlineData("2026-11-29")]
    public void Monthly_OnDay29Or30_UsesFebruary28Or29(string start)
    {
        var series = AllDay(start, start, Rule(RepeatFrequency.Monthly, start));

        Assert.Equal(Ds("2027-02-28"), Dates(series, "2027-02-01", "2027-02-28"));
        Assert.Equal(Ds("2028-02-29"), Dates(series, "2028-02-01", "2028-02-29"));
    }

    [Fact]
    public void Monthly_OnTheSecondWednesday()
    {
        var series = AllDay("2026-10-14", "2026-10-14", Rule(RepeatFrequency.Monthly, "2026-10-14", monthly: new MonthlyPattern.WeekdayPosition(2)));

        Assert.Equal(Ds("2026-10-14", "2026-11-11", "2026-12-09", "2027-01-13"), Dates(series, "2026-10-01", "2027-01-31"));
    }

    [Fact]
    public void Monthly_OnTheLastFriday()
    {
        var series = AllDay("2026-10-30", "2026-10-30", Rule(RepeatFrequency.Monthly, "2026-10-30", monthly: new MonthlyPattern.WeekdayPosition(-1)));

        Assert.Equal(Ds("2026-10-30", "2026-11-27", "2026-12-25", "2027-01-29"), Dates(series, "2026-10-01", "2027-01-31"));
    }

    // ----- Yearly -----

    [Fact]
    public void Yearly_OnMarch14()
    {
        var series = AllDay("2027-03-14", "2027-03-14", Rule(RepeatFrequency.Yearly, "2027-03-14"));

        Assert.Equal(Ds("2027-03-14", "2028-03-14", "2029-03-14"), Dates(series, "2027-01-01", "2029-12-31"));
    }

    [Fact]
    public void Yearly_OnFebruary29_OccursOnlyInLeapYears()
    {
        var series = AllDay("2028-02-29", "2028-02-29", Rule(RepeatFrequency.Yearly, "2028-02-29"));

        Assert.Equal(Ds("2028-02-29", "2032-02-29", "2036-02-29"), Dates(series, "2028-01-01", "2036-12-31"));
    }

    // ----- Long ranges and counts -----

    [Theory]
    [InlineData(RepeatFrequency.Daily, 1)]
    [InlineData(RepeatFrequency.Weekly, 3)]
    [InlineData(RepeatFrequency.Monthly, 5)]
    public void FastForward_MatchesAWalkFromTheStart(RepeatFrequency frequency, int interval)
    {
        IsoDayOfWeek[] days = [IsoDayOfWeek.Monday, IsoDayOfWeek.Thursday];
        var rule = Rule(frequency, "1900-01-01", interval, days);
        var series = AllDay("1900-01-01", "1900-01-01", rule);
        var from = TestZones.D("2199-08-01");
        var to = TestZones.D("2199-12-31");

        var watch = Stopwatch.StartNew();
        var dates = Dates(series, "2199-08-01", "2199-12-31");
        watch.Stop();

        var reference = Recurrence.Dates(rule, TestZones.D("1900-01-01")).SkipWhile(d => d < from).TakeWhile(d => d <= to).ToList();
        Assert.Equal(reference, dates);
        Assert.NotEmpty(dates);
        if (frequency == RepeatFrequency.Daily) Assert.Equal(153, dates.Count);
        Assert.True(watch.ElapsedMilliseconds < 50, $"took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Count_WithARangeAfterTheStart_ReturnsOnlyTheLaterOccurrences()
    {
        var series = AllDay("2026-10-06", "2026-10-06",
            Rule(RepeatFrequency.Weekly, "2026-10-06", weekdays: [IsoDayOfWeek.Tuesday], end: new RepeatEnd.AfterCount(6)));

        Assert.Equal(Ds("2026-10-27", "2026-11-03", "2026-11-10"), Dates(series, "2026-10-25", "2026-12-31"));
    }

    [Fact]
    public void Range_AcrossAYearBoundary()
    {
        var series = AllDay("2026-12-30", "2026-12-30", Rule(RepeatFrequency.Daily, "2026-12-30"));

        Assert.Equal(Ds("2026-12-30", "2026-12-31", "2027-01-01", "2027-01-02"), Dates(series, "2026-12-30", "2027-01-02"));
    }

    [Fact]
    public void NoEnd_ReachesTheEndOfTheSupportedRange()
    {
        var series = AllDay("2026-10-14", "2026-10-14", Rule(RepeatFrequency.Yearly, "2026-10-14"));

        Assert.Equal(Ds("2199-10-14"), Dates(series, "2199-01-01", "2199-12-31"));
    }

    // ----- DST (series zone America/Chicago) -----

    [Fact]
    public void Weekly_KeepsItsLocalTime_AcrossSpringForward()
    {
        var series = Timed("2027-03-07T09:00", "2027-03-07T10:00", Rule(RepeatFrequency.Weekly, "2027-03-07", weekdays: [IsoDayOfWeek.Sunday]));

        var before = On(series, "2027-03-07");
        var after = On(series, "2027-03-14");

        Assert.Equal(new LocalTime(9, 0), LocalTimes(before, Chicago).Start.TimeOfDay);
        Assert.Equal(new LocalTime(9, 0), LocalTimes(after, Chicago).Start.TimeOfDay);
        Assert.Equal(Duration.FromHours(167), ((TimedSchedule)after.Schedule).Start - ((TimedSchedule)before.Schedule).Start);
    }

    [Fact]
    public void Weekly_KeepsItsLocalTime_AcrossFallBack()
    {
        var series = Timed("2026-10-25T09:00", "2026-10-25T10:00", Rule(RepeatFrequency.Weekly, "2026-10-25", weekdays: [IsoDayOfWeek.Sunday]));

        var before = On(series, "2026-10-25");
        var after = On(series, "2026-11-01");

        Assert.Equal(new LocalTime(9, 0), LocalTimes(after, Chicago).Start.TimeOfDay);
        Assert.Equal(Duration.FromHours(169), ((TimedSchedule)after.Schedule).Start - ((TimedSchedule)before.Schedule).Start);
    }

    [Fact]
    public void Occurrence_InASpringForwardGap_ShiftsForwardByTheGap()
    {
        var series = Timed("2027-03-13T02:30", "2027-03-13T03:00", Rule(RepeatFrequency.Daily, "2027-03-13"));

        Assert.Equal(
            (new LocalDateTime(2027, 3, 14, 3, 30), new LocalDateTime(2027, 3, 14, 4, 0)),
            LocalTimes(On(series, "2027-03-14"), Chicago));
    }

    [Fact]
    public void Occurrence_InAFallBackOverlap_UsesTheEarlierTime()
    {
        var series = Timed("2026-10-31T01:30", "2026-10-31T02:30", Rule(RepeatFrequency.Daily, "2026-10-31"));

        var start = ((TimedSchedule)On(series, "2026-11-01").Schedule).Start;

        Assert.Equal(Offset.FromHours(-5), start.InZone(Chicago).Offset);
        Assert.Equal(new LocalDateTime(2026, 11, 1, 1, 30), start.InZone(Chicago).LocalDateTime);
    }

    [Fact]
    public void Overnight_KeepsItsWallClockSpan_OnTheFallBackNight()
    {
        var series = Timed("2026-10-30T22:00", "2026-10-31T02:00", Rule(RepeatFrequency.Daily, "2026-10-30"));

        var night = On(series, "2026-10-31");
        var timed = (TimedSchedule)night.Schedule;

        Assert.Equal((new LocalDateTime(2026, 10, 31, 22, 0), new LocalDateTime(2026, 11, 1, 2, 0)), LocalTimes(night, Chicago));
        Assert.Equal(Duration.FromHours(5), timed.End - timed.Start);
    }

    [Fact]
    public void LordHowe_HalfHourGap_ShiftsByThirtyMinutes()
    {
        // Australia/Lord_Howe moves from 02:00 to 02:30 on 2026-10-04.
        var series = Timed("2026-10-03T02:15", "2026-10-03T03:15", Rule(RepeatFrequency.Daily, "2026-10-03"), LordHowe);

        Assert.Equal(new LocalDateTime(2026, 10, 4, 2, 45), LocalTimes(On(series, "2026-10-04"), LordHowe).Start);
    }

    [Theory]
    [InlineData("Asia/Kolkata", "2026-10-21T17:30")]
    [InlineData("Australia/Adelaide", "2026-10-21T22:30")]
    public void ChicagoSeries_ShownInAnotherZone_KeepsItsMoment(string zoneId, string expectedLocal)
    {
        var series = Timed("2026-10-12T07:00", "2026-10-12T08:00", Rule(RepeatFrequency.Weekly, "2026-10-12", weekdays: [IsoDayOfWeek.Wednesday, IsoDayOfWeek.Monday]));

        var start = ((TimedSchedule)On(series, "2026-10-21").Schedule).Start;

        Assert.Equal(LDT(expectedLocal), start.InZone(DateTimeZoneProviders.Tzdb[zoneId]).LocalDateTime);
    }

    // ----- Multi-day -----

    [Fact]
    public void MultiDayAllDay_IsFound_FromItsMiddleDay()
    {
        var series = AllDay("2026-10-10", "2026-10-12", Rule(RepeatFrequency.Monthly, "2026-10-10"));

        var november = Recurrence.Expand(series, TestZones.D("2026-11-11"), TestZones.D("2026-11-30")).Single();

        Assert.Equal(TestZones.D("2026-11-10"), november.Occurrence!.OriginalDate);
        var schedule = Assert.IsType<AllDaySchedule>(november.Schedule);
        Assert.Equal((TestZones.D("2026-11-10"), TestZones.D("2026-11-12")), (schedule.StartDate, schedule.EndDate));
    }

    [Fact]
    public void SeriesRange_IsComputedFromTheRule()
    {
        var counted = AllDay("2026-10-06", "2026-10-07",
            Rule(RepeatFrequency.Weekly, "2026-10-06", weekdays: [IsoDayOfWeek.Tuesday], end: new RepeatEnd.AfterCount(3)));
        var open = AllDay("2026-10-06", "2026-10-06", Rule(RepeatFrequency.Daily, "2026-10-06"));

        Assert.Equal(TestZones.D("2026-10-06"), counted.SeriesFirstDate);
        Assert.Equal(TestZones.D("2026-10-21"), counted.SeriesLastDate); // last occurrence Oct 20, ending Oct 21
        Assert.Null(open.SeriesLastDate);
    }
}
