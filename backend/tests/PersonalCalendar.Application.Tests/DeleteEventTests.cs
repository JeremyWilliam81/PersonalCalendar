using NodaTime;
using NodaTime.Testing;
using PersonalCalendar.Application.Events;
using PersonalCalendar.Application.Tests.Fakes;
using PersonalCalendar.Application.Time;

namespace PersonalCalendar.Application.Tests;

public class DeleteEventTests
{
    private readonly InMemoryEventRepository _repository = new();
    private readonly FakeClock _clock = new(Instant.FromUtc(2026, 9, 29, 15, 0));
    private readonly ZoneLookup _zones = new(DateTimeZoneProviders.Tzdb);

    private DeleteEvent UseCase => new(_repository, _clock);

    private async Task<Guid> CreateAsync()
    {
        var result = await new CreateEvent(_repository, _zones, _clock).HandleAsync(new EventInput(
            "Dentist", null, null, false, "America/Chicago", new(2026, 10, 14, 9, 0), new(2026, 10, 14, 10, 0), null, null));
        return Assert.IsType<UseCaseResult<EventDetails>.Ok>(result).Value.Id;
    }

    [Fact]
    public async Task CurrentVersion_RemovesEvent()
    {
        var id = await CreateAsync();

        var result = await UseCase.HandleAsync(id, 1);

        Assert.IsType<UseCaseResult<Unit>.Ok>(result);
        Assert.Empty(_repository.All);
    }

    [Fact]
    public async Task StaleVersion_IsConflict_AndKeepsEvent()
    {
        var id = await CreateAsync();

        var result = await UseCase.HandleAsync(id, 7);

        Assert.IsType<UseCaseResult<Unit>.Conflict>(result);
        Assert.Single(_repository.All);
    }

    [Fact]
    public async Task UnknownId_IsNotFound()
    {
        var result = await UseCase.HandleAsync(Guid.NewGuid(), 1);

        Assert.IsType<UseCaseResult<Unit>.NotFound>(result);
    }
}
