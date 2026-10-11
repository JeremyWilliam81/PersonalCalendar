using NodaTime;

namespace PersonalCalendar.Infrastructure.Persistence;

/// <summary>
/// One changed or deleted occurrence of a series (data-model.md, table <c>OccurrenceExceptions</c>).
/// A deleted row has no snapshot; a changed row holds the occurrence's full title and schedule.
/// </summary>
public sealed class OccurrenceExceptionRow
{
    public Guid SeriesId { get; set; }

    public LocalDate OriginalDate { get; set; }

    public bool IsDeleted { get; set; }

    public string? Title { get; set; }

    public string? Location { get; set; }

    public string? Notes { get; set; }

    public bool? IsAllDay { get; set; }

    public Instant? StartUtc { get; set; }

    public Instant? EndUtc { get; set; }

    public LocalDate? StartDate { get; set; }

    public LocalDate? EndDate { get; set; }
}
