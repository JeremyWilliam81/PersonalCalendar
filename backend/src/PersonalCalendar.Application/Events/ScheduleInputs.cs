using NodaTime;
using PersonalCalendar.Domain.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Events;

/// <summary>Shared handling of the schedule and repeat parts of an <see cref="EventInput"/> for create and update.</summary>
internal static class ScheduleInputs
{
    public static ResolveResult Resolve(EventInput input, DateTimeZone zone) =>
        ScheduleResolver.Resolve(
            input.IsAllDay, input.Start, input.End, input.StartDate, input.EndDate, zone, input.AcceptAdjustedTimes);

    /// <summary>
    /// The validated rule, or null for "does not repeat". Without a start date there is nothing to validate the
    /// rule against; the missing start is reported by the schedule instead.
    /// </summary>
    public static Validated<RepeatRule>? Rule(EventInput input) =>
        input.Recurrence is { } recurrence && input.EnteredStartDate is { } startDate ? recurrence.ToRule(startDate) : null;

    /// <summary>
    /// Maps an unresolved schedule or an invalid rule to a result. The text fields and rule are validated too, so
    /// every error is reported at once (FR-003), and the user is never asked to confirm a DST adjustment for an
    /// otherwise invalid event.
    /// </summary>
    public static UseCaseResult<T> ToFailure<T>(ResolveResult resolved, EventInput input, DateTimeZone zone, Validated<RepeatRule>? rule)
    {
        var errors = CalendarEvent.ValidateTextFields(input.Title, input.Location, input.Notes);
        if (rule is { IsValid: false }) errors.AddRange(rule.Errors);

        switch (resolved)
        {
            case ResolveResult.AdjustmentRequired adjustment when errors.IsValid:
                return new UseCaseResult<T>.AdjustmentRequired(adjustment.AdjustedStart, adjustment.AdjustedEnd, zone.Id);
            case ResolveResult.AdjustmentRequired or ResolveResult.Resolved:
                return new UseCaseResult<T>.ValidationFailed(errors);
            case ResolveResult.Invalid invalid:
                errors.AddRange(invalid.Errors);
                return new UseCaseResult<T>.ValidationFailed(errors);
            default:
                throw new InvalidOperationException($"Unexpected resolve result {resolved.GetType().Name}.");
        }
    }

    /// <summary>True when the input can be saved: a resolved schedule and a valid rule (or none).</summary>
    public static bool IsReady(ResolveResult resolved, Validated<RepeatRule>? rule) =>
        resolved is ResolveResult.Resolved && rule is null or { IsValid: true };
}
