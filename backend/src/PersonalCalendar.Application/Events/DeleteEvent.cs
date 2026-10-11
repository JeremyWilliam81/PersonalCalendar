using NodaTime;
using PersonalCalendar.Application.Abstractions;
using PersonalCalendar.Domain.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Events;

/// <summary>
/// Deletes an event permanently (FR-011). The caller must hold the current version (research R8). For a series,
/// <paramref name="occurrence"/> and <paramref name="scope"/> pick what is deleted (003 FR-023, FR-024).
/// </summary>
public sealed class DeleteEvent(IEventRepository repository, IClock clock)
{
    public async Task<UseCaseResult<Unit>> HandleAsync(
        Guid id, int version, LocalDate? occurrence = null, EditScope? scope = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var calendarEvent = await repository.GetAsync(new EventId(id), cancellationToken);
            if (calendarEvent is null) return new UseCaseResult<Unit>.NotFound();

            if (calendarEvent.Recurrence is null && scope is not null)
            {
                return new UseCaseResult<Unit>.ValidationFailed(ValidationResult.Single("scope", ErrorCodes.ScopeInvalid));
            }

            if (Occurrences.Check<Unit>(calendarEvent, occurrence) is { } problem) return problem;
            if (calendarEvent.Recurrence is null)
            {
                var deleted = await repository.DeleteAsync(calendarEvent.Id, version, cancellationToken);
                return deleted ? new UseCaseResult<Unit>.Ok(Unit.Value) : new UseCaseResult<Unit>.NotFound();
            }
            if (scope is not { } chosen)
            {
                return new UseCaseResult<Unit>.ValidationFailed(ValidationResult.Single("scope", ErrorCodes.ScopeRequired));
            }

            if (calendarEvent.Version != version) return new UseCaseResult<Unit>.Conflict();

            var change = calendarEvent.ApplyDelete(chosen, occurrence!.Value, clock);
            if (change.DeleteOriginal) await repository.DeleteAsync(calendarEvent.Id, version, cancellationToken);
            else await repository.UpdateAsync(calendarEvent, version, cancellationToken);

            return new UseCaseResult<Unit>.Ok(Unit.Value);
        }
        catch (ConcurrencyConflictException)
        {
            return new UseCaseResult<Unit>.Conflict();
        }
    }
}
