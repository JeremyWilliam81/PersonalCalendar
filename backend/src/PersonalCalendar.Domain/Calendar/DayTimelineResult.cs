using NodaTime;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Domain.Calendar;

/// <summary>One day on a time scale measured from the day's real start, so DST days are 23 or 25 hours long.</summary>
public sealed record DayTimelineResult(
    LocalDate Date,
    Instant DayStart,
    Instant DayEnd,
    int LengthMinutes,
    IReadOnlyList<HourMark> HourMarks,
    IReadOnlyList<CalendarEvent> AllDay,
    IReadOnlyList<TimedSegment> Timed);
