using NodaTime;
using NodaTime.Testing;
using PersonalCalendar.Application.Events;
using PersonalCalendar.Application.Tests.Fakes;
using PersonalCalendar.Application.Time;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Application.Tests;

/// <summary>Use cases for recurring events (specs/003-recurring-events).</summary>
public class RecurringUseCaseTests
{
    private const string Chicago = "America/Chicago";

    private readonly InMemoryEventRepository _repository = new();
    private readonly FakeClock _clock = new(Instant.FromUtc(2026, 9, 29, 15, 0));
    private readonly ZoneLookup _zones = new(DateTimeZoneProviders.Tzdb);

    private CreateEvent Create => new(_repository, _zones, _clock);

    private UpdateEvent Update => new(_repository, _zones, _clock);

    private GetMonthView Month => new(_repository, _zones, _clock);

    private GetDaysView Days => new(_repository, _zones, _clock);

    private static RecurrenceInput Weekly(params IsoDayOfWeek[] days) =>
        new(RepeatFrequency.Weekly, 1, days, null, RepeatEnd.Never.Instance);

    private static RecurrenceInput Daily(RepeatEnd? end = null) =>
        new(RepeatFrequency.Daily, 1, [], null, end ?? RepeatEnd.Never.Instance);

    private static EventInput Timed(
        LocalDateTime start, LocalDateTime end, RecurrenceInput? recurrence, string title = "Gym", int? version = null) =>
        new(title, null, null, false, Chicago, start, end, null, null, Version: version, Recurrence: recurrence);

    private async Task<EventDetails> CreateAsync(EventInput input) =>
        Assert.IsType<UseCaseResult<EventDetails>.Ok>(await Create.HandleAsync(input)).Value;

    private async Task<MonthViewModel> MonthAsync(int year, int month, string zone = Chicago) =>
        Assert.IsType<UseCaseResult<MonthViewModel>.Ok>(await Month.HandleAsync(year, month, zone)).Value;

    private static IEnumerable<EventSummary> On(MonthViewModel view, LocalDate date) =>
        view.Weeks.SelectMany(w => w.Days).Single(d => d.Date == date).Events;

    private static List<LocalDate> DatesWith(MonthViewModel view, string title) =>
        view.Weeks.SelectMany(w => w.Days).Where(d => d.InMonth && d.Events.Any(e => e.Title == title)).Select(d => d.Date).ToList();

    // ----- Create (US1) -----

    [Fact]
    public async Task Create_Weekly_NormalizesTheStartToTheFirstChosenWeekday()
    {
        // Tuesday Oct 6 with Thursdays only: the series starts Thursday Oct 8 (FR-009).
        var details = await CreateAsync(Timed(new(2026, 10, 6, 7, 0), new(2026, 10, 6, 8, 0), Weekly(IsoDayOfWeek.Thursday)));

        Assert.Equal(new LocalDateTime(2026, 10, 8, 7, 0), details.Start!.Value.LocalDateTime);
        Assert.Equal(new LocalDate(2026, 10, 8), details.OccurrenceDate);
        Assert.Equal(Chicago, details.Recurrence!.TimeZone);
        Assert.Equal("weekly", details.Recurrence.Frequency);
        Assert.Equal(["thursday"], details.Recurrence.Weekdays);

        var stored = Assert.Single(_repository.All);
        Assert.Equal(new LocalDateTime(2026, 10, 8, 7, 0), stored.Recurrence!.StartLocal);
        Assert.Equal(new LocalDateTime(2026, 10, 8, 8, 0), stored.Recurrence.EndLocal);
        Assert.Equal(Chicago, stored.Recurrence.TimeZone.Id);
    }

    [Fact]
    public async Task Create_ReportsRuleAndTitleErrorsTogether()
    {
        var input = Timed(new(2026, 10, 6, 7, 0), new(2026, 10, 6, 8, 0), Weekly(), title: " ");

        var failed = Assert.IsType<UseCaseResult<EventDetails>.ValidationFailed>(await Create.HandleAsync(input));

        Assert.Contains(failed.Errors.Errors, e => e is { Field: "title", Code: "title.required" });
        Assert.Contains(failed.Errors.Errors, e => e is { Field: "recurrence.weekdays", Code: "recurrence.weekdays.required" });
        Assert.Empty(_repository.All);
    }

