using PersonalCalendar.Api.Http;
using PersonalCalendar.Application.Events;

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

        events.MapGet("/{id:guid}", async (Guid id, string? timeZone, GetEventDetails useCase, CancellationToken cancellationToken) =>
            ProblemResults.From(await useCase.HandleAsync(id, timeZone, cancellationToken), Results.Ok));

        events.MapPut("/{id:guid}", async (Guid id, EventRequest request, UpdateEvent useCase, CancellationToken cancellationToken) =>
        {
            var (input, errors) = request.ToInput();
            if (input is null) return ProblemResults.Validation(errors);

            return ProblemResults.From(await useCase.HandleAsync(id, input, cancellationToken), Results.Ok);
        });

        events.MapDelete("/{id:guid}", async (Guid id, int? version, DeleteEvent useCase, CancellationToken cancellationToken) =>
        {
            if (version is null) return ProblemResults.Validation("version", "version.required");

            return ProblemResults.From(await useCase.HandleAsync(id, version.Value, cancellationToken), _ => Results.NoContent());
        });

        return app;
    }
}
