using NodaTime;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Events;

public abstract record UseCaseResult<T>
{
    public sealed record Ok(T Value) : UseCaseResult<T>;

    public sealed record ValidationFailed(ValidationResult Errors) : UseCaseResult<T>;

    public sealed record AdjustmentRequired(LocalDateTime AdjustedStart, LocalDateTime AdjustedEnd, string TimeZone) : UseCaseResult<T>;

    public sealed record NotFound : UseCaseResult<T>;

    public sealed record Conflict : UseCaseResult<T>;
}

/// <summary>Result value for use cases that return nothing on success.</summary>
public sealed record Unit
{
    public static readonly Unit Value = new();
}
