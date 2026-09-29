using NodaTime;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Domain.Events;

/// <summary>When an event happens: either a timed range of instants or an inclusive range of dates.</summary>
public abstract record EventSchedule;

/// <summary>A timed event. Invariant: <c>End &gt; Start</c>, compared as instants (FR-003).</summary>
public sealed record TimedSchedule : EventSchedule
{
    private TimedSchedule(Instant start, Instant end)
    {
        Start = start;
        End = end;
    }

    public Instant Start { get; }

    public Instant End { get; }

    public static Validated<TimedSchedule> Create(Instant start, Instant end) =>
        end > start
            ? Validated<TimedSchedule>.Success(new TimedSchedule(start, end))
            : Validated<TimedSchedule>.Failure(ValidationResult.Single("end", ErrorCodes.EndNotAfterStart));
}

/// <summary>An all-day event. The end date is inclusive. Invariant: <c>EndDate &gt;= StartDate</c>.</summary>
public sealed record AllDaySchedule : EventSchedule
{
    private AllDaySchedule(LocalDate startDate, LocalDate endDate)
    {
        StartDate = startDate;
        EndDate = endDate;
    }

    public LocalDate StartDate { get; }

    public LocalDate EndDate { get; }

    public static Validated<AllDaySchedule> Create(LocalDate startDate, LocalDate endDate) =>
        endDate >= startDate
            ? Validated<AllDaySchedule>.Success(new AllDaySchedule(startDate, endDate))
            : Validated<AllDaySchedule>.Failure(ValidationResult.Single("endDate", ErrorCodes.EndDateBeforeStart));
}
