using NodaTime;
using PersonalCalendar.Domain.Events;
using static PersonalCalendar.Domain.Tests.TestZones;

namespace PersonalCalendar.Domain.Tests;

/// <summary>Validation, RRULE round-trip and first-occurrence rules (data-model "RepeatRule", research S2).</summary>
public class RepeatRuleTests
{
    private static readonly IsoDayOfWeek[] NoDays = [];

    private static Validations.Result Validate(
        RepeatFrequency frequency,
        LocalDate start,
        int interval = 1,
        IsoDayOfWeek[]? weekdays = null,
        MonthlyPattern? monthly = null,
        RepeatEnd? end = null) =>
        new(RepeatRule.Create(frequency, interval, weekdays ?? NoDays, monthly, end ?? RepeatEnd.Never.Instance, start));

    private static RepeatRule Rule(
        RepeatFrequency frequency,
        LocalDate start,
        int interval = 1,
        IsoDayOfWeek[]? weekdays = null,
        MonthlyPattern? monthly = null,
        RepeatEnd? end = null)
    {
        var result = RepeatRule.Create(frequency, interval, weekdays ?? NoDays, monthly, end ?? RepeatEnd.Never.Instance, start);
        Assert.True(result.IsValid, string.Join(", ", result.Errors.Errors.Select(e => e.Code)));
        return result.Value!;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Interval_OutsideOneToNinetyNine_IsRejected(int interval)
    {
        var result = Validate(RepeatFrequency.Daily, D("2026-10-06"), interval);

        Assert.Equal(["recurrence.interval.outOfRange"], result.Codes);
        Assert.Equal(["recurrence.interval"], result.Fields);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(99)]
    public void Interval_AtTheLimits_IsAccepted(int interval) =>
        Assert.True(Validate(RepeatFrequency.Daily, D("2026-10-06"), interval).IsValid);

    [Fact]
    public void Weekly_WithNoWeekdays_IsRejected() =>
        Assert.Equal(["recurrence.weekdays.required"], Validate(RepeatFrequency.Weekly, D("2026-10-06")).Codes);

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    public void Count_OutsideOneTo999_IsRejected(int count) =>
        Assert.Equal(
            ["recurrence.count.outOfRange"],
            Validate(RepeatFrequency.Daily, D("2026-10-06"), end: new RepeatEnd.AfterCount(count)).Codes);

    [Theory]
    [InlineData(1)]
    [InlineData(999)]
    public void Count_AtTheLimits_IsAccepted(int count) =>
        Assert.True(Validate(RepeatFrequency.Daily, D("2026-10-06"), end: new RepeatEnd.AfterCount(count)).IsValid);

    [Fact]
    public void UntilDate_BeforeStart_IsRejected() =>
        Assert.Equal(
            ["recurrence.until.beforeStart"],
            Validate(RepeatFrequency.Daily, D("2026-10-06"), end: new RepeatEnd.OnDate(D("2026-10-05"))).Codes);

    [Fact]
    public void UntilDate_EqualToStart_IsAccepted() =>
        Assert.True(Validate(RepeatFrequency.Daily, D("2026-10-06"), end: new RepeatEnd.OnDate(D("2026-10-06"))).IsValid);

    [Fact]
    public void UntilDate_BeforeTheAdjustedFirstWeeklyOccurrence_IsRejected() =>
        // Start Tuesday, Thursdays only: the first occurrence is Oct 8, so an Oct 7 end leaves none.
        Assert.Equal(
            ["recurrence.until.beforeStart"],
            Validate(RepeatFrequency.Weekly, D("2026-10-06"), weekdays: [IsoDayOfWeek.Thursday], end: new RepeatEnd.OnDate(D("2026-10-07"))).Codes);

    [Theory]
    [InlineData("2026-10-14", 2, true)]   // Wednesday, day 14 → ceil(14/7) = 2
    [InlineData("2026-10-14", 3, false)]
    [InlineData("2026-10-14", -1, false)] // not in the last 7 days
    [InlineData("2026-10-30", -1, true)]  // Friday in the last 7 days
    [InlineData("2026-10-28", 4, true)]   // 4th Wednesday, also the last one
    [InlineData("2026-10-28", -1, true)]
    [InlineData("2026-10-29", 5, false)]  // 5th Thursday: only "last" is offered
    [InlineData("2026-10-29", -1, true)]
    [InlineData("2026-10-14", 0, false)]
    public void WeekdayPosition_MustMatchTheStartDate(string start, int ordinal, bool valid)
    {
        var result = Validate(RepeatFrequency.Monthly, D(start), monthly: new MonthlyPattern.WeekdayPosition(ordinal));

        if (valid) Assert.True(result.IsValid);
        else Assert.Equal(["recurrence.monthly.invalid"], result.Codes);
    }

    [Fact]
    public void Monthly_WithoutAPattern_IsRejected() =>
        Assert.Equal(["recurrence.monthly.invalid"], Validate(RepeatFrequency.Monthly, D("2026-10-14")).Codes);

    [Fact]
    public void EveryError_IsReportedAtOnce()
    {
        var result = Validate(RepeatFrequency.Weekly, D("2026-10-06"), 0, end: new RepeatEnd.AfterCount(0));

        Assert.Equal(
            ["recurrence.interval.outOfRange", "recurrence.weekdays.required", "recurrence.count.outOfRange"],
            result.Codes);
    }

    [Fact]
    public void Weekdays_AreIgnored_ForOtherFrequencies() =>
        Assert.Empty(Rule(RepeatFrequency.Daily, D("2026-10-06"), weekdays: [IsoDayOfWeek.Monday]).Weekdays);

    public static TheoryData<string, string, bool, string> RRules => new()
    {
        { "daily", "2026-10-06", false, "FREQ=DAILY;INTERVAL=1" },
        { "weekly-tu-th-every-2", "2026-10-06", false, "FREQ=WEEKLY;INTERVAL=2;BYDAY=TU,TH;WKST=SU" },
        { "weekly-sunday-first", "2026-10-11", false, "FREQ=WEEKLY;INTERVAL=1;BYDAY=SU,MO,SA;WKST=SU" },
        { "monthly-14", "2026-10-14", false, "FREQ=MONTHLY;INTERVAL=1;BYMONTHDAY=14" },
        { "monthly-31", "2027-01-31", false, "FREQ=MONTHLY;INTERVAL=1;BYMONTHDAY=28,29,30,31;BYSETPOS=-1" },
        { "monthly-30", "2026-11-30", false, "FREQ=MONTHLY;INTERVAL=1;BYMONTHDAY=28,29,30;BYSETPOS=-1" },
        { "monthly-29", "2027-01-29", false, "FREQ=MONTHLY;INTERVAL=1;BYMONTHDAY=28,29;BYSETPOS=-1" },
        { "monthly-2nd-wed-every-3", "2026-10-14", false, "FREQ=MONTHLY;INTERVAL=3;BYDAY=2WE" },
        { "monthly-last-fri", "2026-10-30", false, "FREQ=MONTHLY;INTERVAL=1;BYDAY=-1FR" },
        { "yearly-feb-29", "2028-02-29", true, "FREQ=YEARLY;INTERVAL=1;BYMONTH=2;BYMONTHDAY=29" },
        { "count", "2026-10-06", false, "FREQ=DAILY;INTERVAL=1;COUNT=10" },
        { "until-timed", "2026-10-06", false, "FREQ=DAILY;INTERVAL=1;UNTIL=20270101T055959Z" },
        { "until-all-day", "2026-10-06", true, "FREQ=DAILY;INTERVAL=1;UNTIL=20261231" },
    };

    private static RepeatRule RuleFor(string name, LocalDate start) => name switch
    {
        "daily" => Rule(RepeatFrequency.Daily, start),
        "weekly-tu-th-every-2" => Rule(RepeatFrequency.Weekly, start, 2, [IsoDayOfWeek.Thursday, IsoDayOfWeek.Tuesday]),
        "weekly-sunday-first" => Rule(RepeatFrequency.Weekly, start, weekdays: [IsoDayOfWeek.Saturday, IsoDayOfWeek.Monday, IsoDayOfWeek.Sunday]),
        "monthly-14" or "monthly-31" or "monthly-30" or "monthly-29" => Rule(RepeatFrequency.Monthly, start, monthly: MonthlyPattern.DayOfMonth.Instance),
        "monthly-2nd-wed-every-3" => Rule(RepeatFrequency.Monthly, start, 3, monthly: new MonthlyPattern.WeekdayPosition(2)),
        "monthly-last-fri" => Rule(RepeatFrequency.Monthly, start, monthly: new MonthlyPattern.WeekdayPosition(-1)),
        "yearly-feb-29" => Rule(RepeatFrequency.Yearly, start),
        "count" => Rule(RepeatFrequency.Daily, start, end: new RepeatEnd.AfterCount(10)),
        "until-timed" or "until-all-day" => Rule(RepeatFrequency.Daily, start, end: new RepeatEnd.OnDate(D("2026-12-31"))),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [MemberData(nameof(RRules))]
    public void RRule_RoundTrips(string name, string start, bool isAllDay, string expected)
    {
        var first = D(start);
        var rule = RuleFor(name, first);

        var text = rule.ToRRule(first, Chicago, isAllDay);

        Assert.Equal(expected, text);
        Assert.Equal(rule, RepeatRule.ParseRRule(text, first, Chicago));
    }

    [Theory]
    [InlineData("FREQ=DAILY;INTERVAL=1;BYHOUR=9")]
    [InlineData("FREQ=SECONDLY;INTERVAL=1")]
    [InlineData("INTERVAL=1")]
    [InlineData("FREQ=DAILY;INTERVAL=x")]
    public void ParseRRule_RejectsAnythingTheFormatterDoesNotWrite(string text) =>
        Assert.Throws<FormatException>(() => RepeatRule.ParseRRule(text, D("2026-10-06"), Chicago));

    [Fact]
    public void FirstOccurrence_Weekly_MovesToTheFirstChosenWeekday() =>
        Assert.Equal(D("2026-10-08"), Rule(RepeatFrequency.Weekly, D("2026-10-06"), weekdays: [IsoDayOfWeek.Thursday]).FirstOccurrence(D("2026-10-06")));

    [Fact]
    public void FirstOccurrence_Weekly_KeepsTheStartWhenItsWeekdayIsChosen() =>
        Assert.Equal(
            D("2026-10-06"),
            Rule(RepeatFrequency.Weekly, D("2026-10-06"), weekdays: [IsoDayOfWeek.Tuesday, IsoDayOfWeek.Thursday]).FirstOccurrence(D("2026-10-06")));

    [Theory]
    [InlineData(RepeatFrequency.Daily)]
    [InlineData(RepeatFrequency.Yearly)]
    public void FirstOccurrence_OtherFrequencies_IsTheStart(RepeatFrequency frequency) =>
        Assert.Equal(D("2026-10-06"), Rule(frequency, D("2026-10-06")).FirstOccurrence(D("2026-10-06")));

    [Fact]
    public void Equality_ComparesWeekdaysBySetContents() =>
        Assert.Equal(
            Rule(RepeatFrequency.Weekly, D("2026-10-06"), weekdays: [IsoDayOfWeek.Tuesday, IsoDayOfWeek.Thursday]),
            Rule(RepeatFrequency.Weekly, D("2026-10-06"), weekdays: [IsoDayOfWeek.Thursday, IsoDayOfWeek.Tuesday]));

    private static class Validations
    {
        public sealed class Result(PersonalCalendar.Domain.Validation.Validated<RepeatRule> validated)
        {
            public bool IsValid => validated.IsValid;

            public IEnumerable<string> Codes => validated.Errors.Errors.Select(e => e.Code);

            public IEnumerable<string> Fields => validated.Errors.Errors.Select(e => e.Field);
        }
    }
}
