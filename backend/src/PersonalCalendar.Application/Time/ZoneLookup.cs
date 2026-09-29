using NodaTime;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Time;

/// <summary>Resolves the IANA zone id sent by the browser (research R7).</summary>
public sealed class ZoneLookup(IDateTimeZoneProvider provider)
{
    public DateTimeZone? Find(string? timeZoneId) =>
        string.IsNullOrWhiteSpace(timeZoneId) ? null : provider.GetZoneOrNull(timeZoneId);

    public static ValidationResult UnknownZone() => ValidationResult.Single("timeZone", ErrorCodes.TimeZoneUnknown);
}
