using NodaTime;
using PersonalCalendar.Application.Abstractions;
using PersonalCalendar.Application.Time;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Application.Events;

public sealed class CreateEvent(IEventRepository repository, ZoneLookup zones, IClock clock)
{
    public async Task<UseCaseResult<EventDetails>> HandleAsync(EventInput input, CancellationToken cancellationToken = default)
    {
        var zone = zones.Find(input.TimeZone);
        if (zone is null) return new UseCaseResult<EventDetails>.ValidationFailed(ZoneLookup.UnknownZone());

        var resolved = ScheduleInputs.Resolve(input, zone);
        var rule = ScheduleInputs.Rule(input);
        if (!ScheduleInputs.IsReady(resolved, rule))
        {
            return ScheduleInputs.ToFailure<EventDetails>(resolved, input, zone, rule);
        }

        var schedule = ((ResolveResult.Resolved)resolved).Schedule;
        var created = CalendarEvent.Create(input.Title, input.Location, input.Notes, schedule, zone, clock, rule?.Value);
        if (!created.IsValid) return new UseCaseResult<EventDetails>.ValidationFailed(created.Errors);

        await repository.AddAsync(created.Value!, cancellationToken);
        return new UseCaseResult<EventDetails>.Ok(EventMapping.ToDetails(created.Value!, zone));
    }
}
