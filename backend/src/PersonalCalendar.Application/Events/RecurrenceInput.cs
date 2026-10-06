using NodaTime;
using PersonalCalendar.Domain.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Events;

/// <summary>The repeat rule as the user set it (contracts/http-api.md, <c>recurrence</c>), already parsed by the API.</summary>
public sealed record RecurrenceInput(
    RepeatFrequency Frequency,
    int Interval,
    IReadOnlyList<IsoDayOfWeek> Weekdays,
    MonthlyPattern? Monthly,
    RepeatEnd End)
{
    /// <summary>Validates against the entered start date; error fields are prefixed <c>recurrence.</c>.</summary>
    public Validated<RepeatRule> ToRule(LocalDate startDate) =>
        RepeatRule.Create(Frequency, Interval, Weekdays, Monthly, End, startDate);
}
