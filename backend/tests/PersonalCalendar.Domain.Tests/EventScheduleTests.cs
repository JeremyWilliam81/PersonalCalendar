using NodaTime;
using PersonalCalendar.Domain.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Domain.Tests;

public class EventScheduleTests
{
    private static readonly Instant Start = Instant.FromUtc(2026, 10, 14, 14, 0);

    [Fact]
    public void Timed_EndEqualToStart_IsRejected()
    {
        var result = TimedSchedule.Create(Start, Start);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors.Errors, e => e is { Field: "end", Code: ErrorCodes.EndNotAfterStart });
    }

    [Fact]
    public void Timed_EndBeforeStart_IsRejected()
    {
        var result = TimedSchedule.Create(Start, Start - Duration.FromMinutes(1));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Timed_EndOneMinuteAfterStart_IsAccepted()
    {
        var result = TimedSchedule.Create(Start, Start + Duration.FromMinutes(1));

        Assert.True(result.IsValid);
        Assert.Equal(Start, result.Value!.Start);
    }

    [Fact]
    public void Timed_ComparesInstantsNotWallClock_AcrossFallBackOverlap()
    {
        // America/Chicago falls back on 2026-11-01, so 01:00-01:59 happens twice.
        var zone = DateTimeZoneProviders.Tzdb["America/Chicago"];
        var secondOneThirty = zone.MapLocal(new LocalDateTime(2026, 11, 1, 1, 30)).Last();
        var firstOneFortyFive = zone.MapLocal(new LocalDateTime(2026, 11, 1, 1, 45)).First();

        var result = TimedSchedule.Create(secondOneThirty.ToInstant(), firstOneFortyFive.ToInstant());

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AllDay_SameStartAndEndDate_IsAcceptedAsOneDay()
    {
        var date = new LocalDate(2026, 10, 14);

        var result = AllDaySchedule.Create(date, date);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void AllDay_MultiDayInclusiveRange_IsAccepted()
    {
        var result = AllDaySchedule.Create(new LocalDate(2026, 10, 20), new LocalDate(2026, 10, 23));

        Assert.True(result.IsValid);
        Assert.Equal(new LocalDate(2026, 10, 23), result.Value!.EndDate);
    }

    [Fact]
    public void AllDay_EndDateBeforeStartDate_IsRejected()
    {
        var result = AllDaySchedule.Create(new LocalDate(2026, 10, 23), new LocalDate(2026, 10, 20));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors.Errors, e => e is { Field: "endDate", Code: ErrorCodes.EndDateBeforeStart });
    }

    [Fact]
    public void AllDay_LeapDay_IsAccepted()
    {
        var leapDay = new LocalDate(2028, 2, 29);

        var result = AllDaySchedule.Create(leapDay, leapDay);

        Assert.True(result.IsValid);
    }
}
