using NodaTime;

namespace PersonalCalendar.Application.Events;

public sealed record MonthViewModel(int Year, int Month, string TimeZone, LocalDate Today, IReadOnlyList<WeekModel> Weeks);

public sealed record WeekModel(IReadOnlyList<DayModel> Days);

public sealed record DayModel(LocalDate Date, bool InMonth, bool IsToday, IReadOnlyList<EventSummary> Events);

public sealed record EventSummary(
    Guid Id,
    string Title,
    bool IsAllDay,
    OffsetDateTime? Start,
    OffsetDateTime? End,
    LocalDate? StartDate,
    LocalDate? EndDate);
