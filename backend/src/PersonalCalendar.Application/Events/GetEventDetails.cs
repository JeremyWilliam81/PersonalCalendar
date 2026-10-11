using NodaTime;
using PersonalCalendar.Application.Abstractions;
using PersonalCalendar.Application.Time;
using PersonalCalendar.Domain.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Events;

public sealed class GetEventDetails(IEventRepository repository, ZoneLookup zones)
{
    /// <param name="occurrence">Required for a series and rejected for a one-time event (003 contracts/http-api.md).</param>
    public async Task<UseCaseResult<EventDetails>> HandleAsync(
        Guid id, string? timeZone, LocalDate? occurrence = null, CancellationToken cancellationToken = default)
    {
        var zone = zones.Find(timeZone);
        if (zone is null) return new UseCaseResult<EventDetails>.ValidationFailed(ZoneLookup.UnknownZone());

        var calendarEvent = await repository.GetAsync(new EventId(id), cancellationToken);
        if (calendarEvent is null) return new UseCaseResult<EventDetails>.NotFound();

        var problem = Occurrences.Check(calendarEvent, occurrence);
        return problem ?? new UseCaseResult<EventDetails>.Ok(EventMapping.ToDetails(calendarEvent, zone, occurrence));
    }
}

/// <summary>The presence rules for <c>occurrence</c> shared by get, update and delete.</summary>
internal static class Occurrences
{
    /// <returns>Null when the request may go ahead.</returns>
    public static UseCaseResult<EventDetails>? Check(CalendarEvent calendarEvent, LocalDate? occurrence) =>
        Check<EventDetails>(calendarEvent, occurrence);

    public static UseCaseResult<T>? Check<T>(CalendarEvent calendarEvent, LocalDate? occurrence)
    {
        if (calendarEvent.Recurrence is null)
        {
            return occurrence is null
                ? null
                : new UseCaseResult<T>.ValidationFailed(ValidationResult.Single("occurrence", ErrorCodes.OccurrenceInvalid));
        }

        if (occurrence is not { } date)
        {
            return new UseCaseResult<T>.ValidationFailed(ValidationResult.Single("occurrence", ErrorCodes.OccurrenceRequired));
        }

        // A date the rule does not produce, or one that was deleted, does not exist.
        var exists = Recurrence.Produces(calendarEvent, date)
            && !calendarEvent.Exceptions.Any(e => e.OriginalDate == date && e.IsDeleted);
        return exists ? null : new UseCaseResult<T>.NotFound();
    }
}
