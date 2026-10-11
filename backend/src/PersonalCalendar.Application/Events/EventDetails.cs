using System.Text.Json.Serialization;
using NodaTime;

namespace PersonalCalendar.Application.Events;

/// <summary>
/// An event as shown in the requested zone. Timed events set Start/End; all-day events set StartDate/EndDate.
/// For a series these are one occurrence's values, and the series fields are set (003 contracts/http-api.md).
/// </summary>
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
    int Version,
    RecurrenceModel? Recurrence = null,
    LocalDate? OccurrenceDate = null,
    OffsetDateTime? SeriesStart = null,
    LocalDate? SeriesStartDate = null,
    bool IsException = false,
    int ExceptionCount = 0);

/// <summary>The <c>recurrence</c> object of the HTTP contract.</summary>
public sealed record RecurrenceModel(
    string Frequency,
    int Interval,
    IReadOnlyList<string> Weekdays,
    MonthlyModel? Monthly,
    RepeatEndModel End,
    string TimeZone);

/// <param name="Type"><c>dayOfMonth</c> or <c>weekdayPosition</c>.</param>
public sealed record MonthlyModel(
    string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Ordinal);

/// <param name="Type"><c>never</c>, <c>until</c> or <c>count</c>.</param>
public sealed record RepeatEndModel(
    string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LocalDate? Until,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Count);
