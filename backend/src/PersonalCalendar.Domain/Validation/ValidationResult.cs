namespace PersonalCalendar.Domain.Validation;

/// <summary>Collects every validation error instead of stopping at the first (FR-003).</summary>
public sealed class ValidationResult
{
    private readonly List<ValidationError> _errors = [];

    public IReadOnlyList<ValidationError> Errors => _errors;

    public bool IsValid => _errors.Count == 0;

    public static ValidationResult Single(string field, string code)
    {
        var result = new ValidationResult();
        result.Add(field, code);
        return result;
    }

    public void Add(string field, string code) => _errors.Add(new ValidationError(field, code));

    public void AddRange(ValidationResult other) => _errors.AddRange(other._errors);
}

/// <summary>Either a valid value or the errors that prevented it.</summary>
public sealed class Validated<T> where T : class
{
    private Validated(T? value, ValidationResult errors)
    {
        Value = value;
        Errors = errors;
    }

    public T? Value { get; }

    public ValidationResult Errors { get; }

    public bool IsValid => Value is not null;

    public static Validated<T> Success(T value) => new(value, new ValidationResult());

    public static Validated<T> Failure(ValidationResult errors) => new(null, errors);
}
