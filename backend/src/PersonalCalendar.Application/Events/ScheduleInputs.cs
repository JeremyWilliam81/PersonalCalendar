using NodaTime;
using PersonalCalendar.Domain.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Events;

/// <summary>Shared handling of the schedule part of an <see cref="EventInput"/> for create and update.</summary>
internal static class ScheduleInputs
{
    public static ResolveResult Resolve(EventInput input, DateTimeZone zone) =>
        ScheduleResolver.Resolve(
            input.IsAllDay, input.Start, input.End, input.StartDate, input.EndDate, zone, input.AcceptAdjustedTimes);

    /// <summary>
    /// Maps an unresolved schedule to a result. The text fields are validated too, so every error is reported
    /// at once (FR-003), and the user is never asked to confirm a DST adjustment for an otherwise invalid event.
    /// </summary>
    public static UseCaseResult<T> ToFailure<T>(ResolveResult resolved, EventInput input, DateTimeZone zone)
    {
        var errors = CalendarEvent.ValidateTextFields(input.Title, input.Location, input.Notes);
        switch (resolved)
        {
            case ResolveResult.AdjustmentRequired adjustment when errors.IsValid:
                return new UseCaseResult<T>.AdjustmentRequired(adjustment.AdjustedStart, adjustment.AdjustedEnd, zone.Id);
            case ResolveResult.AdjustmentRequired:
                return new UseCaseResult<T>.ValidationFailed(errors);
            case ResolveResult.Invalid invalid:
                errors.AddRange(invalid.Errors);
                return new UseCaseResult<T>.ValidationFailed(errors);
            default:
                throw new InvalidOperationException($"Unexpected resolve result {resolved.GetType().Name}.");
        }
    }
}
