using NodaTime;
using NodaTime.Text;

namespace PersonalCalendar.Api.Http;

/// <summary>Parses and formats the local wall-clock values used in request/response payloads.</summary>
public static class LocalValues
{
    private static readonly LocalDateTimePattern MinutePattern =
        LocalDateTimePattern.CreateWithInvariantCulture("uuuu'-'MM'-'dd'T'HH':'mm");

    public static string FormatLocalDateTime(LocalDateTime value) => MinutePattern.Format(value);

    /// <returns>false when <paramref name="text"/> is present but not a valid local date-time.</returns>
    public static bool TryParseLocalDateTime(string? text, out LocalDateTime? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;

        var result = MinutePattern.Parse(text);
        if (!result.Success) result = LocalDateTimePattern.ExtendedIso.Parse(text);
        if (!result.Success) return false;

        value = result.Value;
        return true;
    }

    /// <returns>false when <paramref name="text"/> is present but not a valid date.</returns>
    public static bool TryParseLocalDate(string? text, out LocalDate? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;

        var result = LocalDatePattern.Iso.Parse(text);
        if (!result.Success) return false;

        value = result.Value;
        return true;
    }
}
