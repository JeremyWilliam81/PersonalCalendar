using NodaTime;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Domain.Calendar;

/// <summary>
/// Packs all-day events into bars across a range of days (research V5). Only plain dates are used,
/// so no zone or DST rule can move a bar (FR-016).
/// </summary>
public static class AllDayLanes
{
    public static IReadOnlyList<AllDayBar> Build(LocalDate first, int dayCount, IEnumerable<CalendarItem> allDayEvents)
    {
        var last = first.PlusDays(dayCount - 1);
        var laneEnds = new List<int>(); // last covered day index per lane
        var bars = new List<AllDayBar>();

        foreach (var calendarEvent in EventOrdering.Order(allDayEvents))
        {
            if (calendarEvent.Schedule is not AllDaySchedule schedule) continue;
            if (schedule.EndDate < first || schedule.StartDate > last) continue;

            var from = schedule.StartDate < first ? first : schedule.StartDate;
            var to = schedule.EndDate > last ? last : schedule.EndDate;
            var startIndex = Period.Between(first, from, PeriodUnits.Days).Days;
            var span = Period.Between(from, to, PeriodUnits.Days).Days + 1;

            var lane = laneEnds.FindIndex(end => end < startIndex);
            if (lane < 0)
            {
                lane = laneEnds.Count;
                laneEnds.Add(0);
            }

            laneEnds[lane] = startIndex + span - 1;
            bars.Add(new AllDayBar(
                calendarEvent, startIndex, span, lane, schedule.StartDate < first, schedule.EndDate > last));
        }

        return bars;
    }
}
