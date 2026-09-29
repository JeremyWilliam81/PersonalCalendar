using NodaTime;
using NodaTime.Testing;
using PersonalCalendar.Application.Events;
using PersonalCalendar.Application.Tests.Fakes;
using PersonalCalendar.Application.Time;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Tests;

public class GetMonthViewTests
{
    private readonly InMemoryEventRepository _repository = new();
    private readonly FakeClock _clock = new(Instant.FromUtc(2026, 9, 29, 15, 0));

    private GetMonthView UseCase => new(_repository, new ZoneLookup(DateTimeZoneProviders.Tzdb), _clock);

    [Fact]
    public async Task QueryRange_IsGridLocalMidnights_AcrossDstChange()
    {
        // November 2026 grid: Sun Nov 1 .. Sat Dec 5. Chicago leaves CDT on Nov 1.
        await UseCase.HandleAsync(2026, 11, "America/Chicago");

        var query = _repository.LastRangeQuery!.Value;
        Assert.Equal(Instant.FromUtc(2026, 11, 1, 5, 0), query.From); // 00:00 CDT
        Assert.Equal(Instant.FromUtc(2026, 12, 6, 6, 0), query.To);   // 00:00 CST on the day after the grid
        Assert.Equal(new LocalDate(2026, 11, 1), query.FromDate);
        Assert.Equal(new LocalDate(2026, 12, 5), query.ToDate);
    }

    [Theory]
    [InlineData("America/Chicago", 9)]
    [InlineData("Asia/Kolkata", 10)]
    public async Task WithoutYearAndMonth_ReturnsCurrentMonthInZone(string zone, int expectedMonth)
    {
        _clock.Reset(Instant.FromUtc(2026, 10, 1, 3, 0));

        var result = await UseCase.HandleAsync(null, null, zone);

        var view = Assert.IsType<UseCaseResult<MonthViewModel>.Ok>(result).Value;
        Assert.Equal(2026, view.Year);
        Assert.Equal(expectedMonth, view.Month);
    }

    [Fact]
    public async Task ReturnsEventSummariesOnTheirDays_WithTodayFlag()
    {
        var create = new CreateEvent(_repository, new ZoneLookup(DateTimeZoneProviders.Tzdb), _clock);
        await create.HandleAsync(new EventInput("Dentist", null, null, false, "America/Chicago",
            new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0), null, null));

        var result = await UseCase.HandleAsync(2026, 10, "America/Chicago");

        var view = Assert.IsType<UseCaseResult<MonthViewModel>.Ok>(result).Value;
        var days = view.Weeks.SelectMany(w => w.Days).ToList();
        var dentistDay = Assert.Single(days, d => d.Events.Count > 0);
        Assert.Equal(new LocalDate(2026, 10, 14), dentistDay.Date);
        Assert.Equal("Dentist", dentistDay.Events[0].Title);
        Assert.Equal(new LocalDateTime(2026, 10, 14, 9, 0), dentistDay.Events[0].Start!.Value.LocalDateTime);
        Assert.Equal(new LocalDate(2026, 9, 29), view.Today);
        Assert.True(days.Single(d => d.Date == new LocalDate(2026, 9, 29)).IsToday);
    }

    [Fact]
    public async Task UnknownZone_IsRejected()
    {
        var result = await UseCase.HandleAsync(2026, 10, "Nowhere/Special");

        var failed = Assert.IsType<UseCaseResult<MonthViewModel>.ValidationFailed>(result);
        Assert.Contains(failed.Errors.Errors, e => e.Code == ErrorCodes.TimeZoneUnknown);
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(0, 1)]
    [InlineData(9999, 1)]
    public async Task OutOfRangeYearOrMonth_IsRejected(int year, int month)
    {
        var result = await UseCase.HandleAsync(year, month, "America/Chicago");

        Assert.IsType<UseCaseResult<MonthViewModel>.ValidationFailed>(result);
    }
}
