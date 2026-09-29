using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;
using NodaTime.Text;

namespace PersonalCalendar.Infrastructure.Persistence;

/// <summary>
/// Fixed-width text formats so SQLite's text comparison orders values correctly (research R3).
/// </summary>
public static class NodaTimeConverters
{
    public static readonly InstantPattern InstantPattern =
        InstantPattern.CreateWithInvariantCulture("uuuu'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");

    public static readonly LocalDatePattern DatePattern = LocalDatePattern.Iso;

    public static readonly ValueConverter<Instant, string> Instant = new(
        value => InstantPattern.Format(value),
        text => InstantPattern.Parse(text).Value);

    public static readonly ValueConverter<LocalDate, string> LocalDate = new(
        value => DatePattern.Format(value),
        text => DatePattern.Parse(text).Value);
}
