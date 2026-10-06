using System.Globalization;
using NodaTime;
using NodaTime.Text;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Domain.Events;

public enum RepeatFrequency
{
    Daily,
    Weekly,
    Monthly,
    Yearly,
}

/// <summary>How a monthly series picks its day. Both take their day or weekday from the first occurrence.</summary>
public abstract record MonthlyPattern
{
    /// <summary>The first occurrence's day number; days 29–31 fall on the last day of shorter months (clarification Q1).</summary>
    public sealed record DayOfMonth : MonthlyPattern
    {
        public static readonly DayOfMonth Instance = new();
    }

    /// <summary>The first occurrence's weekday at position 1–4, or −1 for the last one in the month (clarification Q2).</summary>
    public sealed record WeekdayPosition(int Ordinal) : MonthlyPattern;
}

/// <summary>When a series stops.</summary>
public abstract record RepeatEnd
{
    public sealed record Never : RepeatEnd
    {
        public static readonly Never Instance = new();
    }

    /// <summary>No occurrence starts after this date (inclusive).</summary>
    public sealed record OnDate(LocalDate Date) : RepeatEnd;

    public sealed record AfterCount(int Count) : RepeatEnd;
}

/// <summary>
/// The supported subset of an RFC 5545 RRULE (research S1, S2). The weekday and day numbers of monthly and yearly
/// rules come from the series' first occurrence, so they are not stored twice.
/// </summary>
public sealed record RepeatRule
{
    public const int MaxInterval = 99;
    public const int MaxCount = 999;

    private RepeatRule(RepeatFrequency frequency, int interval, IReadOnlyList<IsoDayOfWeek> weekdays, MonthlyPattern? monthly, RepeatEnd end)
    {
        Frequency = frequency;
        Interval = interval;
        Weekdays = weekdays;
        Monthly = monthly;
        End = end;
    }

    public RepeatFrequency Frequency { get; }

    public int Interval { get; }

    /// <summary>Weekly only, in Sunday-first order; empty for other frequencies.</summary>
    public IReadOnlyList<IsoDayOfWeek> Weekdays { get; }

    /// <summary>Monthly only.</summary>
    public MonthlyPattern? Monthly { get; }

    public RepeatEnd End { get; }

    /// <param name="startDate">The start date as entered; the first occurrence is derived from it (FR-009).</param>
    public static Validated<RepeatRule> Create(
        RepeatFrequency frequency,
        int interval,
        IEnumerable<IsoDayOfWeek> weekdays,
        MonthlyPattern? monthly,
        RepeatEnd end,
        LocalDate startDate)
    {
        var errors = new ValidationResult();
        if (!Enum.IsDefined(frequency)) errors.Add("recurrence.frequency", ErrorCodes.RecurrenceFrequencyInvalid);
        if (interval is < 1 or > MaxInterval) errors.Add("recurrence.interval", ErrorCodes.RecurrenceIntervalOutOfRange);

        var days = frequency == RepeatFrequency.Weekly ? SundayFirst(weekdays) : [];
        if (frequency == RepeatFrequency.Weekly && days.Count == 0)
        {
            errors.Add("recurrence.weekdays", ErrorCodes.RecurrenceWeekdaysRequired);
        }

        var pattern = frequency == RepeatFrequency.Monthly ? monthly : null;
        if (frequency == RepeatFrequency.Monthly && !MatchesStart(pattern, startDate))
        {
            errors.Add("recurrence.monthly", ErrorCodes.RecurrenceMonthlyInvalid);
        }

        switch (end)
        {
            case RepeatEnd.AfterCount { Count: < 1 or > MaxCount }:
                errors.Add("recurrence.count", ErrorCodes.RecurrenceCountOutOfRange);
                break;
            case RepeatEnd.OnDate onDate when onDate.Date < FirstOccurrence(frequency, days, startDate):
                errors.Add("recurrence.until", ErrorCodes.RecurrenceUntilBeforeStart);
                break;
        }

        return errors.IsValid
            ? Validated<RepeatRule>.Success(new RepeatRule(frequency, interval, days, pattern, end))
            : Validated<RepeatRule>.Failure(errors);
    }

