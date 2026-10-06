using NodaTime;

namespace PersonalCalendar.Domain.Events;

/// <summary>Where a change to one occurrence of a series applies (FR-015, FR-023).</summary>
public enum EditScope
{
    This,
    Following,
    All,
}

/// <summary>
/// The values the user saved for an occurrence. <see cref="Repeat"/> is the full rule the form showed;
/// null means "does not repeat".
/// </summary>
public sealed record OccurrenceInput(string? Title, string? Location, string? Notes, EventSchedule Schedule, RepeatRule? Repeat);

/// <summary>The outcome of a scope operation (research S6). The series itself has been changed in place.</summary>
/// <param name="Created">A new series or one-time event, from "This and following".</param>
/// <param name="DeleteOriginal">True when the whole series is to be deleted.</param>
/// <param name="ShowDate">The occurrence to show afterwards: in the original series, or in <paramref name="Created"/> when set.</param>
public sealed record SeriesChange(CalendarEvent? Created, bool DeleteOriginal, LocalDate? ShowDate);
