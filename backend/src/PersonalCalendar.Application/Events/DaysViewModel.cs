using NodaTime;

namespace PersonalCalendar.Application.Events;

/// <summary>The day and week views (specs/002-calendar-views/contracts/http-api.md).</summary>
public sealed record DaysViewModel(
    string TimeZone,
    LocalDate Today,
    OffsetDateTime Now,
    IReadOnlyList<TimelineDayModel> Days,
    IReadOnlyList<AllDayBarModel> AllDayBars);

public sealed record TimelineDayModel(
    LocalDate Date,
    bool IsToday,
    OffsetDateTime DayStart,
    OffsetDateTime DayEnd,
    int LengthMinutes,
    IReadOnlyList<HourMarkModel> HourMarks,
    IReadOnlyList<EventSummary> AllDay,
    IReadOnlyList<TimedSegmentModel> Timed);

/// <param name="Label">Local wall time as HH:mm; the client formats it for the device locale.</param>
public sealed record HourMarkModel(int OffsetMinutes, string Label);

public sealed record TimedSegmentModel(
    EventSummary Event,
    int OffsetMinutes,
    int DurationMinutes,
    bool ContinuesBefore,
    bool ContinuesAfter,
    int Column,
    int ColumnCount);

public sealed record AllDayBarModel(
    EventSummary Event,
    int StartIndex,
    int Span,
    int Lane,
    bool ContinuesBefore,
    bool ContinuesAfter);
