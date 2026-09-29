using NodaTime;

namespace PersonalCalendar.Application.Events;

/// <summary>An event as shown in the requested zone. Timed events set Start/End; all-day events set StartDate/EndDate.</summary>
public sealed record EventDetails(
    Guid Id,
    string Title,
    string? Location,
    string? Notes,
    bool IsAllDay,
    string TimeZone,
    OffsetDateTime? Start,
    OffsetDateTime? End,
    LocalDate? StartDate,
    LocalDate? EndDate,
    int Version);
