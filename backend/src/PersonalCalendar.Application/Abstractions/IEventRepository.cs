using NodaTime;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Application.Abstractions;

public interface IEventRepository
{
    Task<CalendarEvent?> GetAsync(EventId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Timed events overlapping <c>[from, to)</c> plus all-day events overlapping <c>[fromDate, toDate]</c>.
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> ListOverlappingAsync(
        Instant from, Instant to, LocalDate fromDate, LocalDate toDate, CancellationToken cancellationToken = default);

    Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default);

    /// <exception cref="ConcurrencyConflictException">The stored version is not <paramref name="expectedVersion"/>.</exception>
    Task UpdateAsync(CalendarEvent calendarEvent, int expectedVersion, CancellationToken cancellationToken = default);

    /// <returns><c>false</c> if the event does not exist.</returns>
    /// <exception cref="ConcurrencyConflictException">The stored version is not <paramref name="expectedVersion"/>.</exception>
    Task<bool> DeleteAsync(EventId id, int expectedVersion, CancellationToken cancellationToken = default);
}
