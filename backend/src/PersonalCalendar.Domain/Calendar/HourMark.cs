using NodaTime;

namespace PersonalCalendar.Domain.Calendar;

/// <summary>A whole-hour wall time that occurs on a day, placed by elapsed minutes from the day's start.</summary>
public sealed record HourMark(int OffsetMinutes, LocalTime Label);
