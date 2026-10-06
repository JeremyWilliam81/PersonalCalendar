using NodaTime;
using PersonalCalendar.Api.Http;
using PersonalCalendar.Application.Events;
using PersonalCalendar.Domain.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Api.Endpoints;

/// <summary>The <c>recurrence</c> object (specs/003-recurring-events/contracts/http-api.md).</summary>
public sealed record RecurrenceRequest(
    string? Frequency,
    int? Interval,
    string[]? Weekdays,
    MonthlyRequest? Monthly,
    RepeatEndRequest? End)
{
    /// <summary>Parses the names and dates; range rules are left to the domain. Unknown values are added to <paramref name="errors"/>.</summary>
    public RecurrenceInput? ToInput(ValidationResult errors)
    {
        var before = errors.Errors.Count;

        if (!Enum.TryParse<RepeatFrequency>(Frequency, ignoreCase: true, out var frequency) || !Enum.IsDefined(frequency) || int.TryParse(Frequency, out _))
        {
            errors.Add("recurrence.frequency", ErrorCodes.RecurrenceFrequencyInvalid);
        }

        var weekdays = new List<IsoDayOfWeek>();
        foreach (var name in Weekdays ?? [])
        {
            if (Enum.TryParse<IsoDayOfWeek>(name, ignoreCase: true, out var day) && day != IsoDayOfWeek.None && !int.TryParse(name, out _)) weekdays.Add(day);
            else errors.Add("recurrence.weekdays", "recurrence.weekdays.invalid");
        }

        MonthlyPattern? monthly = Monthly?.Type switch
        {
            null => null,
            "dayOfMonth" => MonthlyPattern.DayOfMonth.Instance,
            "weekdayPosition" when Monthly.Ordinal is { } ordinal => new MonthlyPattern.WeekdayPosition(ordinal),
            _ => Invalid<MonthlyPattern>(errors, "recurrence.monthly", ErrorCodes.RecurrenceMonthlyInvalid),
        };

        RepeatEnd end = RepeatEnd.Never.Instance;
        switch (End?.Type)
        {
            case null or "never":
                break;
            case "count" when End.Count is { } count:
                end = new RepeatEnd.AfterCount(count);
                break;
            case "until" when LocalValues.TryParseLocalDate(End.Until, out var until) && until is { } date:
                end = new RepeatEnd.OnDate(date);
                break;
            case "count":
                errors.Add("recurrence.count", ErrorCodes.RecurrenceCountOutOfRange);
                break;
            case "until":
                errors.Add("recurrence.until", "recurrence.until.invalid");
                break;
            default:
                errors.Add("recurrence.end", "recurrence.end.invalid");
                break;
        }

        return errors.Errors.Count == before
            ? new RecurrenceInput(frequency, Interval ?? 1, weekdays, monthly, end)
            : null;
    }

    private static T? Invalid<T>(ValidationResult errors, string field, string code) where T : class
    {
        errors.Add(field, code);
        return null;
    }
}

public sealed record MonthlyRequest(string? Type, int? Ordinal);

public sealed record RepeatEndRequest(string? Type, string? Until, int? Count);
