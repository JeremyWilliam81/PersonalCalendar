namespace PersonalCalendar.Application.Abstractions;

/// <summary>The event was changed or deleted elsewhere since the caller last read it (research R8).</summary>
public sealed class ConcurrencyConflictException(string message) : Exception(message);
