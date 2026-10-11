using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Infrastructure.Persistence;

internal sealed class OccurrenceExceptionConfiguration : IEntityTypeConfiguration<OccurrenceExceptionRow>
{
    public void Configure(EntityTypeBuilder<OccurrenceExceptionRow> builder)
    {
        // Deleted: no snapshot at all. Changed: a title plus a valid timed or all-day schedule (001's CK_Events_Schedule).
        builder.ToTable("OccurrenceExceptions", table => table.HasCheckConstraint(
            "CK_OccurrenceExceptions_Shape",
            "(IsDeleted = 1 AND Title IS NULL AND Location IS NULL AND Notes IS NULL AND IsAllDay IS NULL " +
            "AND StartUtc IS NULL AND EndUtc IS NULL AND StartDate IS NULL AND EndDate IS NULL) " +
            "OR (IsDeleted = 0 AND Title IS NOT NULL AND (" +
            "(IsAllDay = 0 AND StartUtc IS NOT NULL AND EndUtc IS NOT NULL AND StartDate IS NULL AND EndDate IS NULL AND EndUtc > StartUtc) " +
            "OR (IsAllDay = 1 AND StartDate IS NOT NULL AND EndDate IS NOT NULL AND StartUtc IS NULL AND EndUtc IS NULL AND EndDate >= StartDate)))"));

        builder.HasKey(e => new { e.SeriesId, e.OriginalDate });
        builder.Property(e => e.Title).HasMaxLength(CalendarEvent.TitleMaxLength);
        builder.Property(e => e.Location).HasMaxLength(CalendarEvent.LocationMaxLength);
        builder.Property(e => e.Notes).HasMaxLength(CalendarEvent.NotesMaxLength);

        builder.Property(e => e.OriginalDate).HasConversion(NodaTimeConverters.LocalDate);
        builder.Property(e => e.StartUtc).HasConversion(NodaTimeConverters.Instant);
        builder.Property(e => e.EndUtc).HasConversion(NodaTimeConverters.Instant);
        builder.Property(e => e.StartDate).HasConversion(NodaTimeConverters.LocalDate);
        builder.Property(e => e.EndDate).HasConversion(NodaTimeConverters.LocalDate);

        // Moved occurrences can fall outside their series' range, so they are found by their own schedule (research S7).
        builder.HasIndex(e => new { e.StartUtc, e.EndUtc }).HasDatabaseName("IX_OccurrenceExceptions_StartUtc_EndUtc");
        builder.HasIndex(e => new { e.StartDate, e.EndDate }).HasDatabaseName("IX_OccurrenceExceptions_StartDate_EndDate");
    }
}
