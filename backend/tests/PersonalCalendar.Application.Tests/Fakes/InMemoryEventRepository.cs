using NodaTime;
using PersonalCalendar.Application.Abstractions;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Application.Tests.Fakes;

internal sealed class InMemoryEventRepository : IEventRepository
{
    private readonly Dictionary<EventId, CalendarEvent> _events = [];

    // Stored versions are tracked separately: use cases mutate the same instance before saving it.
    private readonly Dictionary<EventId, int> _storedVersions = [];

    public (Instant From, Instant To, LocalDate FromDate, LocalDate ToDate)? LastRangeQuery { get; private set; }

    public IReadOnlyCollection<CalendarEvent> All => _events.Values;

    public int StoredVersion(EventId id) => _storedVersions[id];

    public Task<CalendarEvent?> GetAsync(EventId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_events.GetValueOrDefault(id));

    public Task<IReadOnlyList<CalendarEvent>> ListOverlappingAsync(
        Instant from, Instant to, LocalDate fromDate, LocalDate toDate, CancellationToken cancellationToken = default)
    {
        LastRangeQuery = (from, to, fromDate, toDate);
        IReadOnlyList<CalendarEvent> matches = _events.Values.Where(e => e.Schedule switch
        {
            TimedSchedule t => t.Start < to && t.End > from,
            AllDaySchedule a => a.StartDate <= toDate && a.EndDate >= fromDate,
            _ => false,
        }).ToList();
        return Task.FromResult(matches);
    }

    public Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
    {
        _events.Add(calendarEvent.Id, calendarEvent);
        _storedVersions[calendarEvent.Id] = calendarEvent.Version;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CalendarEvent calendarEvent, int expectedVersion, CancellationToken cancellationToken = default)
    {
        if (!_storedVersions.TryGetValue(calendarEvent.Id, out var stored) || stored != expectedVersion)
        {
            throw new ConcurrencyConflictException("stale");
        }

        _events[calendarEvent.Id] = calendarEvent;
        _storedVersions[calendarEvent.Id] = calendarEvent.Version;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(EventId id, int expectedVersion, CancellationToken cancellationToken = default)
    {
        if (!_storedVersions.TryGetValue(id, out var stored)) return Task.FromResult(false);
        if (stored != expectedVersion) throw new ConcurrencyConflictException("stale");

        _events.Remove(id);
        _storedVersions.Remove(id);
        return Task.FromResult(true);
    }
}
