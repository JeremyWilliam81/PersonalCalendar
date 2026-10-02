using NodaTime;
using NodaTime.Testing;
using PersonalCalendar.Application.Events;
using PersonalCalendar.Application.Tests.Fakes;
using PersonalCalendar.Application.Time;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Tests;

public class GetDaysViewTests
{
    private const string Chicago = "America/Chicago";

    private readonly InMemoryEventRepository _repository = new();
    private readonly FakeClock _clock = new(Instant.FromUtc(2026, 10, 14, 15, 0)); // 10:00 in Chicago

    private GetDaysView UseCase => new(_repository, new ZoneLookup(DateTimeZoneProviders.Tzdb), _clock);

    private async Task SeedAsync()
    {
        var create = new CreateEvent(_repository, new ZoneLookup(DateTimeZoneProviders.Tzdb), _clock);
        await create.HandleAsync(new EventInput("Dentist", null, null, false, Chicago,
            new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 30), null, null));
        await create.HandleAsync(new EventInput("Trip", null, null, true, Chicago,
            null, null, new(2026, 10, 16), new(2026, 10, 20)));
    }

    private static DaysViewModel Ok(UseCaseResult<DaysViewModel> result) =>
        Assert.IsType<UseCaseResult<DaysViewModel>.Ok>(result).Value;

    private static string[] Codes(UseCaseResult<DaysViewModel> result, string field) =>
        Assert.IsType<UseCaseResult<DaysViewModel>.ValidationFailed>(result).Errors.Errors
            .Where(e => e.Field == field).Select(e => e.Code).ToArray();

    [Fact]
    public async Task OneDay_HasNoAllDayBars()
    {
        await SeedAsync();

        var view = Ok(await UseCase.HandleAsync("2026-10-14", 1, Chicago));

        var day = Assert.Single(view.Days);
        Assert.Equal(new LocalDate(2026, 10, 14), day.Date);
        Assert.Equal(1440, day.LengthMinutes);
        Assert.Equal("Dentist", Assert.Single(day.Timed).Event.Title);
        Assert.Empty(view.AllDayBars);
    }

    [Fact]
    public async Task Week_HasSevenDays_AndAllDayBars()
    {
        await SeedAsync();

        var view = Ok(await UseCase.HandleAsync("2026-10-11", 7, Chicago));

        Assert.Equal(7, view.Days.Count);
        var trip = Assert.Single(view.AllDayBars);
        Assert.Equal((5, 2, true), (trip.StartIndex, trip.Span, trip.ContinuesAfter));
        Assert.All(view.Days.Where(d => d.Date >= new LocalDate(2026, 10, 16)), d => Assert.Equal("Trip", Assert.Single(d.AllDay).Title));
    }

    [Fact]
    public async Task TodayAndNow_ComeFromTheClockInTheZone()
    {
        var view = Ok(await UseCase.HandleAsync("2026-10-11", 7, Chicago));

        Assert.Equal(new LocalDate(2026, 10, 14), view.Today);
        Assert.Equal(_clock.GetCurrentInstant(), view.Now.ToInstant());
        Assert.Equal([new LocalDate(2026, 10, 14)], view.Days.Where(d => d.IsToday).Select(d => d.Date));
    }

    [Fact]
    public async Task QueryRange_IsFromTheFirstDayStartToTheLastDayEnd()
    {
        await UseCase.HandleAsync("2026-11-01", 1, Chicago);

        var query = _repository.LastRangeQuery!.Value;
        Assert.Equal(Instant.FromUtc(2026, 11, 1, 5, 0), query.From);
        Assert.Equal(Instant.FromUtc(2026, 11, 2, 6, 0), query.To);
    }

    [Fact]
    public async Task UnknownZone_IsRejected()
    {
        Assert.Equal(["timeZone.unknown"], Codes(await UseCase.HandleAsync("2026-10-14", 1, "Mars/Base"), "timeZone"));
    }

    [Theory]
    [InlineData("2026-02-30")]
    [InlineData("2026-10-14T00:00")]
    [InlineData(null)]
    public async Task InvalidStart_IsRejected(string? start)
    {
        Assert.Equal(["start.invalid"], Codes(await UseCase.HandleAsync(start, 1, Chicago), "start"));
    }

    [Theory]
    [InlineData("9999-01-01", 1)]
    [InlineData("9998-12-30", 7)]
    public async Task StartOutsideTechnicalLimits_IsRejected(string start, int count)
    {
        Assert.Equal(["start.outOfRange"], Codes(await UseCase.HandleAsync(start, count, Chicago), "start"));
    }

    [Fact]
    public async Task WeekShowingDaysBefore1900_IsAccepted_BecauseTheNavigationRangeIsAUiRule()
    {
        Assert.Equal(7, Ok(await UseCase.HandleAsync("1899-12-31", 7, Chicago)).Days.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(null)]
    public async Task CountOtherThanOneOrSeven_IsRejected(int? count)
    {
        Assert.Equal(["count.invalid"], Codes(await UseCase.HandleAsync("2026-10-14", count, Chicago), "count"));
    }
}
