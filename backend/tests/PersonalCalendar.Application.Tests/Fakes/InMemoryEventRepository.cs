using NodaTime;
using PersonalCalendar.Application.Abstractions;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Application.Tests.Fakes;

internal sealed class InMemoryEventRepository : IEventRepository
{
    private readonly Dictionary<EventId, CalendarEvent> _events = [];

    // Stored versions are tracked separately. Events are stored and returned as copies, so a use case that changes
    // an event and then fails to save leaves the stored one untouched, as with a real database (FR-027).
    private readonly Dictionary<EventId, int> _storedVersions = [];

    public (Instant From, Instant To, LocalDate FromDate, LocalDate ToDate)? LastRangeQuery { get; private set; }

    public IReadOnlyCollection<CalendarEvent> All => _events.Values;

    public int StoredVersion(EventId id) => _storedVersions[id];

    public Task<CalendarEvent?> GetAsync(EventId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_events.TryGetValue(id, out var stored) ? Copy(stored) : null);

    public Task<IReadOnlyList<CalendarEvent>> ListOverlappingAsync(
        Instant from, Instant to, LocalDate fromDate, LocalDate toDate, CancellationToken cancellationToken = default)
    {
        LastRangeQuery = (from, to, fromDate, toDate);
        // Same rule as EfEventRepository: series by their padded date range (research S7), one-time events by schedule.
        IReadOnlyList<CalendarEvent> matches = _events.Values.Where(e => e.Recurrence is not null
            ? e.SeriesFirstDate <= toDate.PlusDays(2) && (e.SeriesLastDate is null || e.SeriesLastDate >= fromDate.PlusDays(-2))
            : e.Schedule switch
            {
                TimedSchedule t => t.Start < to && t.End > from,
                AllDaySchedule a => a.StartDate <= toDate && a.EndDate >= fromDate,
                _ => false,
            }).Select(Copy).ToList();
        return Task.FromResult(matches);
    }

    public Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
    {
        _events.Add(calendarEvent.Id, Copy(calendarEvent));
        _storedVersions[calendarEvent.Id] = calendarEvent.Version;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(CalendarEvent calendarEvent, int expectedVersion, CancellationToken cancellationToken = default)
    {
        if (!_storedVersions.TryGetValue(calendarEvent.Id, out var stored) || stored != expectedVersion)
        {
            throw new ConcurrencyConflictException("stale");
        }

        _events[calendarEvent.Id] = Copy(calendarEvent);
        _storedVersions[calendarEvent.Id] = calendarEvent.Version;
        return Task.CompletedTask;
    }

    /// <summary>When set, the next <see cref="ReplaceAsync"/> throws before changing anything (FR-027).</summary>
    public bool FailNextReplace { get; set; }

    public int ReplaceCalls { get; private set; }

    public Task ReplaceAsync(
        IReadOnlyList<CalendarEvent> upserts,
        IReadOnlyList<EventId> deletes,
        EventId expectedId,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        ReplaceCalls++;
        if (FailNextReplace)
        {
            FailNextReplace = false;
            throw new InvalidOperationException("Simulated storage failure.");
        }

        if (!_storedVersions.TryGetValue(expectedId, out var stored) || stored != expectedVersion)
        {
            throw new ConcurrencyConflictException("stale");
        }

        foreach (var calendarEvent in upserts)
        {
            _events[calendarEvent.Id] = Copy(calendarEvent);
            _storedVersions[calendarEvent.Id] = calendarEvent.Version;
        }

        foreach (var id in deletes)
        {
            _events.Remove(id);
            _storedVersions.Remove(id);
        }

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

    private static CalendarEvent Copy(CalendarEvent e) =>
        CalendarEvent.Rehydrate(
            e.Id, e.Title, e.Location, e.Notes, e.Schedule, e.EntryTimeZone, e.CreatedUtc, e.UpdatedUtc, e.Version,
            e.Recurrence, e.Exceptions);
}
