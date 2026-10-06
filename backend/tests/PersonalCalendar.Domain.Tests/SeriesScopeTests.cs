using NodaTime;
using NodaTime.Testing;
using PersonalCalendar.Domain.Calendar;
using PersonalCalendar.Domain.Events;
using PersonalCalendar.Domain.Validation;
using static PersonalCalendar.Domain.Tests.TestZones;

namespace PersonalCalendar.Domain.Tests;

/// <summary>This / This and following / All for edits (research S6, FR-015 to FR-022).</summary>
public class SeriesScopeTests
{
    private static readonly FakeClock Clock = new(Instant.FromUtc(2026, 9, 29, 15, 0));
    private static readonly IsoDayOfWeek[] Mwf = [IsoDayOfWeek.Monday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Friday];

    private static RepeatRule Weekly(IsoDayOfWeek[] days, RepeatEnd? end = null, string start = "2026-10-12") =>
        RepeatRule.Create(RepeatFrequency.Weekly, 1, days, null, end ?? RepeatEnd.Never.Instance, D(start)).Value!;

    private static RepeatRule Daily(RepeatEnd? end = null, string start = "2026-10-12") =>
        RepeatRule.Create(RepeatFrequency.Daily, 1, [], null, end ?? RepeatEnd.Never.Instance, D(start)).Value!;

    private static TimedSchedule At(string start, string end) =>
        TimedSchedule.Create(LDT(start).InZoneLeniently(Chicago).ToInstant(), LDT(end).InZoneLeniently(Chicago).ToInstant()).Value!;

    /// <summary>"Gym" 7:00–8:00 from Monday Oct 12, 2026.</summary>
    private static CalendarEvent Gym(RepeatRule? rule = null, string location = "Y") =>
        CalendarEvent.Create("Gym", location, null, At("2026-10-12T07:00", "2026-10-12T08:00"), Chicago, Clock, rule ?? Weekly(Mwf)).Value!;

    private static OccurrenceInput Input(string date, string start = "07:00", string end = "08:00", string title = "Gym", string? location = "Y", RepeatRule? repeat = null) =>
        new(title, location, null, At($"{date}T{start}", $"{date}T{end}"), repeat);

    private static SeriesChange Ok(Validated<SeriesChange> result)
    {
        Assert.True(result.IsValid, string.Join(", ", result.Errors.Errors.Select(e => e.Code)));
        return result.Value!;
    }

    private static string Code(Validated<SeriesChange> result) => Assert.Single(result.Errors.Errors).Code;

    private static List<CalendarItem> Items(CalendarEvent series, string from = "2026-10-01", string to = "2026-11-30") =>
        Recurrence.Expand(series, D(from), D(to)).ToList();

    private static CalendarItem Occurrence(CalendarEvent series, string date) =>
        Items(series).Single(i => i.Occurrence!.OriginalDate == D(date));

    private static LocalDateTime LocalStart(CalendarItem item) => ((TimedSchedule)item.Schedule).Start.InZone(Chicago).LocalDateTime;

    // ----- This event -----

    [Fact]
    public void This_ChangesOnlyThatOccurrence()
    {
        var gym = Gym();
        var version = gym.Version;

        Ok(gym.ApplyEdit(EditScope.This, D("2026-10-21"), Input("2026-10-21", "18:00", "19:00", repeat: gym.Recurrence!.Rule), Chicago, Clock));

        Assert.Equal(new LocalDateTime(2026, 10, 21, 18, 0), LocalStart(Occurrence(gym, "2026-10-21")));
        Assert.True(Occurrence(gym, "2026-10-21").Occurrence!.IsException);
        Assert.Equal(new LocalDateTime(2026, 10, 19, 7, 0), LocalStart(Occurrence(gym, "2026-10-19")));
        Assert.Equal(version + 1, gym.Version);
    }

