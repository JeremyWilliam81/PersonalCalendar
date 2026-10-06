using NodaTime;
using NodaTime.Text;

namespace PersonalCalendar.Domain.Tests;

/// <summary>Zones and short value parsers shared by the recurrence tests (Constitution I hazard zones).</summary>
internal static class TestZones
{
    public static readonly DateTimeZone Chicago = DateTimeZoneProviders.Tzdb["America/Chicago"];
    public static readonly DateTimeZone NewYork = DateTimeZoneProviders.Tzdb["America/New_York"];
    public static readonly DateTimeZone Kolkata = DateTimeZoneProviders.Tzdb["Asia/Kolkata"];
    public static readonly DateTimeZone Adelaide = DateTimeZoneProviders.Tzdb["Australia/Adelaide"];
    public static readonly DateTimeZone LordHowe = DateTimeZoneProviders.Tzdb["Australia/Lord_Howe"];

    /// <summary>Parses <c>yyyy-MM-dd</c>.</summary>
    public static LocalDate D(string value) => LocalDatePattern.Iso.Parse(value).Value;

    /// <summary>Parses <c>yyyy-MM-ddTHH:mm</c>.</summary>
    public static LocalDateTime LDT(string value) => LocalDateTimePattern.GeneralIso.Parse(value + ":00").Value;
}
