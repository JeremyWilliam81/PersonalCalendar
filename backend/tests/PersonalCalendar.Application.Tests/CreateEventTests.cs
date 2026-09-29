using NodaTime;
using NodaTime.Testing;
using PersonalCalendar.Application.Events;
using PersonalCalendar.Application.Tests.Fakes;
using PersonalCalendar.Application.Time;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Tests;

public class CreateEventTests
{
    private readonly InMemoryEventRepository _repository = new();
    private readonly FakeClock _clock = new(Instant.FromUtc(2026, 9, 29, 15, 0));

    private CreateEvent UseCase => new(_repository, new ZoneLookup(DateTimeZoneProviders.Tzdb), _clock);

    private static EventInput Timed(string timeZone, LocalDateTime start, LocalDateTime end, bool accept = false) =>
        new("Dentist", null, null, false, timeZone, start, end, null, null, accept);

    [Fact]
    public async Task ValidInput_IsSavedAndReturnedInZone()
    {
        var result = await UseCase.HandleAsync(Timed("America/Chicago", new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0)));

        var details = Assert.IsType<UseCaseResult<EventDetails>.Ok>(result).Value;
        Assert.Equal(1, details.Version);
        Assert.Equal("America/Chicago", details.TimeZone);
        Assert.Equal(new LocalDateTime(2026, 10, 14, 9, 0), details.Start!.Value.LocalDateTime);
        Assert.Equal(Offset.FromHours(-5), details.Start!.Value.Offset);
        Assert.Single(_repository.All);
    }

    [Fact]
    public async Task UnknownTimeZone_IsRejected()
    {
        var result = await UseCase.HandleAsync(Timed("Mars/Base", new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0)));

        var failed = Assert.IsType<UseCaseResult<EventDetails>.ValidationFailed>(result);
        Assert.Contains(failed.Errors.Errors, e => e is { Field: "timeZone", Code: ErrorCodes.TimeZoneUnknown });
        Assert.Empty(_repository.All);
    }

    [Fact]
    public async Task TimeInDstGap_RequiresAdjustment_AndSavesNothing()
    {
        var result = await UseCase.HandleAsync(Timed("America/Chicago", new(2027, 3, 14, 2, 30), new(2027, 3, 14, 4, 0)));

        var adjustment = Assert.IsType<UseCaseResult<EventDetails>.AdjustmentRequired>(result);
        Assert.Equal(new LocalDateTime(2027, 3, 14, 3, 30), adjustment.AdjustedStart);
        Assert.Equal("America/Chicago", adjustment.TimeZone);
        Assert.Empty(_repository.All);
    }

    [Fact]
    public async Task InvalidFields_AndInvalidSchedule_AreReportedTogether()
    {
        var input = new EventInput("  ", null, null, false, "America/Chicago",
            new(2026, 10, 14, 9, 0), new(2026, 10, 14, 9, 0), null, null);

        var result = await UseCase.HandleAsync(input);

        var failed = Assert.IsType<UseCaseResult<EventDetails>.ValidationFailed>(result);
        Assert.Contains(failed.Errors.Errors, e => e.Code == ErrorCodes.TitleRequired);
        Assert.Contains(failed.Errors.Errors, e => e.Code == ErrorCodes.EndNotAfterStart);
    }
}
