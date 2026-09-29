using NodaTime;

namespace PersonalCalendar.Infrastructure.Persistence;

/// <summary>Flat persistence shape of an event (data-model.md, table <c>Events</c>). Mapped to the domain in <see cref="EventRowMapper"/>.</summary>
public sealed class EventRow
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Location { get; set; }

    public string? Notes { get; set; }

    public bool IsAllDay { get; set; }

    public Instant? StartUtc { get; set; }

    public Instant? EndUtc { get; set; }

    public LocalDate? StartDate { get; set; }

    public LocalDate? EndDate { get; set; }

    public string EntryTimeZone { get; set; } = string.Empty;

    public Instant CreatedUtc { get; set; }

    public Instant UpdatedUtc { get; set; }

    public int Version { get; set; }
}
