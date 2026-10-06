using NodaTime;
using PersonalCalendar.Api.Http;
using PersonalCalendar.Domain.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Api.Endpoints;

/// <summary>The <c>occurrence</c> and <c>scope</c> query parameters of PUT and DELETE (003 contracts/http-api.md).</summary>
internal readonly record struct SeriesTarget(LocalDate? Occurrence, EditScope? Scope)
{
    public static (SeriesTarget Target, ValidationResult Errors) Parse(string? occurrence, string? scope)
    {
        var errors = new ValidationResult();
        if (!LocalValues.TryParseLocalDate(occurrence, out var date)) errors.Add("occurrence", ErrorCodes.OccurrenceInvalid);

        EditScope? parsed = scope switch
        {
            null or "" => null,
            "this" => EditScope.This,
            "following" => EditScope.Following,
            "all" => EditScope.All,
            _ => null,
        };
        if (!string.IsNullOrEmpty(scope) && parsed is null) errors.Add("scope", ErrorCodes.ScopeInvalid);

        return (new SeriesTarget(date, parsed), errors);
    }
}