    [Fact]
    public void This_CanMoveTheOccurrenceToAnotherDay()
    {
        var gym = Gym();

        Ok(gym.ApplyEdit(EditScope.This, D("2026-10-21"), Input("2026-10-22", repeat: gym.Recurrence!.Rule), Chicago, Clock));

        var moved = Items(gym).Single(i => i.Occurrence!.OriginalDate == D("2026-10-21"));
        Assert.Equal(new LocalDateTime(2026, 10, 22, 7, 0), LocalStart(moved));
        Assert.DoesNotContain(Items(gym, "2026-10-21", "2026-10-21"), i => LocalStart(i).Date == D("2026-10-21"));
    }

    [Fact]
    public void This_WithARepeatChange_IsRefused()
    {
        var gym = Gym();

        var result = gym.ApplyEdit(EditScope.This, D("2026-10-21"), Input("2026-10-21", repeat: Weekly([IsoDayOfWeek.Monday])), Chicago, Clock);

        Assert.Equal(ErrorCodes.ScopeThisWithRepeatChange, Code(result));
        Assert.Empty(gym.Exceptions);
    }

    // ----- FR-016a -----

    [Theory]
    [InlineData(EditScope.Following)]
    [InlineData(EditScope.All)]
    public void DateChange_OnlyAppliesToThisEvent(EditScope scope)
    {
        var gym = Gym();

        var result = gym.ApplyEdit(scope, D("2026-10-21"), Input("2026-10-22", repeat: gym.Recurrence!.Rule), Chicago, Clock);

        Assert.Equal(ErrorCodes.ScopeDateChangeRequiresThis, Code(result));
    }

    [Theory]
    [InlineData(EditScope.This)]
    [InlineData(EditScope.Following)]
    [InlineData(EditScope.All)]
    public void DateAndRepeatChange_IsRefusedInEveryScope(EditScope scope)
    {
        var gym = Gym();

        var result = gym.ApplyEdit(scope, D("2026-10-21"), Input("2026-10-22", repeat: Weekly([IsoDayOfWeek.Monday])), Chicago, Clock);

        Assert.Equal(ErrorCodes.ScopeDateAndRepeatChanged, Code(result));
        Assert.Equal("start", Assert.Single(result.Errors.Errors).Field);
    }

    [Fact]
    public void TimeOrEndChange_IsNotADateChange()
    {
        var gym = Gym();

        // Same start date, later time, ending the next day.
        Ok(gym.ApplyEdit(EditScope.All, D("2026-10-21"), Input("2026-10-21", "22:00", "23:30", repeat: gym.Recurrence!.Rule), Chicago, Clock));

        Assert.Equal(new LocalDateTime(2026, 10, 19, 22, 0), LocalStart(Occurrence(gym, "2026-10-19")));
    }

    // ----- This and following -----

    [Fact]
    public void Following_SplitsTheSeries_AndKeepsEarlierOccurrences()
    {
        var gym = Gym();

        var change = Ok(gym.ApplyEdit(EditScope.Following, D("2026-11-02"), Input("2026-11-02", title: "Swim", repeat: gym.Recurrence!.Rule), Chicago, Clock));

        Assert.Equal(new RepeatEnd.OnDate(D("2026-11-01")), gym.Recurrence!.Rule.End);
        Assert.All(Items(gym), i => Assert.Equal("Gym", i.Title));
        Assert.Equal(D("2026-10-30"), Items(gym).Max(i => i.Occurrence!.OriginalDate));

        var tail = change.Created!;
        Assert.Equal("Swim", tail.Title);
        Assert.Equal(D("2026-11-02"), tail.SeriesFirstDate);
        Assert.Equal(D("2026-11-02"), change.ShowDate);
        Assert.Equal([D("2026-11-02"), D("2026-11-04"), D("2026-11-06")], Items(tail, "2026-11-01", "2026-11-07").Select(i => i.Occurrence!.OriginalDate));
    }

    [Fact]
    public void Following_WithACount_KeepsTheTotal()
    {
        var gym = Gym(Daily(new RepeatEnd.AfterCount(10)));

        var change = Ok(gym.ApplyEdit(EditScope.Following, D("2026-10-15"), Input("2026-10-15", title: "Run", repeat: gym.Recurrence!.Rule), Chicago, Clock));

        Assert.Equal(new RepeatEnd.AfterCount(3), gym.Recurrence!.Rule.End);
        Assert.Equal(new RepeatEnd.AfterCount(7), change.Created!.Recurrence!.Rule.End);
        Assert.Equal(10, Items(gym).Count + Items(change.Created).Count);
    }

