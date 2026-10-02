using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Domain.Calendar;

/// <summary>The part of a timed event that falls on one day, with its overlap column (research V3, V4).</summary>
public sealed record TimedSegment(
    CalendarEvent Event,
    int OffsetMinutes,
    int DurationMinutes,
    bool ContinuesBefore,
    bool ContinuesAfter,
    int Column,
    int ColumnCount);
