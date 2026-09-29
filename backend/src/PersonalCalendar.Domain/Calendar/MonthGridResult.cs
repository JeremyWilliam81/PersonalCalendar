namespace PersonalCalendar.Domain.Calendar;

/// <summary>A month grid: 4–6 Sunday-to-Saturday weeks of 7 days.</summary>
public sealed record MonthGridResult(int Year, int Month, IReadOnlyList<IReadOnlyList<DayCell>> Weeks);
