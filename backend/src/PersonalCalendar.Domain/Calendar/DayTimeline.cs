using NodaTime;
using PersonalCalendar.Domain.Events;

namespace PersonalCalendar.Domain.Calendar;

/// <summary>
/// Lays out one day for the day and week views (research V3, V4). Positions are elapsed minutes from the
/// day's real start instant, so spring-forward and fall-back days are 23 and 25 hours long by construction.
/// </summary>
public static class DayTimeline
{
    private static readonly Duration HourMarkProbe = Duration.FromMinutes(15);

    public static DayTimelineResult Build(LocalDate date, DateTimeZone zone, IEnumerable<CalendarItem> events)
    {
        var dayStart = zone.AtStartOfDay(date).ToInstant();
        var dayEnd = zone.AtStartOfDay(date.PlusDays(1)).ToInstant();
        var all = events.ToList();

        var allDay = EventOrdering.Order(all.Where(e =>
            e.Schedule is AllDaySchedule a && a.StartDate <= date && a.EndDate >= date));

        return new DayTimelineResult(
            date,
            dayStart,
            dayEnd,
            WholeMinutes(dayEnd - dayStart),
            HourMarks(dayStart, dayEnd, zone),
            allDay,
            Segments(dayStart, dayEnd, all));
    }

    /// <summary>Every instant in the day whose local time is a whole hour, in instant order.</summary>
    private static List<HourMark> HourMarks(Instant dayStart, Instant dayEnd, DateTimeZone zone)
    {
        // Probing every 15 minutes finds whole hours even after a 30-minute DST shift (Australia/Lord_Howe).
        var marks = new List<HourMark>();
        for (var instant = dayStart; instant < dayEnd; instant += HourMarkProbe)
        {
            var local = instant.InZone(zone).TimeOfDay;
            if (local.Minute == 0 && local.Second == 0)
                marks.Add(new HourMark(WholeMinutes(instant - dayStart), local));
        }

        return marks;
    }

    private static List<TimedSegment> Segments(Instant dayStart, Instant dayEnd, IEnumerable<CalendarItem> events)
    {
        var clipped = events
            .Select(e => (Event: e, Timed: e.Schedule as TimedSchedule))
            .Where(x => x.Timed is not null && x.Timed.Start < dayEnd && x.Timed.End > dayStart)
            .Select(x => new Clipped(
                x.Event,
                Max(x.Timed!.Start, dayStart),
                Min(x.Timed.End, dayEnd),
                x.Timed.Start < dayStart,
                x.Timed.End > dayEnd))
            .OrderBy(c => c.Start)
            .ThenByDescending(c => c.End - c.Start)
            .ThenBy(c => c.Event.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Event.Id.Value)
            .ThenBy(c => c.Event.Occurrence?.OriginalDate ?? LocalDate.MinIsoValue)
            .ToList();

        var columns = AssignColumns(clipped);
        return clipped
            .Select((c, i) => new TimedSegment(
                c.Event,
                WholeMinutes(c.Start - dayStart),
                Math.Max(1, (int)Math.Ceiling((c.End - c.Start).TotalMinutes)),
                c.ContinuesBefore,
                c.ContinuesAfter,
                columns[i].Column,
                columns[i].Count))
            .ToList();
    }

    /// <summary>
    /// Splits the sorted segments into clusters that overlap directly or through a chain, then gives each
    /// segment the first column whose previous segment has ended. Touching segments do not overlap.
    /// </summary>
    private static (int Column, int Count)[] AssignColumns(List<Clipped> sorted)
    {
        var result = new (int Column, int Count)[sorted.Count];
        var clusterFirst = 0;
        var clusterEnd = Instant.MinValue;
        var columnEnds = new List<Instant>();

        for (var i = 0; i <= sorted.Count; i++)
        {
            if (i == sorted.Count || sorted[i].Start >= clusterEnd)
            {
                for (var j = clusterFirst; j < i; j++) result[j].Count = columnEnds.Count;
                if (i == sorted.Count) break;
                clusterFirst = i;
                columnEnds.Clear();
            }

            var column = columnEnds.FindIndex(end => end <= sorted[i].Start);
            if (column < 0)
            {
                column = columnEnds.Count;
                columnEnds.Add(sorted[i].End);
            }
            else
            {
                columnEnds[column] = sorted[i].End;
            }

            result[i].Column = column;
            clusterEnd = Max(clusterEnd, sorted[i].End);
        }

        return result;
    }

    private static int WholeMinutes(Duration duration) => (int)Math.Floor(duration.TotalMinutes);

    private static Instant Max(Instant a, Instant b) => a > b ? a : b;

    private static Instant Min(Instant a, Instant b) => a < b ? a : b;

    private sealed record Clipped(CalendarItem Event, Instant Start, Instant End, bool ContinuesBefore, bool ContinuesAfter);
}
