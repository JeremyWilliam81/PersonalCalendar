using Microsoft.EntityFrameworkCore;

namespace PersonalCalendar.Infrastructure.Persistence;

public sealed class CalendarDbContext(DbContextOptions<CalendarDbContext> options) : DbContext(options)
{
    public DbSet<EventRow> Events => Set<EventRow>();

    public DbSet<OccurrenceExceptionRow> OccurrenceExceptions => Set<OccurrenceExceptionRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new EventConfiguration());
        modelBuilder.ApplyConfiguration(new OccurrenceExceptionConfiguration());
    }
}
