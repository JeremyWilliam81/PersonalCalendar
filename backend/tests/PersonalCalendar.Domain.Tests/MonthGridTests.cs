using NodaTime;
using NodaTime.Testing;
using PersonalCalendar.Domain.Calendar;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Domain.Tests;

public class MonthGridTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
    private static readonly DateTimeZone Kolkata = DateTimeZoneProviders.Tzdb["Asia/Kolkata"];
    private static readonly IClock Clock = new FakeClock(Instant.FromUtc(2026, 9, 29, 15, 0));
    private static readonly LocalDate Today = new(2026, 9, 29);

    private static CalendarItem Timed(string title, Instant start, Instant end) =>
        Items.OneTime(CalendarEvent.Create(title, null, null, TimedSchedule.Create(start, end).Value!, Chicago, Clock).Value!);

    private static CalendarItem TimedLocal(string title, DateTimeZone zone, LocalDateTime start, LocalDateTime end) =>
        Timed(title, start.InZoneLeniently(zone).ToInstant(), end.InZoneLeniently(zone).ToInstant());

    private static DayCell Day(MonthGridResult grid, LocalDate date) =>
        grid.Weeks.SelectMany(w => w).Single(d => d.Date == date);

    private static IEnumerable<LocalDate> DaysWith(MonthGridResult grid, string title) =>
        grid.Weeks.SelectMany(w => w).Where(d => d.Events.Any(e => e.Title == title)).Select(d => d.Date);

    [Theory]
    [InlineData(2026, 2, 4, "2026-02-01", "2026-02-28")]
    [InlineData(2026, 10, 5, "2026-09-27", "2026-10-31")]
    [InlineData(2026, 8, 6, "2026-07-26", "2026-09-05")]
    public void Grid_CoversWholeMonth_InSundayToSaturdayWeeks(int year, int month, int weeks, string first, string last)
    {
        var grid = MonthGrid.Build(year, month, Chicago, Today, []);

        Assert.Equal(weeks, grid.Weeks.Count);
        Assert.All(grid.Weeks, w => Assert.Equal(7, w.Count));
        Assert.All(grid.Weeks, w => Assert.Equal(IsoDayOfWeek.Sunday, w[0].Date.DayOfWeek));
        Assert.Equal(first, grid.Weeks[0][0].Date.ToString("uuuu-MM-dd", null));
        Assert.Equal(last, grid.Weeks[^1][6].Date.ToString("uuuu-MM-dd", null));
    }

    [Fact]
    public void Grid_FlagsDaysOutsideMonth_AndToday()
    {
        var grid = MonthGrid.Build(2026, 10, Chicago, new LocalDate(2026, 10, 14), []);

        Assert.False(Day(grid, new LocalDate(2026, 9, 30)).InMonth);
        Assert.True(Day(grid, new LocalDate(2026, 10, 1)).InMonth);
        Assert.True(Day(grid, new LocalDate(2026, 10, 14)).IsToday);
        Assert.Single(grid.Weeks.SelectMany(w => w), d => d.IsToday);
    }

    [Fact]
    public void TimedEvent_SpanningMidnight_AppearsOnBothDays()
    {
        var ev = TimedLocal("Late", Chicago, new(2026, 10, 14, 22, 0), new(2026, 10, 15, 1, 0));

        var grid = MonthGrid.Build(2026, 10, Chicago, Today, [ev]);

        Assert.Equal([new LocalDate(2026, 10, 14), new LocalDate(2026, 10, 15)], DaysWith(grid, "Late"));
    }

    [Fact]
    public void TimedEvent_EndingExactlyAtMidnight_AppearsOnStartDayOnly()
    {
        var ev = TimedLocal("Evening", Chicago, new(2026, 10, 14, 20, 0), new(2026, 10, 15, 0, 0));

        var grid = MonthGrid.Build(2026, 10, Chicago, Today, [ev]);

        Assert.Equal([new LocalDate(2026, 10, 14)], DaysWith(grid, "Evening"));
    }

    [Fact]
    public void TimedEvent_SpanningMonths_AppearsInEachMonthsGrid()
    {
        var ev = TimedLocal("Trip", Chicago, new(2026, 10, 30, 10, 0), new(2026, 11, 2, 10, 0));

        var october = MonthGrid.Build(2026, 10, Chicago, Today, [ev]);
        var november = MonthGrid.Build(2026, 11, Chicago, Today, [ev]);

        Assert.Equal([new LocalDate(2026, 10, 30), new LocalDate(2026, 10, 31)], DaysWith(october, "Trip"));
        Assert.Equal([new LocalDate(2026, 11, 1), new LocalDate(2026, 11, 2)], DaysWith(november, "Trip"));
    }

    [Fact]
    public void TimedEvent_OnLeadingOrTrailingDay_AppearsInNeighbouringMonthGrid()
    {
        var ev = TimedLocal("First", Chicago, new(2026, 10, 1, 9, 0), new(2026, 10, 1, 10, 0));

        var september = MonthGrid.Build(2026, 9, Chicago, Today, [ev]);

        Assert.Equal(new LocalDate(2026, 10, 3), september.Weeks[^1][6].Date);
        var cell = Day(september, new LocalDate(2026, 10, 1));
        Assert.False(cell.InMonth);
        Assert.Single(cell.Events);
    }

    [Fact]
    public void TimedEvent_AcrossYearBoundary_AppearsOnBothDays()
    {
        var ev = TimedLocal("NYE", Chicago, new(2026, 12, 31, 23, 0), new(2027, 1, 1, 1, 0));

        var december = MonthGrid.Build(2026, 12, Chicago, Today, [ev]);

        Assert.Equal([new LocalDate(2026, 12, 31), new LocalDate(2027, 1, 1)], DaysWith(december, "NYE"));
    }

    [Fact]
    public void TimedEvents_AreOrderedByStartThenTitle()
    {
        var nine = TimedLocal("B at nine", Chicago, new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0));
        var nineToo = TimedLocal("A at nine", Chicago, new(2026, 10, 14, 9, 0), new(2026, 10, 14, 9, 30));
        var eight = TimedLocal("Z at eight", Chicago, new(2026, 10, 14, 8, 0), new(2026, 10, 14, 8, 30));

        var grid = MonthGrid.Build(2026, 10, Chicago, Today, [nine, nineToo, eight]);

        Assert.Equal(["Z at eight", "A at nine", "B at nine"], Day(grid, new LocalDate(2026, 10, 14)).Events.Select(e => e.Title));
    }

    [Fact]
    public void TimedEvent_ContinuingFromPreviousDay_IsOrderedAsStartingAtMidnight()
    {
        // On the 15th, "Late" counts as starting at 00:00, so it ties with "A at midnight" and the title decides.
        var late = TimedLocal("Late", Chicago, new(2026, 10, 14, 18, 0), new(2026, 10, 15, 1, 0));
        var midnight = TimedLocal("A at midnight", Chicago, new(2026, 10, 15, 0, 0), new(2026, 10, 15, 0, 30));
        var morning = TimedLocal("B in the morning", Chicago, new(2026, 10, 15, 8, 0), new(2026, 10, 15, 9, 0));
        var evening = TimedLocal("C in the evening", Chicago, new(2026, 10, 14, 17, 0), new(2026, 10, 14, 19, 0));

        var grid = MonthGrid.Build(2026, 10, Chicago, Today, [morning, late, midnight, evening]);

        Assert.Equal(["C in the evening", "Late"], Day(grid, new LocalDate(2026, 10, 14)).Events.Select(e => e.Title));
        Assert.Equal(["A at midnight", "Late", "B in the morning"], Day(grid, new LocalDate(2026, 10, 15)).Events.Select(e => e.Title));
    }

    [Fact]
    public void TimedEvent_IsPlacedByLocalDateInRequestedZone()
    {
        var ev = Timed("Call", Instant.FromUtc(2026, 10, 14, 20, 0), Instant.FromUtc(2026, 10, 14, 21, 0));

        var inKolkata = MonthGrid.Build(2026, 10, Kolkata, Today, [ev]);
        var inChicago = MonthGrid.Build(2026, 10, Chicago, Today, [ev]);

        Assert.Equal([new LocalDate(2026, 10, 15)], DaysWith(inKolkata, "Call"));
        Assert.Equal([new LocalDate(2026, 10, 14)], DaysWith(inChicago, "Call"));
    }

    private static CalendarItem AllDay(string title, LocalDate start, LocalDate end) =>
        Items.OneTime(CalendarEvent.Create(title, null, null, AllDaySchedule.Create(start, end).Value!, Chicago, Clock).Value!);

    [Fact]
    public void AllDayEvent_AppearsOnEveryDateInItsInclusiveRange()
    {
        var vacation = AllDay("Vacation", new LocalDate(2026, 10, 20), new LocalDate(2026, 10, 23));
        var birthday = AllDay("Birthday", new LocalDate(2026, 10, 14), new LocalDate(2026, 10, 14));

        var grid = MonthGrid.Build(2026, 10, Chicago, Today, [vacation, birthday]);

        Assert.Equal(
            [new LocalDate(2026, 10, 20), new LocalDate(2026, 10, 21), new LocalDate(2026, 10, 22), new LocalDate(2026, 10, 23)],
            DaysWith(grid, "Vacation"));
        Assert.Equal([new LocalDate(2026, 10, 14)], DaysWith(grid, "Birthday"));
    }

    [Fact]
    public void AllDayEvents_ComeBeforeTimedEvents_OrderedByStartDateThenTitle()
    {
        var early = TimedLocal("Early meeting", Chicago, new(2026, 10, 21, 7, 0), new(2026, 10, 21, 8, 0));
        var conference = AllDay("Conference", new LocalDate(2026, 10, 21), new LocalDate(2026, 10, 21));
        var vacation = AllDay("Vacation", new LocalDate(2026, 10, 20), new LocalDate(2026, 10, 23));
        var anniversary = AllDay("Anniversary", new LocalDate(2026, 10, 21), new LocalDate(2026, 10, 21));

        var grid = MonthGrid.Build(2026, 10, Chicago, Today, [early, conference, vacation, anniversary]);

        Assert.Equal(
            ["Vacation", "Anniversary", "Conference", "Early meeting"],
            Day(grid, new LocalDate(2026, 10, 21)).Events.Select(e => e.Title));
    }

    [Theory]
    [InlineData("America/Chicago")]
    [InlineData("Asia/Kolkata")]
    [InlineData("Australia/Adelaide")]
    [InlineData("Pacific/Kiritimati")]
    public void AllDayEvent_KeepsItsDate_InEveryZone(string zoneId)
    {
        var zone = DateTimeZoneProviders.Tzdb[zoneId];
        var birthday = AllDay("Birthday", new LocalDate(2026, 10, 20), new LocalDate(2026, 10, 20));

        var grid = MonthGrid.Build(2026, 10, zone, Today, [birthday]);

        Assert.Equal([new LocalDate(2026, 10, 20)], DaysWith(grid, "Birthday"));
    }

    [Fact]
    public void AllDayEvent_OnLeapDay_AppearsInFebruaryGrid()
    {
        var leap = AllDay("Leap", new LocalDate(2028, 2, 29), new LocalDate(2028, 2, 29));

        var grid = MonthGrid.Build(2028, 2, Chicago, Today, [leap]);

        Assert.Equal([new LocalDate(2028, 2, 29)], DaysWith(grid, "Leap"));
    }

    [Fact]
    public void AllDayEvent_SpanningMonths_AppearsInBothGrids()
    {
        var trip = AllDay("Trip", new LocalDate(2026, 10, 30), new LocalDate(2026, 11, 2));

        var october = MonthGrid.Build(2026, 10, Chicago, Today, [trip]);
        var november = MonthGrid.Build(2026, 11, Chicago, Today, [trip]);

        Assert.Equal([new LocalDate(2026, 10, 30), new LocalDate(2026, 10, 31)], DaysWith(october, "Trip"));
        Assert.Equal([new LocalDate(2026, 11, 1), new LocalDate(2026, 11, 2)], DaysWith(november, "Trip"));
    }

    [Fact]
    public void TimedEvent_OnFallBackDay_AppearsOnce()
    {
        // 01:30 resolves to the earlier occurrence; 02:30 is after the repeated hour.
        var ev = TimedLocal("Overlap", Chicago, new(2026, 11, 1, 1, 30), new(2026, 11, 1, 2, 30));

        var grid = MonthGrid.Build(2026, 11, Chicago, Today, [ev]);

        Assert.Equal([new LocalDate(2026, 11, 1)], DaysWith(grid, "Overlap"));
    }

    [Fact]
    public void Occurrences_OfOneSeries_ArePlacedOnTheirOwnDays()
    {
        var id = EventId.New();
        var wednesday = Items.Of(id, "Gym", AllDaySchedule.Create(new(2026, 10, 21), new(2026, 10, 21)).Value!, new LocalDate(2026, 10, 21));
        var friday = Items.Of(id, "Gym", AllDaySchedule.Create(new(2026, 10, 23), new(2026, 10, 23)).Value!, new LocalDate(2026, 10, 23));

        var grid = MonthGrid.Build(2026, 10, Chicago, Today, [friday, wednesday]);

        Assert.Equal([new LocalDate(2026, 10, 21), new LocalDate(2026, 10, 23)], DaysWith(grid, "Gym"));
        Assert.Equal(new LocalDate(2026, 10, 23), Day(grid, new LocalDate(2026, 10, 23)).Events.Single().Occurrence!.OriginalDate);
    }
}
