using NodaTime;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Application.Abstractions;

public interface IEventRepository
{
    Task<CalendarEvent?> GetAsync(EventId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Timed events overlapping <c>[from, to)</c> plus all-day events overlapping <c>[fromDate, toDate]</c>, plus every
    /// series whose date range touches <c>[fromDate − 2, toDate + 2]</c> or that has an occurrence moved into the range
    /// (research S7). Series come with all their exceptions.
    /// </summary>
    Task<IReadOnlyList<CalendarEvent>> ListOverlappingAsync(
        Instant from, Instant to, LocalDate fromDate, LocalDate toDate, CancellationToken cancellationToken = default);

    Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default);

    /// <exception cref="ConcurrencyConflictException">The stored version is not <paramref name="expectedVersion"/>.</exception>
    Task UpdateAsync(CalendarEvent calendarEvent, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves several events and deletes others in one transaction: all or nothing (003 FR-027). Used when a series is
    /// split or turned into a one-time event. <paramref name="expectedId"/> must still have <paramref name="expectedVersion"/>.
    /// </summary>
    /// <exception cref="ConcurrencyConflictException">The stored version of <paramref name="expectedId"/> is not <paramref name="expectedVersion"/>.</exception>
    Task ReplaceAsync(
        IReadOnlyList<CalendarEvent> upserts,
        IReadOnlyList<EventId> deletes,
        EventId expectedId,
        int expectedVersion,
        CancellationToken cancellationToken = default);

    /// <returns><c>false</c> if the event does not exist.</returns>
    /// <exception cref="ConcurrencyConflictException">The stored version is not <paramref name="expectedVersion"/>.</exception>
    Task<bool> DeleteAsync(EventId id, int expectedVersion, CancellationToken cancellationToken = default);
}
