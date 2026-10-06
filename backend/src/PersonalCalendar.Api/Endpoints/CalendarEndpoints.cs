using PersonalCalendar.Api.Http;
using PersonalCalendar.Application.Events;

namespace PersonalCalendar.Api.Endpoints;

public static class CalendarEndpoints
{
    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/calendar/month", async (
            int? year, int? month, string? timeZone, GetMonthView useCase, CancellationToken cancellationToken) =>
        {
            var result = await useCase.HandleAsync(year, month, timeZone, cancellationToken);
            return ProblemResults.From(result, Results.Ok);
        });

        app.MapGet("/api/calendar/days", async (
            string? start, int? count, string? timeZone, GetDaysView useCase, CancellationToken cancellationToken) =>
        {
            var result = await useCase.HandleAsync(start, count, timeZone, cancellationToken);
            return ProblemResults.From(result, Results.Ok);
        });

        return app;
    }
}
