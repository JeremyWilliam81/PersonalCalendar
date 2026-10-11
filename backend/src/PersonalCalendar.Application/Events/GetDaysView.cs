using NodaTime;
using NodaTime.Text;
using PersonalCalendar.Application.Abstractions;
using PersonalCalendar.Application.Time;
using PersonalCalendar.Domain.Calendar;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Application.Events;

/// <summary>One day or seven consecutive days laid out on a time scale (research V3–V5).</summary>
public sealed class GetDaysView(IEventRepository repository, ZoneLookup zones, IClock clock)
{
    // Same technical limits as the month view; the 1900–2199 navigation range is a UI rule (FR-012).
    private const int MinYear = 1;
    private const int MaxYear = 9998;
    private static readonly LocalTimePattern LabelPattern = LocalTimePattern.CreateWithInvariantCulture("HH:mm");

    public async Task<UseCaseResult<DaysViewModel>> HandleAsync(
        string? start, int? count, string? timeZone, CancellationToken cancellationToken = default)
    {
        var zone = zones.Find(timeZone);
        if (zone is null) return new UseCaseResult<DaysViewModel>.ValidationFailed(ZoneLookup.UnknownZone());

        var errors = new ValidationResult();
        var parsed = start is null ? null : LocalDatePattern.Iso.Parse(start);
        if (parsed is not { Success: true }) errors.Add("start", "start.invalid");
        if (count is not (1 or 7)) errors.Add("count", "count.invalid");
        if (errors.IsValid && !InRange(parsed!.Value, count!.Value)) errors.Add("start", "start.outOfRange");
        if (!errors.IsValid) return new UseCaseResult<DaysViewModel>.ValidationFailed(errors);

        var first = parsed!.Value;
        var last = first.PlusDays(count!.Value - 1);
        var stored = await repository.ListOverlappingAsync(
            zone.AtStartOfDay(first).ToInstant(), zone.AtStartOfDay(last.PlusDays(1)).ToInstant(), first, last, cancellationToken);
        var events = CalendarItems.Build(stored, first, last);

        var now = clock.GetCurrentInstant().InZone(zone);
        var days = Enumerable.Range(0, count.Value)
            .Select(i => ToModel(DayTimeline.Build(first.PlusDays(i), zone, events), zone, now.Date))
            .ToList();
        var bars = count == 7
            ? AllDayLanes.Build(first, 7, events)
                .Select(b => new AllDayBarModel(
                    EventMapping.ToSummary(b.Event, zone), b.StartIndex, b.Span, b.Lane, b.ContinuesBefore, b.ContinuesAfter))
                .ToList()
            : [];

        return new UseCaseResult<DaysViewModel>.Ok(new DaysViewModel(zone.Id, now.Date, now.ToOffsetDateTime(), days, bars));
    }

    private static bool InRange(LocalDate first, int count) =>
        first.Year >= MinYear && first.Year <= MaxYear && first.PlusDays(count - 1).Year <= MaxYear;

    private static TimelineDayModel ToModel(DayTimelineResult day, DateTimeZone zone, LocalDate today) =>
        new(
            day.Date,
            day.Date == today,
            day.DayStart.InZone(zone).ToOffsetDateTime(),
            day.DayEnd.InZone(zone).ToOffsetDateTime(),
            day.LengthMinutes,
            day.HourMarks.Select(m => new HourMarkModel(m.OffsetMinutes, LabelPattern.Format(m.Label))).ToList(),
            day.AllDay.Select(e => EventMapping.ToSummary(e, zone)).ToList(),
            day.Timed.Select(s => new TimedSegmentModel(
                EventMapping.ToSummary(s.Event, zone),
                s.OffsetMinutes,
                s.DurationMinutes,
                s.ContinuesBefore,
                s.ContinuesAfter,
                s.Column,
                s.ColumnCount)).ToList());
}
