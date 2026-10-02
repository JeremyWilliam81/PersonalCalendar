using NodaTime;
using NodaTime.Testing;
using PersonalCalendar.Domain.Calendar;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Domain.Tests;

/// <summary>Multi-day all-day bars across the week view (research V5, FR-005).</summary>
public class AllDayLanesTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
    private static readonly IClock Clock = new FakeClock(Instant.FromUtc(2026, 9, 29, 15, 0));
    private static readonly LocalDate WeekStart = new(2026, 10, 11);

    private static CalendarEvent AllDay(string title, LocalDate start, LocalDate end) =>
        CalendarEvent.Create(title, null, null, AllDaySchedule.Create(start, end).Value!, Chicago, Clock).Value!;

    private static AllDayBar Bar(IReadOnlyList<AllDayBar> bars, string title) => bars.Single(b => b.Event.Title == title);

    [Fact]
    public void BarRunningPastTheWeek_IsClippedAndFlagged()
    {
        var bars = AllDayLanes.Build(WeekStart, 7, [AllDay("Trip", new(2026, 10, 16), new(2026, 10, 20))]);

        var trip = Bar(bars, "Trip");
        Assert.Equal((5, 2, false, true), (trip.StartIndex, trip.Span, trip.ContinuesBefore, trip.ContinuesAfter));
    }

    [Fact]
    public void BarStartingBeforeTheWeek_IsClippedAndFlagged()
    {
        var bars = AllDayLanes.Build(WeekStart, 7, [AllDay("Away", new(2026, 10, 9), new(2026, 10, 12))]);

        var away = Bar(bars, "Away");
        Assert.Equal((0, 2, true, false), (away.StartIndex, away.Span, away.ContinuesBefore, away.ContinuesAfter));
    }

    [Fact]
    public void OverlappingBars_UseSeparateLanes_AndFreeLanesAreReused()
    {
        var bars = AllDayLanes.Build(WeekStart, 7,
        [
            AllDay("A", new(2026, 10, 11), new(2026, 10, 13)),
            AllDay("B", new(2026, 10, 12), new(2026, 10, 14)),
            AllDay("C", new(2026, 10, 15), new(2026, 10, 16)),
        ]);

        Assert.Equal(0, Bar(bars, "A").Lane);
        Assert.Equal(1, Bar(bars, "B").Lane);
        Assert.Equal(0, Bar(bars, "C").Lane);
    }

    [Fact]
    public void WeekAcrossTheYearBoundary_PlacesBarsByPlainDates()
    {
        var bars = AllDayLanes.Build(new LocalDate(2026, 12, 27), 7, [AllDay("NYE", new(2026, 12, 31), new(2027, 1, 1))]);

        Assert.Equal((4, 2), (Bar(bars, "NYE").StartIndex, Bar(bars, "NYE").Span));
    }

    [Fact]
    public void EventsOutsideTheRange_AreLeftOut()
    {
        Assert.Empty(AllDayLanes.Build(WeekStart, 7, [AllDay("Later", new(2026, 10, 18), new(2026, 10, 19))]));
    }

    [Fact]
    public void Invariants_HoldForAMixOfBars()
    {
        var events = Enumerable.Range(0, 12)
            .Select(i => AllDay($"E{i}", WeekStart.PlusDays(i % 9 - 2), WeekStart.PlusDays(i % 9 - 2 + i % 4)))
            .ToList();

        var bars = AllDayLanes.Build(WeekStart, 7, events);

        Assert.All(bars, b =>
        {
            Assert.InRange(b.StartIndex, 0, 6);
            Assert.True(b.Span >= 1);
            Assert.True(b.StartIndex + b.Span <= 7);
        });
        foreach (var lane in bars.GroupBy(b => b.Lane))
        {
            var days = lane.SelectMany(b => Enumerable.Range(b.StartIndex, b.Span)).ToList();
            Assert.Equal(days.Count, days.Distinct().Count());
        }
    }
}
