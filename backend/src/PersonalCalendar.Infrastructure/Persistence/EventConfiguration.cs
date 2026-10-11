using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Infrastructure.Persistence;

internal sealed class EventConfiguration : IEntityTypeConfiguration<EventRow>
{
    public void Configure(EntityTypeBuilder<EventRow> builder)
    {
        builder.ToTable("Events", table =>
        {
            table.HasCheckConstraint(
                "CK_Events_Schedule",
                "(IsAllDay = 0 AND StartUtc IS NOT NULL AND EndUtc IS NOT NULL AND StartDate IS NULL AND EndDate IS NULL AND EndUtc > StartUtc) " +
                "OR (IsAllDay = 1 AND StartDate IS NOT NULL AND EndDate IS NOT NULL AND StartUtc IS NULL AND EndUtc IS NULL AND EndDate >= StartDate)");

            // A one-time event has no series columns; a series has its rule, zone and range, plus local times when timed.
            table.HasCheckConstraint(
                "CK_Events_Recurrence",
                "(RecurrenceRule IS NULL AND RecurrenceTimeZone IS NULL AND StartLocal IS NULL AND EndLocal IS NULL " +
                "AND SeriesFirstDate IS NULL AND SeriesLastDate IS NULL) " +
                "OR (RecurrenceRule IS NOT NULL AND RecurrenceTimeZone IS NOT NULL AND SeriesFirstDate IS NOT NULL " +
                "AND ((IsAllDay = 0 AND StartLocal IS NOT NULL AND EndLocal IS NOT NULL) OR (IsAllDay = 1 AND StartLocal IS NULL AND EndLocal IS NULL)) " +
                "AND (SeriesLastDate IS NULL OR SeriesLastDate >= SeriesFirstDate))");
        });

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Title).IsRequired().HasMaxLength(CalendarEvent.TitleMaxLength);
        builder.Property(e => e.Location).HasMaxLength(CalendarEvent.LocationMaxLength);
        builder.Property(e => e.Notes).HasMaxLength(CalendarEvent.NotesMaxLength);
        builder.Property(e => e.EntryTimeZone).IsRequired();
        builder.Property(e => e.Version).IsConcurrencyToken();

        builder.Property(e => e.StartUtc).HasConversion(NodaTimeConverters.Instant);
        builder.Property(e => e.EndUtc).HasConversion(NodaTimeConverters.Instant);
        builder.Property(e => e.CreatedUtc).HasConversion(NodaTimeConverters.Instant);
        builder.Property(e => e.UpdatedUtc).HasConversion(NodaTimeConverters.Instant);
        builder.Property(e => e.StartDate).HasConversion(NodaTimeConverters.LocalDate);
        builder.Property(e => e.EndDate).HasConversion(NodaTimeConverters.LocalDate);

        builder.HasIndex(e => new { e.StartUtc, e.EndUtc }).HasDatabaseName("IX_Events_StartUtc_EndUtc");
        builder.HasIndex(e => new { e.StartDate, e.EndDate }).HasDatabaseName("IX_Events_StartDate_EndDate");

        builder.Property(e => e.StartLocal).HasConversion(NodaTimeConverters.LocalDateTime);
        builder.Property(e => e.EndLocal).HasConversion(NodaTimeConverters.LocalDateTime);
        builder.Property(e => e.SeriesFirstDate).HasConversion(NodaTimeConverters.LocalDate);
        builder.Property(e => e.SeriesLastDate).HasConversion(NodaTimeConverters.LocalDate);
        builder.HasIndex(e => new { e.SeriesFirstDate, e.SeriesLastDate })
            .HasDatabaseName("IX_Events_Series")
            .HasFilter("RecurrenceRule IS NOT NULL");

        builder.HasMany(e => e.Exceptions)
            .WithOne()
            .HasForeignKey(x => x.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