    [Fact]
    public void Following_EveryTwoWeeks_KeepsTheSameDates()
    {
        var rule = RepeatRule.Create(RepeatFrequency.Weekly, 2, [IsoDayOfWeek.Tuesday, IsoDayOfWeek.Thursday], null, RepeatEnd.Never.Instance, D("2026-10-06")).Value!;
        var series = CalendarEvent.Create("Class", null, null, At("2026-10-06T09:00", "2026-10-06T10:00"), Chicago, Clock, rule).Value!;
        var before = Items(series, "2026-10-20", "2026-12-31").Select(i => i.Occurrence!.OriginalDate).ToList();

        var change = Ok(series.ApplyEdit(EditScope.Following, D("2026-10-20"),
            new OccurrenceInput("Class", null, "new notes", At("2026-10-20T09:00", "2026-10-20T10:00"), rule), Chicago, Clock));

        Assert.Equal(before, Items(change.Created!, "2026-10-20", "2026-12-31").Select(i => i.Occurrence!.OriginalDate));
    }

    [Fact]
    public void Following_AtTheFirstOccurrence_ActsLikeAll()
    {
        var gym = Gym();

        var change = Ok(gym.ApplyEdit(EditScope.Following, D("2026-10-12"), Input("2026-10-12", title: "Swim", repeat: gym.Recurrence!.Rule), Chicago, Clock));

        Assert.Null(change.Created);
        Assert.All(Items(gym), i => Assert.Equal("Swim", i.Title));
    }

    [Fact]
    public void Following_TextOnly_MovesLaterExceptionsToTheNewSeries()
    {
        var gym = Gym();
        Ok(gym.ApplyEdit(EditScope.This, D("2026-11-04"), Input("2026-11-04", "18:00", "19:00", repeat: gym.Recurrence!.Rule), Chicago, Clock));
        Ok(gym.ApplyEdit(EditScope.This, D("2026-10-14"), Input("2026-10-14", "06:00", "07:00", repeat: gym.Recurrence!.Rule), Chicago, Clock));

        var change = Ok(gym.ApplyEdit(EditScope.Following, D("2026-11-02"), Input("2026-11-02", location: "Pool", repeat: gym.Recurrence!.Rule), Chicago, Clock));

        Assert.Equal([D("2026-10-14")], gym.Exceptions.Select(e => e.OriginalDate));
        var moved = Occurrence(change.Created!, "2026-11-04");
        Assert.Equal(new LocalDateTime(2026, 11, 4, 18, 0), LocalStart(moved));
        Assert.Equal("Pool", change.Created!.Exceptions.Single().Location);
    }

    // ----- All events -----

    [Fact]
    public void All_TextOnly_PropagatesIntoExceptions_KeepingTheirTimes()
    {
        var gym = Gym();
        Ok(gym.ApplyEdit(EditScope.This, D("2026-10-21"), Input("2026-10-21", "18:00", "19:00", repeat: gym.Recurrence!.Rule), Chicago, Clock));
        Ok(gym.ApplyEdit(EditScope.This, D("2026-10-23"), Input("2026-10-23", "07:00", "08:00", location: "Park", repeat: gym.Recurrence!.Rule), Chicago, Clock));
        gym.ApplyDelete(EditScope.This, D("2026-10-26"), Clock);

        Ok(gym.ApplyEdit(EditScope.All, D("2026-10-19"), Input("2026-10-19", title: "Swim", repeat: gym.Recurrence!.Rule), Chicago, Clock));

        Assert.All(Items(gym), i => Assert.Equal("Swim", i.Title));
        Assert.Equal(new LocalDateTime(2026, 10, 21, 18, 0), LocalStart(Occurrence(gym, "2026-10-21")));
        Assert.Equal("Park", gym.Exceptions.Single(e => e.OriginalDate == D("2026-10-23")).Location); // unchanged field kept
        Assert.DoesNotContain(Items(gym), i => i.Occurrence!.OriginalDate == D("2026-10-26"));
    }

