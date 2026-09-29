using NodaTime;

namespace PersonalCalendar.Application.Events;

/// <summary>
/// User-entered event values. Local values are always paired with <see cref="TimeZone"/> (Date &amp; Time Standards).
/// </summary>
public sealed record EventInput(
    string? Title,
    string? Location,
    string? Notes,
    bool IsAllDay,
    string? TimeZone,
    LocalDateTime? Start,
    LocalDateTime? End,
    LocalDate? StartDate,
    LocalDate? EndDate,
    bool AcceptAdjustedTimes = false,
    int? Version = null);
