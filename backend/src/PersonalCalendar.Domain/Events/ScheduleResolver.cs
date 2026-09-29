using NodaTime;
using NodaTime.TimeZones;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Domain.Events;

public abstract record ResolveResult
{
    public sealed record Resolved(EventSchedule Schedule) : ResolveResult;

    /// <summary>A time fell in a DST gap; the user must confirm the shifted times before saving (FR-017).</summary>
    public sealed record AdjustmentRequired(LocalDateTime AdjustedStart, LocalDateTime AdjustedEnd) : ResolveResult;

    public sealed record Invalid(ValidationResult Errors) : ResolveResult;
}

/// <summary>Turns user-entered local values into an <see cref="EventSchedule"/> (research R4).</summary>
public static class ScheduleResolver
{
    public static ResolveResult Resolve(
        bool isAllDay,
        LocalDateTime? start,
        LocalDateTime? end,
        LocalDate? startDate,
        LocalDate? endDate,
        DateTimeZone zone,
        bool acceptAdjustedTimes)
    {
        return isAllDay
            ? ResolveAllDay(startDate, endDate)
            : ResolveTimed(start, end, zone, acceptAdjustedTimes);
    }

    private static ResolveResult ResolveAllDay(LocalDate? startDate, LocalDate? endDate)
    {
        var errors = new ValidationResult();
        if (startDate is null) errors.Add("startDate", ErrorCodes.StartDateRequired);
        if (endDate is null) errors.Add("endDate", ErrorCodes.EndDateRequired);
        if (!errors.IsValid) return new ResolveResult.Invalid(errors);

        var schedule = AllDaySchedule.Create(startDate!.Value, endDate!.Value);
        return schedule.IsValid
            ? new ResolveResult.Resolved(schedule.Value!)
            : new ResolveResult.Invalid(schedule.Errors);
    }

    private static ResolveResult ResolveTimed(LocalDateTime? start, LocalDateTime? end, DateTimeZone zone, bool acceptAdjustedTimes)
    {
        var errors = new ValidationResult();
        if (start is null) errors.Add("start", ErrorCodes.StartRequired);
        if (end is null) errors.Add("end", ErrorCodes.EndRequired);
        if (!errors.IsValid) return new ResolveResult.Invalid(errors);

        // Gap: shift forward by the gap length. Overlap: take the earlier occurrence.
        var zonedStart = zone.ResolveLocal(start!.Value, Resolvers.LenientResolver);
        var zonedEnd = zone.ResolveLocal(end!.Value, Resolvers.LenientResolver);

        // Validity is checked on the adjusted instants before asking the user to confirm them.
        var schedule = TimedSchedule.Create(zonedStart.ToInstant(), zonedEnd.ToInstant());
        if (!schedule.IsValid) return new ResolveResult.Invalid(schedule.Errors);

        var fellInGap = IsInGap(zone, start.Value) || IsInGap(zone, end.Value);
        if (fellInGap && !acceptAdjustedTimes)
        {
            return new ResolveResult.AdjustmentRequired(zonedStart.LocalDateTime, zonedEnd.LocalDateTime);
        }

        return new ResolveResult.Resolved(schedule.Value!);
    }

    private static bool IsInGap(DateTimeZone zone, LocalDateTime local) => zone.MapLocal(local).Count == 0;
}