    [Fact]
    public void All_TimeChange_ClearsExceptions()
    {
        var gym = Gym();
        Ok(gym.ApplyEdit(EditScope.This, D("2026-10-21"), Input("2026-10-21", "18:00", "19:00", repeat: gym.Recurrence!.Rule), Chicago, Clock));
        gym.ApplyDelete(EditScope.This, D("2026-10-26"), Clock);

        Ok(gym.ApplyEdit(EditScope.All, D("2026-10-19"), Input("2026-10-19", "06:30", "07:30", repeat: gym.Recurrence!.Rule), Chicago, Clock));

        Assert.Empty(gym.Exceptions);
        Assert.All(Items(gym), i => Assert.Equal(new LocalTime(6, 30), LocalStart(i).TimeOfDay));
        Assert.Contains(Items(gym), i => i.Occurrence!.OriginalDate == D("2026-10-26"));
    }

    [Fact]
    public void All_RuleChange_ClearsExceptions_AndAppliesFromTheSeriesStart()
    {
        var gym = Gym(Weekly([IsoDayOfWeek.Monday]));
        Ok(gym.ApplyEdit(EditScope.This, D("2026-10-19"), Input("2026-10-19", "18:00", "19:00", repeat: gym.Recurrence!.Rule), Chicago, Clock));

        Ok(gym.ApplyEdit(EditScope.All, D("2026-10-19"), Input("2026-10-19", repeat: Weekly([IsoDayOfWeek.Monday, IsoDayOfWeek.Thursday])), Chicago, Clock));

        Assert.Empty(gym.Exceptions);
        Assert.Equal(
            [D("2026-10-12"), D("2026-10-15"), D("2026-10-19"), D("2026-10-22")],
            Items(gym, "2026-10-01", "2026-10-22").Select(i => i.Occurrence!.OriginalDate));
    }

    [Fact]
    public void EveryOperation_IncrementsTheVersionOnce()
    {
        var gym = Gym();
        var version = gym.Version;

        Ok(gym.ApplyEdit(EditScope.Following, D("2026-11-02"), Input("2026-11-02", title: "Swim", repeat: gym.Recurrence!.Rule), Chicago, Clock));

        Assert.Equal(version + 1, gym.Version);
    }

    [Fact]
    public void InvalidText_ChangesNothing()
    {
        var gym = Gym();
        var version = gym.Version;

        var result = gym.ApplyEdit(EditScope.All, D("2026-10-21"), Input("2026-10-21", title: " ", repeat: gym.Recurrence!.Rule), Chicago, Clock);

        Assert.Equal(ErrorCodes.TitleRequired, Code(result));
        Assert.Equal(version, gym.Version);
        Assert.Equal("Gym", gym.Title);
    }

    // ----- Deleting (US4) -----

    [Fact]
    public void DeleteThis_HidesOneOccurrence_AndStillCountsTowardsCount()
    {
        var series = Gym(Daily(new RepeatEnd.AfterCount(10)));

        var change = series.ApplyDelete(EditScope.This, D("2026-10-14"), Clock);

        Assert.False(change.DeleteOriginal);
        Assert.Equal(9, Items(series).Count);
        Assert.Equal(D("2026-10-21"), Items(series).Max(i => i.Occurrence!.OriginalDate)); // the last date doesn't move
        Assert.True(Assert.Single(series.Exceptions).IsDeleted);
    }

    [Fact]
    public void DeleteThis_OnTheLastVisibleOccurrence_DeletesTheSeries()
    {
        var series = Gym(Daily(new RepeatEnd.AfterCount(2)));
        Assert.False(series.ApplyDelete(EditScope.This, D("2026-10-12"), Clock).DeleteOriginal);

        Assert.True(series.ApplyDelete(EditScope.This, D("2026-10-13"), Clock).DeleteOriginal);
    }

