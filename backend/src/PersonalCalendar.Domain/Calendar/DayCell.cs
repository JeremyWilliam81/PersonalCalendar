using NodaTime;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Domain.Calendar;

/// <summary>One day of the month grid with its events in display order.</summary>
public sealed record DayCell(LocalDate Date, bool InMonth, bool IsToday, IReadOnlyList<CalendarEvent> Events);
