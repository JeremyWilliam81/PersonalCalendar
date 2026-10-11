using NodaTime;
using NodaTime.Testing;
using PersonalCalendar.Domain.Calendar;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Domain.Tests;

/// <summary>Day and week view layout on a time scale measured from the real start of the day (research V3, V4).</summary>
public class DayTimelineTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
    private static readonly DateTimeZone Kolkata = DateTimeZoneProviders.Tzdb["Asia/Kolkata"];
    private static readonly DateTimeZone Adelaide = DateTimeZoneProviders.Tzdb["Australia/Adelaide"];
    private static readonly DateTimeZone LordHowe = DateTimeZoneProviders.Tzdb["Australia/Lord_Howe"];
    private static readonly IClock Clock = new FakeClock(Instant.FromUtc(2026, 9, 29, 15, 0));
    private static readonly LocalDate Oct14 = new(2026, 10, 14);
    private static readonly LocalDate FallBack = new(2026, 11, 1);

    private static CalendarItem Timed(string title, Instant start, Instant end) =>
        Items.OneTime(CalendarEvent.Create(title, null, null, TimedSchedule.Create(start, end).Value!, Chicago, Clock).Value!);

    private static CalendarItem Local(string title, LocalDateTime start, LocalDateTime end) =>
        Timed(title, start.InZoneStrictly(Chicago).ToInstant(), end.InZoneStrictly(Chicago).ToInstant());

    private static CalendarItem AllDay(string title, LocalDate start, LocalDate end) =>
        Items.OneTime(CalendarEvent.Create(title, null, null, AllDaySchedule.Create(start, end).Value!, Chicago, Clock).Value!);

    private static TimedSegment Segment(DayTimelineResult day, string title) => day.Timed.Single(s => s.Event.Title == title);

    [Fact]
    public void NormalDay_Has1440Minutes_And24HourMarks()
    {
        var day = DayTimeline.Build(Oct14, Chicago, []);

        Assert.Equal(1440, day.LengthMinutes);
        Assert.Equal(Enumerable.Range(0, 24).Select(h => h * 60), day.HourMarks.Select(m => m.OffsetMinutes));
        Assert.Equal(Enumerable.Range(0, 24).Select(h => new LocalTime(h, 0)), day.HourMarks.Select(m => m.Label));
    }

    [Fact]
    public void SpringForward_Has1380Minutes_AndNoTwoOClockMark()
    {
        var day = DayTimeline.Build(new LocalDate(2027, 3, 14), Chicago, []);

        Assert.Equal(1380, day.LengthMinutes);
        Assert.DoesNotContain(day.HourMarks, m => m.Label == new LocalTime(2, 0));
        Assert.Equal(120, day.HourMarks.Single(m => m.Label == new LocalTime(3, 0)).OffsetMinutes);
        Assert.Equal(23, day.HourMarks.Count);
    }

    [Fact]
    public void FallBack_Has1500Minutes_AndTheOneOClockHourTwice()
    {
        var day = DayTimeline.Build(FallBack, Chicago, []);

        Assert.Equal(1500, day.LengthMinutes);
        Assert.Equal([60, 120], day.HourMarks.Where(m => m.Label == new LocalTime(1, 0)).Select(m => m.OffsetMinutes));
        Assert.Equal(180, day.HourMarks.Single(m => m.Label == new LocalTime(2, 0)).OffsetMinutes);
        Assert.Equal(25, day.HourMarks.Count);
    }

    [Fact]
    public void LordHowe_HalfHourDstShift_GivesA1410MinuteDay()
    {
        var day = DayTimeline.Build(new LocalDate(2026, 10, 4), LordHowe, []);

        Assert.Equal(1410, day.LengthMinutes);
        Assert.All(day.HourMarks, m => Assert.Equal(0, m.Label.Minute));
    }

    [Fact]
    public void Adelaide_DstStart_GivesA1380MinuteDay()
    {
        Assert.Equal(1380, DayTimeline.Build(new LocalDate(2026, 10, 4), Adelaide, []).LengthMinutes);
    }

    [Fact]
    public void Kolkata_DayStartsAtLocalMidnight()
    {
        var day = DayTimeline.Build(Oct14, Kolkata, []);

        Assert.Equal(1440, day.LengthMinutes);
        Assert.Equal(Instant.FromUtc(2026, 10, 13, 18, 30), day.DayStart);
        Assert.Equal(Instant.FromUtc(2026, 10, 14, 18, 30), day.DayEnd);
    }

    [Fact]
    public void Segment_IsPlacedByElapsedMinutes()
    {
        var day = DayTimeline.Build(Oct14, Chicago, [Local("Dentist", new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 30))]);

        var segment = Segment(day, "Dentist");
        Assert.Equal(540, segment.OffsetMinutes);
        Assert.Equal(90, segment.DurationMinutes);
        Assert.False(segment.ContinuesBefore);
        Assert.False(segment.ContinuesAfter);
    }

    [Fact]
    public void EventAcrossMidnight_IsClippedToEachDay()
    {
        var late = Local("Late show", new(2026, 10, 14, 22, 0), new(2026, 10, 15, 1, 0));

        var first = Segment(DayTimeline.Build(Oct14, Chicago, [late]), "Late show");
        var second = Segment(DayTimeline.Build(Oct14.PlusDays(1), Chicago, [late]), "Late show");

        Assert.Equal((1320, 120, false, true), (first.OffsetMinutes, first.DurationMinutes, first.ContinuesBefore, first.ContinuesAfter));
        Assert.Equal((0, 60, true, false), (second.OffsetMinutes, second.DurationMinutes, second.ContinuesBefore, second.ContinuesAfter));
    }

    [Fact]
    public void EventEndingExactlyAtMidnight_IsNotOnTheNextDay()
    {
        var ev = Local("Evening", new(2026, 10, 14, 23, 0), new(2026, 10, 15, 0, 0));

        Assert.Single(DayTimeline.Build(Oct14, Chicago, [ev]).Timed);
        Assert.Empty(DayTimeline.Build(Oct14.PlusDays(1), Chicago, [ev]).Timed);
    }

    [Fact]
    public void FallBack_EventAcrossTheRepeatedHour_UsesElapsedTime()
    {
        // 01:30 CDT (-05:00, 06:30Z) to 01:30 CST (-06:00, 07:30Z).
        var ev = Timed("Overlap", Instant.FromUtc(2026, 11, 1, 6, 30), Instant.FromUtc(2026, 11, 1, 7, 30));

        var segment = Segment(DayTimeline.Build(FallBack, Chicago, [ev]), "Overlap");

        Assert.Equal(90, segment.OffsetMinutes);
        Assert.Equal(60, segment.DurationMinutes);
    }

    [Fact]
    public void VeryShortEvents_KeepAtLeastOneMinute()
    {
        var start = new LocalDateTime(2026, 10, 14, 9, 0).InZoneStrictly(Chicago).ToInstant();
        var day = DayTimeline.Build(Oct14, Chicago,
        [
            Timed("Three", start, start + Duration.FromMinutes(3)),
            Timed("Half", start, start + Duration.FromSeconds(30)),
        ]);

        Assert.Equal(3, Segment(day, "Three").DurationMinutes);
        Assert.Equal(1, Segment(day, "Half").DurationMinutes);
    }

    [Fact]
    public void LeapDay_EventAppearsOnFebruary29Only()
    {
        var leapDay = new LocalDate(2028, 2, 29);
        var ev = Local("Leap", new(2028, 2, 29, 12, 0), new(2028, 2, 29, 13, 0));

        Assert.Single(DayTimeline.Build(leapDay, Chicago, [ev]).Timed);
        Assert.Empty(DayTimeline.Build(leapDay.PlusDays(-1), Chicago, [ev]).Timed);
        Assert.Empty(DayTimeline.Build(leapDay.PlusDays(1), Chicago, [ev]).Timed);
    }

    [Fact]
    public void OverlappingEvents_GetSideBySideColumns()
    {
        var day = DayTimeline.Build(Oct14, Chicago,
        [
            Local("Standup", new(2026, 10, 14, 9, 30), new(2026, 10, 14, 9, 45)),
            Local("Dentist", new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 30)),
            Local("Lunch", new(2026, 10, 14, 9, 15), new(2026, 10, 14, 11, 0)),
        ]);

        Assert.All(day.Timed, s => Assert.Equal(3, s.ColumnCount));
        Assert.Equal(0, Segment(day, "Dentist").Column);
        Assert.Equal(1, Segment(day, "Lunch").Column);
        Assert.Equal(2, Segment(day, "Standup").Column);
    }

    [Fact]
    public void TouchingEvents_DoNotOverlap()
    {
        var day = DayTimeline.Build(Oct14, Chicago,
        [
            Local("A", new(2026, 10, 14, 10, 0), new(2026, 10, 14, 11, 0)),
            Local("B", new(2026, 10, 14, 11, 0), new(2026, 10, 14, 12, 0)),
        ]);

        Assert.All(day.Timed, s => Assert.Equal((0, 1), (s.Column, s.ColumnCount)));
    }

    [Fact]
    public void SeparateClusters_HaveTheirOwnColumnCounts()
    {
        var day = DayTimeline.Build(Oct14, Chicago,
        [
            Local("A", new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0)),
            Local("B", new(2026, 10, 14, 9, 30), new(2026, 10, 14, 10, 30)),
            Local("C", new(2026, 10, 14, 10, 45), new(2026, 10, 14, 11, 0)),
        ]);

        Assert.Equal(2, Segment(day, "A").ColumnCount);
        Assert.Equal(2, Segment(day, "B").ColumnCount);
        Assert.Equal((0, 1), (Segment(day, "C").Column, Segment(day, "C").ColumnCount));
    }

    [Fact]
    public void Segments_AreOrderedByStart_ThenLongerFirst_ThenTitle()
    {
        var events = new[]
        {
            Local("beta", new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0)),
            Local("Alpha", new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0)),
            Local("Long", new(2026, 10, 14, 9, 0), new(2026, 10, 14, 12, 0)),
            Local("Early", new(2026, 10, 14, 8, 0), new(2026, 10, 14, 8, 30)),
        };

        var first = DayTimeline.Build(Oct14, Chicago, events).Timed.Select(s => s.Event.Title).ToList();
        var second = DayTimeline.Build(Oct14, Chicago, events.Reverse()).Timed.Select(s => s.Event.Title).ToList();

        Assert.Equal(["Early", "Long", "Alpha", "beta"], first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void AllDayEvents_AreListedSeparately_InMonthViewOrder()
    {
        var day = DayTimeline.Build(Oct14, Chicago,
        [
            Local("Dentist", new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0)),
            AllDay("Trip", Oct14, Oct14.PlusDays(3)),
            AllDay("Vacation", Oct14.PlusDays(-2), Oct14.PlusDays(2)),
            AllDay("Tomorrow", Oct14.PlusDays(1), Oct14.PlusDays(1)),
        ]);

        Assert.Equal(["Vacation", "Trip"], day.AllDay.Select(e => e.Title));
        Assert.Equal(["Dentist"], day.Timed.Select(s => s.Event.Title));
    }

    [Fact]
    public void Occurrences_OfOneSeries_OnTheSameDay_AreBothPlacedSideBySide()
    {
        // A moved occurrence may land on a date where the series already has one (spec Edge Cases).
        var id = EventId.New();
        var nine = new LocalDateTime(2026, 10, 14, 9, 0).InZoneStrictly(Chicago).ToInstant();
        var own = Items.Of(id, "Gym", TimedSchedule.Create(nine, nine + Duration.FromHours(1)).Value!, Oct14);
        var moved = Items.Of(id, "Gym", TimedSchedule.Create(nine, nine + Duration.FromHours(1)).Value!, Oct14.PlusDays(-2));

        var day = DayTimeline.Build(Oct14, Chicago, [moved, own]);

        Assert.Equal(2, day.Timed.Count);
        Assert.Equal([Oct14.PlusDays(-2), Oct14], day.Timed.Select(s => s.Event.Occurrence!.OriginalDate));
        Assert.Equal([0, 1], day.Timed.Select(s => s.Column));
        Assert.All(day.Timed, s => Assert.Equal(2, s.ColumnCount));
    }
}
