using Microsoft.EntityFrameworkCore;
using NodaTime;
using PersonalCalendar.Application.Abstractions;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Infrastructure.Persistence;

internal sealed class EfEventRepository(CalendarDbContext db) : IEventRepository
{
    public async Task<CalendarEvent?> GetAsync(EventId id, CancellationToken cancellationToken = default)
    {
        var row = await db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id.Value, cancellationToken);
        return row is null ? null : EventRowMapper.ToDomain(row);
    }

    public async Task<IReadOnlyList<CalendarEvent>> ListOverlappingAsync(
        Instant from, Instant to, LocalDate fromDate, LocalDate toDate, CancellationToken cancellationToken = default)
    {
        Instant? fromUtc = from;
        Instant? toUtc = to;
        LocalDate? fromDay = fromDate;
        LocalDate? toDay = toDate;

        var rows = await db.Events.AsNoTracking()
            .Where(e => (e.StartUtc < toUtc && e.EndUtc > fromUtc) || (e.StartDate <= toDay && e.EndDate >= fromDay))
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
        var row = await db.Events.SingleOrDefaultAsync(e => e.Id == calendarEvent.Id.Value, cancellationToken)
            ?? throw new ConcurrencyConflictException($"Event {calendarEvent.Id} was deleted.");

        // The UPDATE only matches the row if its version is still the one the caller read.
        db.Entry(row).Property(e => e.Version).OriginalValue = expectedVersion;
        EventRowMapper.CopyTo(calendarEvent, row);
        await SaveOrConflictAsync(calendarEvent.Id, cancellationToken);
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
