using PersonalCalendar.Application.Abstractions;
using PersonalCalendar.Application.Time;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Application.Events;

public sealed class GetEventDetails(IEventRepository repository, ZoneLookup zones)
{
    public async Task<UseCaseResult<EventDetails>> HandleAsync(
        Guid id, string? timeZone, CancellationToken cancellationToken = default)
    {
        var zone = zones.Find(timeZone);
        if (zone is null) return new UseCaseResult<EventDetails>.ValidationFailed(ZoneLookup.UnknownZone());

        var calendarEvent = await repository.GetAsync(new EventId(id), cancellationToken);
        return calendarEvent is null
            ? new UseCaseResult<EventDetails>.NotFound()
            : new UseCaseResult<EventDetails>.Ok(EventMapping.ToDetails(calendarEvent, zone));
    }
}
