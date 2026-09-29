using NodaTime;
using NodaTime.Testing;
using PersonalCalendar.Application.Events;
using PersonalCalendar.Application.Tests.Fakes;
using PersonalCalendar.Application.Time;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Tests;

public class GetEventDetailsTests
{
    private readonly InMemoryEventRepository _repository = new();
    private readonly FakeClock _clock = new(Instant.FromUtc(2026, 9, 29, 15, 0));
    private readonly ZoneLookup _zones = new(DateTimeZoneProviders.Tzdb);

    private GetEventDetails UseCase => new(_repository, _zones);

    private async Task<Guid> CreateDentistAsync()
    {
        var result = await new CreateEvent(_repository, _zones, _clock).HandleAsync(new EventInput(
            "Dentist", "Main St Clinic", null, false, "America/Chicago",
            new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0), null, null));
        return Assert.IsType<UseCaseResult<EventDetails>.Ok>(result).Value.Id;
    }

    [Theory]
    [InlineData("America/Chicago", 9)]
    [InlineData("America/New_York", 10)]
    public async Task ReturnsDetails_WithTimesInRequestedZone(string zone, int expectedHour)
    {
        var id = await CreateDentistAsync();

        var result = await UseCase.HandleAsync(id, zone);

        var details = Assert.IsType<UseCaseResult<EventDetails>.Ok>(result).Value;
        Assert.Equal("Dentist", details.Title);
        Assert.Equal("Main St Clinic", details.Location);
        Assert.Null(details.Notes);
        Assert.Equal(zone, details.TimeZone);
        Assert.Equal(new LocalDateTime(2026, 10, 14, expectedHour, 0), details.Start!.Value.LocalDateTime);
        Assert.Equal(1, details.Version);
    }

    [Fact]
    public async Task UnknownId_IsNotFound()
    {
        var result = await UseCase.HandleAsync(Guid.NewGuid(), "America/Chicago");

        Assert.IsType<UseCaseResult<EventDetails>.NotFound>(result);
    }

    [Fact]
    public async Task UnknownZone_IsRejected()
    {
        var id = await CreateDentistAsync();

        var result = await UseCase.HandleAsync(id, "Mars/Base");

        var failed = Assert.IsType<UseCaseResult<EventDetails>.ValidationFailed>(result);
        Assert.Contains(failed.Errors.Errors, e => e.Code == ErrorCodes.TimeZoneUnknown);
    }
}
