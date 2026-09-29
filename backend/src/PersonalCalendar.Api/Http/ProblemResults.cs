using Microsoft.AspNetCore.Diagnostics;
using PersonalCalendar.Application.Events;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Api.Http;

/// <summary>Maps use-case outcomes to RFC 9457 problem responses (contracts/http-api.md).</summary>
public static class ProblemResults
{
    public static IResult From<T>(UseCaseResult<T> result, Func<T, IResult> onOk) => result switch
    {
        UseCaseResult<T>.Ok ok => onOk(ok.Value),
        UseCaseResult<T>.ValidationFailed failed => Validation(failed.Errors),
        UseCaseResult<T>.AdjustmentRequired adjustment => Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            type: "dst-adjustment-required",
            title: "The time falls in a daylight saving time gap.",
            extensions: new Dictionary<string, object?>
            {
                ["adjustedStart"] = LocalValues.FormatLocalDateTime(adjustment.AdjustedStart),
                ["adjustedEnd"] = LocalValues.FormatLocalDateTime(adjustment.AdjustedEnd),
                ["timeZone"] = adjustment.TimeZone,
            }),
        UseCaseResult<T>.NotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound, type: "not-found", title: "Event not found."),
        UseCaseResult<T>.Conflict => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: "concurrency-conflict",
            title: "The event was changed elsewhere."),
        _ => throw new InvalidOperationException($"Unhandled result {result.GetType().Name}."),
    };

    public static IResult Validation(ValidationResult errors) => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        type: "validation",
        title: "One or more fields are invalid.",
        extensions: new Dictionary<string, object?>
        {
            ["errors"] = errors.Errors
                .GroupBy(e => e.Field)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Code).ToArray()),
        });

    public static IResult Validation(string field, string code) => Validation(ValidationResult.Single(field, code));

    /// <summary>Unexpected failures. On writes this means nothing was saved (FR-014).</summary>
    public static async Task HandleUnexpectedAsync(HttpContext context)
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(ProblemResults))
            .LogError(exception, "Unhandled error for {Method} {Path}", context.Request.Method, context.Request.Path);

        var isWrite = !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method);
        var problem = Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            type: isWrite ? "save-failed" : "unexpected",
            title: isWrite ? "The change could not be saved." : "Something went wrong.");
        await problem.ExecuteAsync(context);
    }
}
