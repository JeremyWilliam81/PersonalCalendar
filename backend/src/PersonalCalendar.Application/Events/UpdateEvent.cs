using NodaTime;
using PersonalCalendar.Application.Abstractions;
using PersonalCalendar.Application.Time;
using PersonalCalendar.Domain.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Events;

public sealed class UpdateEvent(IEventRepository repository, ZoneLookup zones, IClock clock)
{
    public async Task<UseCaseResult<EventDetails>> HandleAsync(
        Guid id, EventInput input, CancellationToken cancellationToken = default)
    {
        if (input.Version is not { } expectedVersion)
        {
            return new UseCaseResult<EventDetails>.ValidationFailed(ValidationResult.Single("version", "version.required"));
        }

        var zone = zones.Find(input.TimeZone);
        if (zone is null) return new UseCaseResult<EventDetails>.ValidationFailed(ZoneLookup.UnknownZone());

        var calendarEvent = await repository.GetAsync(new EventId(id), cancellationToken);
        if (calendarEvent is null) return new UseCaseResult<EventDetails>.NotFound();
        if (calendarEvent.Version != expectedVersion) return new UseCaseResult<EventDetails>.Conflict();

        var resolved = ScheduleInputs.Resolve(input, zone);
        if (resolved is not ResolveResult.Resolved { Schedule: var schedule })
        {
            return ScheduleInputs.ToFailure<EventDetails>(resolved, input, zone);
        }

        var errors = calendarEvent.Update(input.Title, input.Location, input.Notes, schedule, zone, clock);
        if (!errors.IsValid) return new UseCaseResult<EventDetails>.ValidationFailed(errors);

        try
        {
            await repository.UpdateAsync(calendarEvent, expectedVersion, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return new UseCaseResult<EventDetails>.Conflict();
        }

        return new UseCaseResult<EventDetails>.Ok(EventMapping.ToDetails(calendarEvent, zone));
    }
}
