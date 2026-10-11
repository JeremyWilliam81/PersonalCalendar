using NodaTime;
using PersonalCalendar.Application.Abstractions;
using PersonalCalendar.Application.Time;
using PersonalCalendar.Domain.Calendar;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Events;

public sealed class GetMonthView(IEventRepository repository, ZoneLookup zones, IClock clock)
{
    // Grids for these months stay within NodaTime's supported date range.
    private const int MinYear = 1;
    private const int MaxYear = 9998;

    /// <param name="year">With <paramref name="month"/>; when either is null the current month in the zone is used.</param>
    public async Task<UseCaseResult<MonthViewModel>> HandleAsync(
        int? year, int? month, string? timeZone, CancellationToken cancellationToken = default)
    {
        var zone = zones.Find(timeZone);
        if (zone is null) return new UseCaseResult<MonthViewModel>.ValidationFailed(ZoneLookup.UnknownZone());

        var today = clock.GetCurrentInstant().InZone(zone).Date;
        var (targetYear, targetMonth) = year is null || month is null ? (today.Year, today.Month) : (year.Value, month.Value);

        var errors = new ValidationResult();
        if (targetYear is < MinYear or > MaxYear) errors.Add("year", "year.outOfRange");
        if (targetMonth is < 1 or > 12) errors.Add("month", "month.outOfRange");
        if (!errors.IsValid) return new UseCaseResult<MonthViewModel>.ValidationFailed(errors);

        var (first, last) = MonthGrid.GridRange(targetYear, targetMonth);
        var from = zone.AtStartOfDay(first).ToInstant();
        var to = zone.AtStartOfDay(last.PlusDays(1)).ToInstant();

        var events = CalendarItems.Build(await repository.ListOverlappingAsync(from, to, first, last, cancellationToken), first, last);
        var grid = MonthGrid.Build(targetYear, targetMonth, zone, today, events);

        var weeks = grid.Weeks
            .Select(week => new WeekModel(week
                .Select(day => new DayModel(
                    day.Date, day.InMonth, day.IsToday, day.Events.Select(e => EventMapping.ToSummary(e, zone)).ToList()))
                .ToList()))
            .ToList();

        return new UseCaseResult<MonthViewModel>.Ok(new MonthViewModel(targetYear, targetMonth, zone.Id, today, weeks));
    }
}
