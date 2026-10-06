using PersonalCalendar.Api.Http;
using PersonalCalendar.Application.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Api.Endpoints;

public static class EventEndpoints
{
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        var events = app.MapGroup("/api/events");

        events.MapPost("", async (EventRequest request, CreateEvent useCase, CancellationToken cancellationToken) =>
        {
            var (input, errors) = request.ToInput();
            if (input is null) return ProblemResults.Validation(errors);

            var result = await useCase.HandleAsync(input, cancellationToken);
            return ProblemResults.From(result, details => Results.Created($"/api/events/{details.Id}", details));
        });

        events.MapGet("/{id:guid}", async (
            Guid id, string? timeZone, string? occurrence, GetEventDetails useCase, CancellationToken cancellationToken) =>
        {
            if (!LocalValues.TryParseLocalDate(occurrence, out var occurrenceDate))
            {
                return ProblemResults.Validation("occurrence", ErrorCodes.OccurrenceInvalid);
            }

            return ProblemResults.From(await useCase.HandleAsync(id, timeZone, occurrenceDate, cancellationToken), Results.Ok);
        });

        events.MapPut("/{id:guid}", async (
            Guid id, string? occurrence, string? scope, EventRequest request, UpdateEvent useCase, CancellationToken cancellationToken) =>
        {
            var (target, targetErrors) = SeriesTarget.Parse(occurrence, scope);
            var (input, errors) = request.ToInput();
            errors.AddRange(targetErrors);
            if (input is null || !errors.IsValid) return ProblemResults.Validation(errors);

            return ProblemResults.From(await useCase.HandleAsync(id, input, target.Occurrence, target.Scope, cancellationToken), Results.Ok);
        });

        events.MapDelete("/{id:guid}", async (
            Guid id, int? version, string? occurrence, string? scope, DeleteEvent useCase, CancellationToken cancellationToken) =>
        {
            var (target, errors) = SeriesTarget.Parse(occurrence, scope);
            if (version is null) errors.Add("version", "version.required");
            if (!errors.IsValid) return ProblemResults.Validation(errors);

            return ProblemResults.From(
                await useCase.HandleAsync(id, version!.Value, target.Occurrence, target.Scope, cancellationToken),
                _ => Results.NoContent());
        });

        return app;
    }
}