    [Fact]
    public void DeleteThis_OnASeriesWithoutAnEnd_NeverEmptiesIt() =>
        Assert.False(Gym().ApplyDelete(EditScope.This, D("2026-10-12"), Clock).DeleteOriginal);

    [Fact]
    public void DeleteFollowing_EndsTheSeriesAndDropsLaterExceptions()
    {
        var gym = Gym();
        Ok(gym.ApplyEdit(EditScope.This, D("2026-11-04"), Input("2026-11-04", "18:00", "19:00", repeat: gym.Recurrence!.Rule), Chicago, Clock));
        Ok(gym.ApplyEdit(EditScope.This, D("2026-10-14"), Input("2026-10-14", "18:00", "19:00", repeat: gym.Recurrence!.Rule), Chicago, Clock));

        var change = gym.ApplyDelete(EditScope.Following, D("2026-11-02"), Clock);

        Assert.False(change.DeleteOriginal);
        Assert.Equal(D("2026-10-30"), Items(gym).Max(i => i.Occurrence!.OriginalDate));
        Assert.Equal([D("2026-10-14")], gym.Exceptions.Select(e => e.OriginalDate));
    }

    [Fact]
    public void DeleteFollowing_AtTheFirstOccurrence_DeletesTheSeries() =>
        Assert.True(Gym().ApplyDelete(EditScope.Following, D("2026-10-12"), Clock).DeleteOriginal);

    [Fact]
    public void DeleteAll_DeletesTheSeries() =>
        Assert.True(Gym().ApplyDelete(EditScope.All, D("2026-10-21"), Clock).DeleteOriginal);

    // ----- Changing or stopping the repeat (US5) -----

    [Fact]
    public void All_DoesNotRepeat_KeepsOnlyTheFirstOccurrence_AsAOneTimeEvent()
    {
        var gym = Gym();
        Ok(gym.ApplyEdit(EditScope.This, D("2026-10-19"), Input("2026-10-19", "18:00", "19:00", repeat: gym.Recurrence!.Rule), Chicago, Clock));
        var version = gym.Version;

        var change = Ok(gym.ApplyEdit(EditScope.All, D("2026-10-21"), Input("2026-10-21", repeat: null), Chicago, Clock));

        Assert.Null(change.Created);
        Assert.Null(gym.Recurrence);
        Assert.Null(gym.SeriesFirstDate);
        Assert.Empty(gym.Exceptions);
        Assert.Equal(new LocalDateTime(2026, 10, 12, 7, 0), ((TimedSchedule)gym.Schedule).Start.InZone(Chicago).LocalDateTime);
        Assert.Equal(version + 1, gym.Version);
    }

    [Fact]
    public void All_DoesNotRepeat_UsesTheFirstOccurrencesOwnChanges()
    {
        var gym = Gym();
        Ok(gym.ApplyEdit(EditScope.This, D("2026-10-12"), Input("2026-10-12", "09:00", "10:00", title: "Kickoff", repeat: gym.Recurrence!.Rule), Chicago, Clock));

        Ok(gym.ApplyEdit(EditScope.All, D("2026-10-21"), Input("2026-10-21", repeat: null), Chicago, Clock));

        Assert.Equal("Kickoff", gym.Title);
        Assert.Equal(new LocalDateTime(2026, 10, 12, 9, 0), ((TimedSchedule)gym.Schedule).Start.InZone(Chicago).LocalDateTime);
    }

    [Fact]
    public void Following_DoesNotRepeat_EndsTheSeries_AndKeepsTheOccurrenceAsAOneTimeEvent()
    {
        var gym = Gym();

        var change = Ok(gym.ApplyEdit(EditScope.Following, D("2026-11-02"), Input("2026-11-02", repeat: null), Chicago, Clock));

        Assert.Equal(new RepeatEnd.OnDate(D("2026-11-01")), gym.Recurrence!.Rule.End);
        var single = change.Created!;
        Assert.Null(single.Recurrence);
        Assert.Equal("Gym", single.Title);
        Assert.Equal(new LocalDateTime(2026, 11, 2, 7, 0), ((TimedSchedule)single.Schedule).Start.InZone(Chicago).LocalDateTime);
    }
}