    // ----- Views (US1) -----

    [Fact]
    public async Task MonthView_ShowsEveryOccurrence_AndLeavesOneTimeEventsAlone()
    {
        await CreateAsync(Timed(new(2026, 10, 12, 7, 0), new(2026, 10, 12, 8, 0),
            Weekly(IsoDayOfWeek.Monday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Friday)));
        await CreateAsync(Timed(new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0), null, "Dentist"));

        var view = await MonthAsync(2026, 10);

        Assert.Equal(
            [12, 14, 16, 19, 21, 23, 26, 28, 30],
            DatesWith(view, "Gym").Select(d => d.Day));
        var gym = On(view, new LocalDate(2026, 10, 21)).Single(e => e.Title == "Gym");
        Assert.True(gym.IsRecurring);
        Assert.Equal(new LocalDate(2026, 10, 21), gym.OccurrenceDate);
        Assert.Equal(new LocalDateTime(2026, 10, 21, 7, 0), gym.Start!.Value.LocalDateTime);
        var dentist = On(view, new LocalDate(2026, 10, 14)).Single(e => e.Title == "Dentist");
        Assert.False(dentist.IsRecurring);
        Assert.Null(dentist.OccurrenceDate);
    }

    [Fact]
    public async Task MonthView_InAnotherZone_ShowsTheSameMoment()
    {
        await CreateAsync(Timed(new(2026, 10, 12, 7, 0), new(2026, 10, 12, 8, 0), Weekly(IsoDayOfWeek.Wednesday)));

        var view = await MonthAsync(2026, 10, "America/New_York");

        var gym = On(view, new LocalDate(2026, 10, 21)).Single();
        Assert.Equal(new LocalDateTime(2026, 10, 21, 8, 0), gym.Start!.Value.LocalDateTime);
        Assert.Equal(new LocalDate(2026, 10, 21), gym.OccurrenceDate);
    }

    [Fact]
    public async Task DaysView_PlacesOccurrencesOnTheTimeScale()
    {
        await CreateAsync(Timed(new(2026, 10, 12, 7, 0), new(2026, 10, 12, 8, 0),
            Weekly(IsoDayOfWeek.Monday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Friday)));

        var view = Assert.IsType<UseCaseResult<DaysViewModel>.Ok>(await Days.HandleAsync("2026-10-18", 7, Chicago)).Value;

        Assert.Equal([1, 3, 5], view.Days.Select((d, i) => (d, i)).Where(x => x.d.Timed.Count == 1).Select(x => x.i));
        Assert.All(view.Days.SelectMany(d => d.Timed), s =>
        {
            Assert.Equal(7 * 60, s.OffsetMinutes);
            Assert.True(s.Event.IsRecurring);
        });
    }

    // ----- Turning a one-time event into a series (US1 scenario 12) -----

    [Fact]
    public async Task Update_OneTimeEvent_WithARule_BecomesASeriesFromItsDate()
    {
        var created = await CreateAsync(Timed(new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0), null, "Standup"));

        var result = await Update.HandleAsync(
            created.Id, Timed(new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0), Daily(new RepeatEnd.AfterCount(3)), "Standup", created.Version));

        var details = Assert.IsType<UseCaseResult<EventDetails>.Ok>(result).Value;
        Assert.Equal(created.Id, details.Id);
        Assert.Equal(created.Version + 1, details.Version);
        Assert.Equal("daily", details.Recurrence!.Frequency);
        Assert.Equal([14, 15, 16], DatesWith(await MonthAsync(2026, 10), "Standup").Select(d => d.Day));
    }

    // ----- Editing with a scope (US3) -----

    private static RecurrenceInput Mwf() => Weekly(IsoDayOfWeek.Monday, IsoDayOfWeek.Wednesday, IsoDayOfWeek.Friday);

    private Task<EventDetails> CreateGymAsync(RecurrenceInput? recurrence = null) =>
        CreateAsync(Timed(new(2026, 10, 12, 7, 0), new(2026, 10, 12, 8, 0), recurrence ?? Mwf()));

    private Task<UseCaseResult<EventDetails>> EditAsync(
        EventDetails series, LocalDate occurrence, EditScope? scope, LocalDateTime start, LocalDateTime end,
        string title = "Gym", RecurrenceInput? recurrence = null, int? version = null) =>
        Update.HandleAsync(series.Id, Timed(start, end, recurrence ?? Mwf(), title, version ?? series.Version), occurrence, scope);

    [Fact]
    public async Task Update_This_StoresOneException()
    {
        var gym = await CreateGymAsync();

        var result = await EditAsync(gym, new(2026, 10, 21), EditScope.This, new(2026, 10, 21, 18, 0), new(2026, 10, 21, 19, 0));

        var details = Assert.IsType<UseCaseResult<EventDetails>.Ok>(result).Value;
        Assert.Equal(new LocalDate(2026, 10, 21), details.OccurrenceDate);
        Assert.True(details.IsException);
        Assert.Equal(1, details.ExceptionCount);
        Assert.Equal(new LocalDateTime(2026, 10, 21, 18, 0), details.Start!.Value.LocalDateTime);
        Assert.Single(Assert.Single(_repository.All).Exceptions);
    }

    [Fact]
    public async Task Update_Following_TruncatesAndCreatesANewSeries_InOneSave()
    {
        var gym = await CreateGymAsync();

        var result = await EditAsync(gym, new(2026, 11, 2), EditScope.Following, new(2026, 11, 2, 7, 0), new(2026, 11, 2, 8, 0), "Swim");

        var details = Assert.IsType<UseCaseResult<EventDetails>.Ok>(result).Value;
        Assert.NotEqual(gym.Id, details.Id);
        Assert.Equal("Swim", details.Title);
        Assert.Equal(1, _repository.ReplaceCalls);
        Assert.Equal(2, _repository.All.Count);
        var november = await MonthAsync(2026, 11);
        Assert.Contains(new LocalDate(2026, 11, 2), DatesWith(november, "Swim"));
        Assert.DoesNotContain(new LocalDate(2026, 11, 2), DatesWith(november, "Gym"));
        Assert.Contains(new LocalDate(2026, 10, 30), DatesWith(await MonthAsync(2026, 10), "Gym"));
    }

    [Fact]
    public async Task Update_Following_TextOnly_MovesLaterExceptions()
    {
        var gym = await CreateGymAsync();
        var edited = Assert.IsType<UseCaseResult<EventDetails>.Ok>(
            await EditAsync(gym, new(2026, 11, 4), EditScope.This, new(2026, 11, 4, 18, 0), new(2026, 11, 4, 19, 0))).Value;

        var result = await EditAsync(gym, new(2026, 11, 2), EditScope.Following, new(2026, 11, 2, 7, 0), new(2026, 11, 2, 8, 0), "Swim", version: edited.Version);

        var tail = Assert.IsType<UseCaseResult<EventDetails>.Ok>(result).Value;
        Assert.Equal(1, tail.ExceptionCount);
        var original = _repository.All.Single(e => e.Id.Value == gym.Id);
        Assert.Empty(original.Exceptions);
    }

    [Fact]
    public async Task Update_All_UpdatesTheSeries()
    {
        var gym = await CreateGymAsync();

        var result = await EditAsync(gym, new(2026, 10, 21), EditScope.All, new(2026, 10, 21, 6, 30), new(2026, 10, 21, 7, 30));

        Assert.IsType<UseCaseResult<EventDetails>.Ok>(result);
        var month = await MonthAsync(2026, 10);
        Assert.All(month.Weeks.SelectMany(w => w.Days).SelectMany(d => d.Events), e => Assert.Equal(new LocalTime(6, 30), e.Start!.Value.TimeOfDay));
    }

    [Theory]
    [InlineData(false, true, "occurrence", "occurrence.required")]
    [InlineData(true, false, "scope", "scope.required")]
    public async Task Update_Series_RequiresOccurrenceAndScope(bool withOccurrence, bool withScope, string field, string code)
    {
        var gym = await CreateGymAsync();

        var result = await Update.HandleAsync(
            gym.Id, Timed(new(2026, 10, 21, 7, 0), new(2026, 10, 21, 8, 0), Mwf(), version: gym.Version),
            withOccurrence ? new LocalDate(2026, 10, 21) : null, withScope ? EditScope.This : null);

        var failed = Assert.IsType<UseCaseResult<EventDetails>.ValidationFailed>(result);
        Assert.Contains(failed.Errors.Errors, e => e.Field == field && e.Code == code);
    }

    [Fact]
    public async Task Update_OneTimeEvent_WithAScope_IsRejected()
    {
        var created = await CreateAsync(Timed(new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0), null, "Dentist"));

        var result = await Update.HandleAsync(
            created.Id, Timed(new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0), null, "Dentist", created.Version), null, EditScope.All);

        var failed = Assert.IsType<UseCaseResult<EventDetails>.ValidationFailed>(result);
        Assert.Contains(failed.Errors.Errors, e => e is { Field: "scope", Code: "scope.invalid" });
    }

    [Fact]
    public async Task Update_Series_WithAStaleVersion_IsAConflict()
    {
        var gym = await CreateGymAsync();

        var result = await EditAsync(gym, new(2026, 10, 21), EditScope.This, new(2026, 10, 21, 18, 0), new(2026, 10, 21, 19, 0), version: gym.Version - 1);

        Assert.IsType<UseCaseResult<EventDetails>.Conflict>(result);
    }

    [Fact]
    public async Task Update_Following_WhenTheSaveFails_ChangesNothing()
    {
        var gym = await CreateGymAsync();
        _repository.FailNextReplace = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EditAsync(gym, new(2026, 11, 2), EditScope.Following, new(2026, 11, 2, 7, 0), new(2026, 11, 2, 8, 0), "Swim"));

        var stored = Assert.Single(_repository.All);
        Assert.Equal(gym.Version, stored.Version);
        Assert.Equal(RepeatEnd.Never.Instance, stored.Recurrence!.Rule.End);
    }

    // ----- Deleting with a scope (US4) -----

    private DeleteEvent Delete => new(_repository, _clock);

    [Fact]
    public async Task Delete_This_HidesOneOccurrence()
    {
        var gym = await CreateGymAsync();

        Assert.IsType<UseCaseResult<Unit>.Ok>(await Delete.HandleAsync(gym.Id, gym.Version, new LocalDate(2026, 10, 21), EditScope.This));

        Assert.DoesNotContain(new LocalDate(2026, 10, 21), DatesWith(await MonthAsync(2026, 10), "Gym"));
        Assert.Contains(new LocalDate(2026, 10, 23), DatesWith(await MonthAsync(2026, 10), "Gym"));
    }

    [Fact]
    public async Task Delete_This_OnTheLastOccurrence_DeletesTheSeries()
    {
        var series = await CreateAsync(Timed(new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0), Daily(new RepeatEnd.AfterCount(1)), "Once"));

        Assert.IsType<UseCaseResult<Unit>.Ok>(await Delete.HandleAsync(series.Id, series.Version, new LocalDate(2026, 10, 14), EditScope.This));

        Assert.Empty(_repository.All);
    }

    [Fact]
    public async Task Delete_Following_EndsTheSeries()
    {
        var gym = await CreateGymAsync();

        Assert.IsType<UseCaseResult<Unit>.Ok>(await Delete.HandleAsync(gym.Id, gym.Version, new LocalDate(2026, 11, 2), EditScope.Following));

        Assert.Empty(DatesWith(await MonthAsync(2026, 11), "Gym"));
        Assert.Contains(new LocalDate(2026, 10, 30), DatesWith(await MonthAsync(2026, 10), "Gym"));
    }

    [Fact]
    public async Task Delete_All_RemovesTheSeries()
    {
        var gym = await CreateGymAsync();

        Assert.IsType<UseCaseResult<Unit>.Ok>(await Delete.HandleAsync(gym.Id, gym.Version, new LocalDate(2026, 10, 21), EditScope.All));

        Assert.Empty(_repository.All);
    }

    [Theory]
    [InlineData(false, true, "occurrence.required")]
    [InlineData(true, false, "scope.required")]
    public async Task Delete_Series_RequiresOccurrenceAndScope(bool withOccurrence, bool withScope, string code)
    {
        var gym = await CreateGymAsync();

        var result = await Delete.HandleAsync(gym.Id, gym.Version, withOccurrence ? new LocalDate(2026, 10, 21) : null, withScope ? EditScope.All : null);

        Assert.Contains(Assert.IsType<UseCaseResult<Unit>.ValidationFailed>(result).Errors.Errors, e => e.Code == code);
    }

    [Fact]
    public async Task Delete_Series_WithAStaleVersion_IsAConflict()
    {
        var gym = await CreateGymAsync();

        Assert.IsType<UseCaseResult<Unit>.Conflict>(await Delete.HandleAsync(gym.Id, gym.Version + 1, new LocalDate(2026, 10, 21), EditScope.This));
    }

    // ----- Changing or stopping the repeat (US5) -----

    [Fact]
    public async Task Update_All_ToDoesNotRepeat_LeavesAOneTimeEvent()
    {
        var gym = await CreateGymAsync();

        var result = await Update.HandleAsync(
            gym.Id, Timed(new(2026, 10, 21, 7, 0), new(2026, 10, 21, 8, 0), null, version: gym.Version), new LocalDate(2026, 10, 21), EditScope.All);

        var details = Assert.IsType<UseCaseResult<EventDetails>.Ok>(result).Value;
        Assert.Null(details.Recurrence);
        Assert.Null(details.OccurrenceDate);
        Assert.Equal([new LocalDate(2026, 10, 12)], DatesWith(await MonthAsync(2026, 10), "Gym"));
        Assert.IsType<UseCaseResult<EventDetails>.Ok>(await new GetEventDetails(_repository, _zones).HandleAsync(gym.Id, Chicago));
    }

    [Fact]
    public async Task Update_Following_ToDoesNotRepeat_CreatesAOneTimeEvent()
    {
        var gym = await CreateGymAsync();

        var result = await Update.HandleAsync(
            gym.Id, Timed(new(2026, 11, 2, 7, 0), new(2026, 11, 2, 8, 0), null, version: gym.Version), new LocalDate(2026, 11, 2), EditScope.Following);

        var details = Assert.IsType<UseCaseResult<EventDetails>.Ok>(result).Value;
        Assert.NotEqual(gym.Id, details.Id);
        Assert.Null(details.Recurrence);
        Assert.Equal([new LocalDate(2026, 11, 2)], DatesWith(await MonthAsync(2026, 11), "Gym"));
    }

    [Fact]
    public async Task Update_All_RuleChange_ClearsExceptions()
    {
        var gym = await CreateGymAsync();
        var edited = Assert.IsType<UseCaseResult<EventDetails>.Ok>(
            await EditAsync(gym, new(2026, 10, 21), EditScope.This, new(2026, 10, 21, 18, 0), new(2026, 10, 21, 19, 0))).Value;

        var result = await EditAsync(gym, new(2026, 10, 19), EditScope.All, new(2026, 10, 19, 7, 0), new(2026, 10, 19, 8, 0),
            recurrence: Weekly(IsoDayOfWeek.Monday, IsoDayOfWeek.Thursday), version: edited.Version);

        Assert.Equal(0, Assert.IsType<UseCaseResult<EventDetails>.Ok>(result).Value.ExceptionCount);
        Assert.Equal([12, 15, 19, 22, 26, 29], DatesWith(await MonthAsync(2026, 10), "Gym").Select(d => d.Day));
    }

    // ----- Cross-zone regression (T059, research S4) -----

    [Theory]
    [InlineData("Asia/Kolkata", 2026, 10, 22, 8, 30)]
    [InlineData("Australia/Adelaide", 2026, 10, 22, 13, 30)]
    public async Task Series_ViewedInAnotherZone_MovesToTheLocalDay_ButKeepsItsOriginalDate(
        string zone, int year, int month, int day, int hour, int minute)
    {
        await CreateAsync(Timed(new(2026, 10, 21, 22, 0), new(2026, 10, 21, 23, 0), Weekly(IsoDayOfWeek.Wednesday)));

        var view = await MonthAsync(2026, 10, zone);

        var occurrence = On(view, new LocalDate(year, month, day)).Single();
        Assert.Equal(new LocalDateTime(year, month, day, hour, minute), occurrence.Start!.Value.LocalDateTime);
        Assert.Equal(new LocalDate(2026, 10, 21), occurrence.OccurrenceDate);
        Assert.Empty(On(view, new LocalDate(2026, 10, 21)));
    }
}
