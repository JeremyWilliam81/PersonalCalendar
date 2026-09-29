using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Infrastructure.Persistence;

internal sealed class EventConfiguration : IEntityTypeConfiguration<EventRow>
{
    public void Configure(EntityTypeBuilder<EventRow> builder)
    {
        builder.ToTable("Events", table => table.HasCheckConstraint(
            "CK_Events_Schedule",
            "(IsAllDay = 0 AND StartUtc IS NOT NULL AND EndUtc IS NOT NULL AND StartDate IS NULL AND EndDate IS NULL AND EndUtc > StartUtc) " +
            "OR (IsAllDay = 1 AND StartDate IS NOT NULL AND EndDate IS NOT NULL AND StartUtc IS NULL AND EndUtc IS NULL AND EndDate >= StartDate)"));

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
    }
}