    /// <summary>The same rule with another end, e.g. when a series is split (research S6).</summary>
    public RepeatRule WithEnd(RepeatEnd end) => new(Frequency, Interval, Weekdays, Monthly, end);

    /// <summary>The start date itself, or for weekly rules the first chosen weekday on or after it (FR-009).</summary>
    public LocalDate FirstOccurrence(LocalDate startDate) => FirstOccurrence(Frequency, Weekdays, startDate);

    /// <summary>Formats the rule as RFC 5545 RRULE text (research S2, S3).</summary>
    public string ToRRule(LocalDate firstDate, DateTimeZone zone, bool isAllDay)
    {
        var parts = new List<string> { $"FREQ={Frequency.ToString().ToUpperInvariant()}", $"INTERVAL={Interval}" };

        switch (Frequency)
        {
            case RepeatFrequency.Weekly:
                parts.Add("BYDAY=" + string.Join(",", Weekdays.Select(DayCode)));
                parts.Add("WKST=SU");
                break;
            case RepeatFrequency.Monthly when Monthly is MonthlyPattern.WeekdayPosition position:
                parts.Add($"BYDAY={position.Ordinal}{DayCode(firstDate.DayOfWeek)}");
                break;
            case RepeatFrequency.Monthly when firstDate.Day <= 28:
                parts.Add($"BYMONTHDAY={firstDate.Day}");
                break;
            case RepeatFrequency.Monthly:
                // "Day 31, or the last day of shorter months" in standard RFC 5545 terms.
                parts.Add("BYMONTHDAY=" + string.Join(",", Enumerable.Range(28, firstDate.Day - 27)));
                parts.Add("BYSETPOS=-1");
                break;
            case RepeatFrequency.Yearly:
                parts.Add($"BYMONTH={firstDate.Month}");
                parts.Add($"BYMONTHDAY={firstDate.Day}");
                break;
        }

        switch (End)
        {
            case RepeatEnd.AfterCount count:
                parts.Add($"COUNT={count.Count}");
                break;
            case RepeatEnd.OnDate onDate when isAllDay:
                parts.Add("UNTIL=" + DatePattern.Format(onDate.Date));
                break;
            case RepeatEnd.OnDate onDate:
                // RFC 5545: with a zoned DTSTART, UNTIL is UTC. Use the last second of the end date in the series zone.
                var lastSecond = zone.AtStartOfDay(onDate.Date.PlusDays(1)).ToInstant() - Duration.FromSeconds(1);
                parts.Add("UNTIL=" + UtcPattern.Format(lastSecond));
                break;
        }

        return string.Join(";", parts);
    }

