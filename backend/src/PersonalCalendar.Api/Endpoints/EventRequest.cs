using PersonalCalendar.Api.Http;
using PersonalCalendar.Application.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Api.Endpoints;

/// <summary>Request body for create/update (contracts/http-api.md, <c>EventInput</c>).</summary>
public sealed record EventRequest(
    string? Title,
    string? Location,
    string? Notes,
    bool IsAllDay,
    string? TimeZone,
    string? Start,
    string? End,
    string? StartDate,
    string? EndDate,
    bool AcceptAdjustedTimes,
    int? Version)
{
    /// <summary>Parses the local values. Fields that don't apply to the event type are ignored.</summary>
    public (EventInput? Input, ValidationResult Errors) ToInput()
    {
        var errors = new ValidationResult();
        NodaTime.LocalDateTime? start = null, end = null;
        NodaTime.LocalDate? startDate = null, endDate = null;

        if (IsAllDay)
        {
            if (!LocalValues.TryParseLocalDate(StartDate, out startDate)) errors.Add("startDate", "startDate.invalid");
            if (!LocalValues.TryParseLocalDate(EndDate, out endDate)) errors.Add("endDate", "endDate.invalid");
        }
        else
        {
            if (!LocalValues.TryParseLocalDateTime(Start, out start)) errors.Add("start", "start.invalid");
            if (!LocalValues.TryParseLocalDateTime(End, out end)) errors.Add("end", "end.invalid");
        }

        if (!errors.IsValid) return (null, errors);

        return (new EventInput(
            Title, Location, Notes, IsAllDay, TimeZone, start, end, startDate, endDate, AcceptAdjustedTimes, Version), errors);
    }
}
