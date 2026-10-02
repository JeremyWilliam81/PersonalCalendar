using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Domain.Calendar;

/// <summary>An all-day event drawn as one bar across consecutive days of a range (research V5).</summary>
public sealed record AllDayBar(
    CalendarEvent Event,
    int StartIndex,
    int Span,
    int Lane,
    bool ContinuesBefore,
    bool ContinuesAfter);
