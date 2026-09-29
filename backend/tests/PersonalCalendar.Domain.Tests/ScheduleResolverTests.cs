using NodaTime;
using PersonalCalendar.Domain.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Domain.Tests;

public class ScheduleResolverTests
{
    private static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
    private static readonly DateTimeZone Kolkata = DateTimeZoneProviders.Tzdb["Asia/Kolkata"];
    private static readonly DateTimeZone Adelaide = DateTimeZoneProviders.Tzdb["Australia/Adelaide"];

    private static ResolveResult ResolveTimed(DateTimeZone zone, LocalDateTime start, LocalDateTime end, bool accept = false) =>
        ScheduleResolver.Resolve(false, start, end, null, null, zone, accept);

    private static TimedSchedule AssertResolvedTimed(ResolveResult result) =>
        Assert.IsType<TimedSchedule>(Assert.IsType<ResolveResult.Resolved>(result).Schedule);

    [Fact]
    public void SpringForwardGap_WithoutAcceptance_RequiresAdjustment()
    {
        var result = ResolveTimed(Chicago, new(2027, 3, 14, 2, 30), new(2027, 3, 14, 4, 0));

        var adjustment = Assert.IsType<ResolveResult.AdjustmentRequired>(result);
        Assert.Equal(new LocalDateTime(2027, 3, 14, 3, 30), adjustment.AdjustedStart);
        Assert.Equal(new LocalDateTime(2027, 3, 14, 4, 0), adjustment.AdjustedEnd);
    }

    [Fact]
    public void SpringForwardGap_WhenAdjustedEndIsNotAfterStart_IsInvalidRatherThanAdjustment()
    {
        var result = ResolveTimed(Chicago, new(2027, 3, 14, 2, 30), new(2027, 3, 14, 3, 0));

        var invalid = Assert.IsType<ResolveResult.Invalid>(result);
        Assert.Contains(invalid.Errors.Errors, e => e.Code == ErrorCodes.EndNotAfterStart);
    }

    [Fact]
    public void SpringForwardGap_WithAcceptance_ShiftsForwardByGapLength()
    {
        var result = ResolveTimed(Chicago, new(2027, 3, 14, 2, 30), new(2027, 3, 14, 4, 0), accept: true);

        Assert.Equal(Instant.FromUtc(2027, 3, 14, 8, 30), AssertResolvedTimed(result).Start);
    }

    [Fact]
    public void FallBackOverlap_ResolvesToEarlierOccurrence_WithoutAdjustment()
    {
        var result = ResolveTimed(Chicago, new(2026, 11, 1, 1, 30), new(2026, 11, 1, 2, 30));

        // 01:30 CDT (-05:00) is the earlier of the two 01:30s.
        Assert.Equal(Instant.FromUtc(2026, 11, 1, 6, 30), AssertResolvedTimed(result).Start);
    }

    [Fact]
    public void NonHourOffset_Kolkata_ResolvesCorrectly()
    {
        var result = ResolveTimed(Kolkata, new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0));

        Assert.Equal(Instant.FromUtc(2026, 10, 14, 3, 30), AssertResolvedTimed(result).Start);
    }

    [Fact]
    public void NonHourOffset_Adelaide_SpringForwardGap_ShiftsToHalfHourOffset()
    {
        var result = ResolveTimed(Adelaide, new(2026, 10, 4, 2, 30), new(2026, 10, 4, 5, 0), accept: true);

        var zoned = AssertResolvedTimed(result).Start.InZone(Adelaide);
        Assert.Equal(new LocalDateTime(2026, 10, 4, 3, 30), zoned.LocalDateTime);
        Assert.Equal(Offset.FromHoursAndMinutes(10, 30), zoned.Offset);
    }

    [Fact]
    public void NonHourOffset_Adelaide_DaylightTime()
    {
        var result = ResolveTimed(Adelaide, new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0));

        Assert.Equal(Instant.FromUtc(2026, 10, 13, 22, 30), AssertResolvedTimed(result).Start);
    }

    [Fact]
    public void EventSpanningMidnight_IsValid()
    {
        var result = ResolveTimed(Chicago, new(2026, 10, 14, 22, 0), new(2026, 10, 15, 1, 0));

        Assert.IsType<ResolveResult.Resolved>(result);
    }

    [Fact]
    public void EndNotAfterStart_IsInvalid()
    {
        var result = ResolveTimed(Chicago, new(2026, 10, 14, 9, 0), new(2026, 10, 14, 9, 0));

        var invalid = Assert.IsType<ResolveResult.Invalid>(result);
        Assert.Contains(invalid.Errors.Errors, e => e is { Field: "end", Code: ErrorCodes.EndNotAfterStart });
    }

    [Fact]
    public void Timed_MissingStartAndEnd_ReportsBothRequired()
    {
        var result = ScheduleResolver.Resolve(false, null, null, null, null, Chicago, false);

        var invalid = Assert.IsType<ResolveResult.Invalid>(result);
        Assert.Contains(invalid.Errors.Errors, e => e is { Field: "start", Code: ErrorCodes.StartRequired });
        Assert.Contains(invalid.Errors.Errors, e => e is { Field: "end", Code: ErrorCodes.EndRequired });
    }

    [Fact]
    public void AllDay_IgnoresZoneRules_EvenOnDstChangeDay()
    {
        var date = new LocalDate(2027, 3, 14);

        var result = ScheduleResolver.Resolve(true, null, null, date, date, Chicago, false);

        var schedule = Assert.IsType<AllDaySchedule>(Assert.IsType<ResolveResult.Resolved>(result).Schedule);
        Assert.Equal(date, schedule.StartDate);
        Assert.Equal(date, schedule.EndDate);
    }

    [Fact]
    public void AllDay_EndDateBeforeStart_IsInvalid()
    {
        var result = ScheduleResolver.Resolve(true, null, null, new LocalDate(2026, 10, 23), new LocalDate(2026, 10, 20), Chicago, false);

        var invalid = Assert.IsType<ResolveResult.Invalid>(result);
        Assert.Contains(invalid.Errors.Errors, e => e is { Field: "endDate", Code: ErrorCodes.EndDateBeforeStart });
    }

    [Fact]
    public void AllDay_MissingDates_ReportsRequired()
    {
        var result = ScheduleResolver.Resolve(true, null, null, null, null, Chicago, false);

        var invalid = Assert.IsType<ResolveResult.Invalid>(result);
        Assert.Contains(invalid.Errors.Errors, e => e is { Field: "startDate", Code: ErrorCodes.StartDateRequired });
        Assert.Contains(invalid.Errors.Errors, e => e is { Field: "endDate", Code: ErrorCodes.EndDateRequired });
    }
}