    /// <summary>Parses text written by <see cref="ToRRule"/>. Stored values only; anything else is a <see cref="FormatException"/>.</summary>
    public static RepeatRule ParseRRule(string rrule, LocalDate firstDate, DateTimeZone zone)
    {
        var parts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in rrule.Split(';'))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2 || !parts.TryAdd(pair[0], pair[1])) throw new FormatException($"Bad RRULE part '{part}'.");
        }

        string[] known = ["FREQ", "INTERVAL", "BYDAY", "WKST", "BYMONTHDAY", "BYSETPOS", "BYMONTH", "COUNT", "UNTIL"];
        if (parts.Keys.FirstOrDefault(k => !known.Contains(k)) is { } unknown) throw new FormatException($"Unsupported RRULE part '{unknown}'.");

        var frequency = Take(parts, "FREQ") switch
        {
            "DAILY" => RepeatFrequency.Daily,
            "WEEKLY" => RepeatFrequency.Weekly,
            "MONTHLY" => RepeatFrequency.Monthly,
            "YEARLY" => RepeatFrequency.Yearly,
            var other => throw new FormatException($"Unsupported FREQ '{other}'."),
        };
        var interval = ParseInt(Take(parts, "INTERVAL"));

        IReadOnlyList<IsoDayOfWeek> weekdays = [];
        MonthlyPattern? monthly = null;
        if (frequency == RepeatFrequency.Weekly)
        {
            weekdays = SundayFirst(Take(parts, "BYDAY").Split(',').Select(ParseDayCode));
        }
        else if (frequency == RepeatFrequency.Monthly)
        {
            monthly = parts.TryGetValue("BYDAY", out var byDay)
                ? new MonthlyPattern.WeekdayPosition(ParseInt(byDay[..^2]))
                : MonthlyPattern.DayOfMonth.Instance;
        }

        RepeatEnd end = RepeatEnd.Never.Instance;
        if (parts.TryGetValue("COUNT", out var count)) end = new RepeatEnd.AfterCount(ParseInt(count));
        if (parts.TryGetValue("UNTIL", out var until)) end = new RepeatEnd.OnDate(ParseUntil(until, zone));

        return new RepeatRule(frequency, interval, weekdays, monthly, end);
    }

    public bool Equals(RepeatRule? other) =>
        other is not null
        && Frequency == other.Frequency
        && Interval == other.Interval
        && Weekdays.SequenceEqual(other.Weekdays)
        && Equals(Monthly, other.Monthly)
        && Equals(End, other.End);

    public override int GetHashCode() =>
        HashCode.Combine(Frequency, Interval, string.Join(",", Weekdays), Monthly, End);

    private static readonly LocalDatePattern DatePattern = LocalDatePattern.CreateWithInvariantCulture("uuuuMMdd");
    private static readonly InstantPattern UtcPattern = InstantPattern.CreateWithInvariantCulture("uuuuMMdd'T'HHmmss'Z'");

    private static LocalDate FirstOccurrence(RepeatFrequency frequency, IReadOnlyList<IsoDayOfWeek> weekdays, LocalDate startDate)
    {
        if (frequency != RepeatFrequency.Weekly || weekdays.Count == 0) return startDate;

        var date = startDate;
        while (!weekdays.Contains(date.DayOfWeek)) date = date.PlusDays(1);
        return date;
    }

    /// <summary>Ordinals 1–4 must equal the start's position; −1 needs the start in the month's last 7 days.</summary>
    private static bool MatchesStart(MonthlyPattern? pattern, LocalDate start) => pattern switch
    {
        MonthlyPattern.DayOfMonth => true,
        MonthlyPattern.WeekdayPosition { Ordinal: -1 } => start.Day > CalendarSystem.Iso.GetDaysInMonth(start.Year, start.Month) - 7,
        MonthlyPattern.WeekdayPosition { Ordinal: >= 1 and <= 4 } position => (start.Day + 6) / 7 == position.Ordinal,
        _ => false,
    };

    private static IReadOnlyList<IsoDayOfWeek> SundayFirst(IEnumerable<IsoDayOfWeek> days) =>
        days.Where(d => d != IsoDayOfWeek.None).Distinct().OrderBy(d => (int)d % 7).ToList();

    private static string DayCode(IsoDayOfWeek day) => day.ToString()[..2].ToUpperInvariant();

    private static IsoDayOfWeek ParseDayCode(string code) =>
        Enum.GetValues<IsoDayOfWeek>().FirstOrDefault(d => d != IsoDayOfWeek.None && DayCode(d) == code) is var day and not IsoDayOfWeek.None
            ? day
            : throw new FormatException($"Bad weekday '{code}'.");

    private static LocalDate ParseUntil(string value, DateTimeZone zone)
    {
        if (value.Contains('T'))
        {
            var instant = UtcPattern.Parse(value);
            return instant.Success ? instant.Value.InZone(zone).Date : throw new FormatException($"Bad UNTIL '{value}'.");
        }

        var date = DatePattern.Parse(value);
        return date.Success ? date.Value : throw new FormatException($"Bad UNTIL '{value}'.");
    }

    private static string Take(Dictionary<string, string> parts, string key) =>
        parts.TryGetValue(key, out var value) ? value : throw new FormatException($"Missing RRULE part '{key}'.");

    private static int ParseInt(string value) =>
        int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new FormatException($"Bad number '{value}'.");
}
