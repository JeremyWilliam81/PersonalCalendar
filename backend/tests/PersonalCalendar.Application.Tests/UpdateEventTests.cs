using NodaTime;
using NodaTime.Testing;
using PersonalCalendar.Application.Events;
using PersonalCalendar.Application.Tests.Fakes;
using PersonalCalendar.Application.Time;
using PersonalCalendar.Domain.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Tests;

public class UpdateEventTests
{
    private const string Chicago = "America/Chicago";
    private readonly InMemoryEventRepository _repository = new();
    private readonly FakeClock _clock = new(Instant.FromUtc(2026, 9, 29, 15, 0));
    private readonly ZoneLookup _zones = new(DateTimeZoneProviders.Tzdb);

    private UpdateEvent UseCase => new(_repository, _zones, _clock);

    private static EventInput Timed(LocalDateTime start, LocalDateTime end, int? version, string title = "Dentist", bool accept = false) =>
        new(title, null, null, false, Chicago, start, end, null, null, accept, version);

    private async Task<Guid> CreateAsync()
    {
        var result = await new CreateEvent(_repository, _zones, _clock)
            .HandleAsync(Timed(new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0), null));
        return Assert.IsType<UseCaseResult<EventDetails>.Ok>(result).Value.Id;
    }

    [Fact]
    public async Task ValidUpdate_SavesChanges_AndIncrementsVersion()
    {
        var id = await CreateAsync();

        var result = await UseCase.HandleAsync(id, Timed(new(2026, 10, 15, 13, 0), new(2026, 10, 15, 14, 0), 1, "Dentist (moved)"));

        var details = Assert.IsType<UseCaseResult<EventDetails>.Ok>(result).Value;
        Assert.Equal(2, details.Version);
        Assert.Equal("Dentist (moved)", details.Title);
        Assert.Equal(new LocalDateTime(2026, 10, 15, 13, 0), details.Start!.Value.LocalDateTime);
        Assert.Equal(2, _repository.StoredVersion(new EventId(id)));
    }

    [Fact]
    public async Task StaleVersion_IsConflict()
    {
        var id = await CreateAsync();
        await UseCase.HandleAsync(id, Timed(new(2026, 10, 15, 13, 0), new(2026, 10, 15, 14, 0), 1));

        var result = await UseCase.HandleAsync(id, Timed(new(2026, 10, 16, 13, 0), new(2026, 10, 16, 14, 0), 1));

        Assert.IsType<UseCaseResult<EventDetails>.Conflict>(result);
    }

    [Fact]
    public async Task UnknownId_IsNotFound()
    {
        var result = await UseCase.HandleAsync(Guid.NewGuid(), Timed(new(2026, 10, 15, 13, 0), new(2026, 10, 15, 14, 0), 1));

        Assert.IsType<UseCaseResult<EventDetails>.NotFound>(result);
    }

    [Fact]
    public async Task EndNotAfterStart_IsRejected_AndStoredEventIsUnchanged()
    {
        var id = await CreateAsync();

        var result = await UseCase.HandleAsync(id, Timed(new(2026, 10, 15, 13, 0), new(2026, 10, 15, 13, 0), 1));

        var failed = Assert.IsType<UseCaseResult<EventDetails>.ValidationFailed>(result);
        Assert.Contains(failed.Errors.Errors, e => e.Code == ErrorCodes.EndNotAfterStart);
        var stored = Assert.Single(_repository.All);
        Assert.Equal(1, stored.Version);
        Assert.Equal(Instant.FromUtc(2026, 10, 14, 14, 0), ((TimedSchedule)stored.Schedule).Start);
    }

    [Fact]
    public async Task TimeInDstGap_RequiresAdjustment()
    {
        var id = await CreateAsync();

        var result = await UseCase.HandleAsync(id, Timed(new(2027, 3, 14, 2, 30), new(2027, 3, 14, 4, 0), 1));

        Assert.IsType<UseCaseResult<EventDetails>.AdjustmentRequired>(result);
        Assert.Equal(1, Assert.Single(_repository.All).Version);
    }

    [Fact]
    public async Task MissingVersion_IsRejected()
    {
        var id = await CreateAsync();

        var result = await UseCase.HandleAsync(id, Timed(new(2026, 10, 15, 13, 0), new(2026, 10, 15, 14, 0), null));

        var failed = Assert.IsType<UseCaseResult<EventDetails>.ValidationFailed>(result);
        Assert.Contains(failed.Errors.Errors, e => e is { Field: "version", Code: "version.required" });
    }
}
