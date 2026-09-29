using PersonalCalendar.Application.Abstractions;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Application.Events;

/// <summary>Deletes an event permanently (FR-011). The caller must hold the current version (research R8).</summary>
public sealed class DeleteEvent(IEventRepository repository)
{
    public async Task<UseCaseResult<Unit>> HandleAsync(Guid id, int version, CancellationToken cancellationToken = default)
    {
        try
        {
            var deleted = await repository.DeleteAsync(new EventId(id), version, cancellationToken);
            return deleted ? new UseCaseResult<Unit>.Ok(Unit.Value) : new UseCaseResult<Unit>.NotFound();
        }
        catch (ConcurrencyConflictException)
        {
            return new UseCaseResult<Unit>.Conflict();
        }
    }
}
