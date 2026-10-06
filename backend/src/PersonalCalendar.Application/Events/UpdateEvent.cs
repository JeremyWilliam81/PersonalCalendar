using NodaTime;
using PersonalCalendar.Application.Abstractions;
using PersonalCalendar.Application.Time;
using PersonalCalendar.Domain.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Events;

public sealed class UpdateEvent(IEventRepository repository, ZoneLookup zones, IClock clock)
{
    /// <param name="occurrence">For a series: the occurrence that was edited (its original date). Rejected for a one-time event.</param>
    /// <param name="scope">For a series: where the change applies (FR-015). Rejected for a one-time event.</param>
    public async Task<UseCaseResult<EventDetails>> HandleAsync(
        Guid id,
        EventInput input,
        LocalDate? occurrence = null,
        EditScope? scope = null,
        CancellationToken cancellationToken = default)
    {
        if (input.Version is not { } expectedVersion)
        {
            return new UseCaseResult<EventDetails>.ValidationFailed(ValidationResult.Single("version", "version.required"));
        }

        var zone = zones.Find(input.TimeZone);
        if (zone is null) return new UseCaseResult<EventDetails>.ValidationFailed(ZoneLookup.UnknownZone());

        var calendarEvent = await repository.GetAsync(new EventId(id), cancellationToken);
        if (calendarEvent is null) return new UseCaseResult<EventDetails>.NotFound();

        if (calendarEvent.Recurrence is null && scope is not null)
        {
            return new UseCaseResult<EventDetails>.ValidationFailed(ValidationResult.Single("scope", ErrorCodes.ScopeInvalid));
        }

        if (Occurrences.Check(calendarEvent, occurrence) is { } problem) return problem;
        if (calendarEvent.Recurrence is not null && scope is null)
        {
            return new UseCaseResult<EventDetails>.ValidationFailed(ValidationResult.Single("scope", ErrorCodes.ScopeRequired));
        }

        if (calendarEvent.Version != expectedVersion) return new UseCaseResult<EventDetails>.Conflict();

        var resolved = ScheduleInputs.Resolve(input, zone);
        var rule = ScheduleInputs.Rule(input);
        if (!ScheduleInputs.IsReady(resolved, rule))
        {
            return ScheduleInputs.ToFailure<EventDetails>(resolved, input, zone, rule);
        }

        var schedule = ((ResolveResult.Resolved)resolved).Schedule;
        return calendarEvent.Recurrence is null
            ? await UpdateOneTimeAsync(calendarEvent, input, schedule, rule?.Value, zone, expectedVersion, cancellationToken)
            : await UpdateSeriesAsync(calendarEvent, input, schedule, rule?.Value, zone, occurrence!.Value, scope!.Value, expectedVersion, cancellationToken);
    }

    private async Task<UseCaseResult<EventDetails>> UpdateOneTimeAsync(
        CalendarEvent calendarEvent, EventInput input, EventSchedule schedule, RepeatRule? rule, DateTimeZone zone, int expectedVersion,
        CancellationToken cancellationToken)
    {
        var errors = calendarEvent.Update(input.Title, input.Location, input.Notes, schedule, zone, clock, rule);
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

    private async Task<UseCaseResult<EventDetails>> UpdateSeriesAsync(
        CalendarEvent series, EventInput input, EventSchedule schedule, RepeatRule? rule, DateTimeZone zone,
        LocalDate occurrence, EditScope scope, int expectedVersion, CancellationToken cancellationToken)
    {
        var applied = series.ApplyEdit(scope, occurrence, new OccurrenceInput(input.Title, input.Location, input.Notes, schedule, rule), zone, clock);
        if (!applied.IsValid) return new UseCaseResult<EventDetails>.ValidationFailed(applied.Errors);

        var change = applied.Value!;
        try
        {
            if (change.Created is { } created)
            {
                await repository.ReplaceAsync([series, created], [], series.Id, expectedVersion, cancellationToken);
            }
            else
            {
                await repository.UpdateAsync(series, expectedVersion, cancellationToken);
            }
        }
        catch (ConcurrencyConflictException)
        {
            return new UseCaseResult<EventDetails>.Conflict();
        }

        var shown = change.Created ?? series;
        return new UseCaseResult<EventDetails>.Ok(EventMapping.ToDetails(shown, zone, shown.Recurrence is null ? null : change.ShowDate));
    }
}
