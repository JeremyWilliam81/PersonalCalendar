using Microsoft.EntityFrameworkCore;
using NodaTime;
using PersonalCalendar.Application.Abstractions;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Infrastructure.Persistence;

internal sealed class EfEventRepository(CalendarDbContext db) : IEventRepository
{
    public async Task<CalendarEvent?> GetAsync(EventId id, CancellationToken cancellationToken = default)
    {
        var row = await db.Events.AsNoTracking().Include(e => e.Exceptions).SingleOrDefaultAsync(e => e.Id == id.Value, cancellationToken);
        return row is null ? null : EventRowMapper.ToDomain(row);
    }

    public async Task<IReadOnlyList<CalendarEvent>> ListOverlappingAsync(
        Instant from, Instant to, LocalDate fromDate, LocalDate toDate, CancellationToken cancellationToken = default)
    {
        Instant? fromUtc = from;
        Instant? toUtc = to;
        LocalDate? fromDay = fromDate;
        LocalDate? toDay = toDate;

        // Series by their date range, padded two days because offsets span −12 to +14 hours (research S7), plus series with an
        // occurrence moved into the range from elsewhere.
        LocalDate? seriesTo = toDate.PlusDays(2);
        LocalDate? seriesFrom = fromDate.PlusDays(-2);

        var rows = await db.Events.AsNoTracking()
            .Include(e => e.Exceptions)
            .Where(e => (e.StartUtc < toUtc && e.EndUtc > fromUtc)
                || (e.StartDate <= toDay && e.EndDate >= fromDay)
                || (e.RecurrenceRule != null && e.SeriesFirstDate <= seriesTo && (e.SeriesLastDate == null || e.SeriesLastDate >= seriesFrom))
                || e.Exceptions.Any(x => (x.StartUtc < toUtc && x.EndUtc > fromUtc) || (x.StartDate <= toDay && x.EndDate >= fromDay)))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return rows.Select(EventRowMapper.ToDomain).ToList();
    }

    public async Task AddAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
    {
        db.Events.Add(EventRowMapper.ToRow(calendarEvent));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CalendarEvent calendarEvent, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var row = await db.Events.Include(e => e.Exceptions).SingleOrDefaultAsync(e => e.Id == calendarEvent.Id.Value, cancellationToken)
            ?? throw new ConcurrencyConflictException($"Event {calendarEvent.Id} was deleted.");

        // The UPDATE only matches the row if its version is still the one the caller read.
        db.Entry(row).Property(e => e.Version).OriginalValue = expectedVersion;
        EventRowMapper.CopyTo(calendarEvent, row);
        await SaveOrConflictAsync(calendarEvent.Id, cancellationToken);
    }

    public async Task ReplaceAsync(
        IReadOnlyList<CalendarEvent> upserts,
        IReadOnlyList<EventId> deletes,
        EventId expectedId,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var ids = upserts.Select(e => e.Id.Value).Concat(deletes.Select(d => d.Value)).Append(expectedId.Value).ToList();
        var rows = await db.Events.Include(e => e.Exceptions).Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, cancellationToken);

        if (!rows.TryGetValue(expectedId.Value, out var expected))
        {
            throw new ConcurrencyConflictException($"Event {expectedId} was deleted.");
        }

        // One SaveChanges is one transaction; the version check covers the whole change.
        db.Entry(expected).Property(e => e.Version).OriginalValue = expectedVersion;

        foreach (var calendarEvent in upserts)
        {
            if (rows.TryGetValue(calendarEvent.Id.Value, out var row)) EventRowMapper.CopyTo(calendarEvent, row);
            else db.Events.Add(EventRowMapper.ToRow(calendarEvent));
        }

        foreach (var id in deletes)
        {
            if (rows.TryGetValue(id.Value, out var row)) db.Events.Remove(row);
        }

        await SaveOrConflictAsync(expectedId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(EventId id, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var row = await db.Events.SingleOrDefaultAsync(e => e.Id == id.Value, cancellationToken);
        if (row is null) return false;

        db.Entry(row).Property(e => e.Version).OriginalValue = expectedVersion;
        db.Events.Remove(row);
        await SaveOrConflictAsync(id, cancellationToken);
        return true;
    }

    private async Task SaveOrConflictAsync(EventId id, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw new ConcurrencyConflictException($"Event {id} was changed elsewhere.");
        }
    }
}
