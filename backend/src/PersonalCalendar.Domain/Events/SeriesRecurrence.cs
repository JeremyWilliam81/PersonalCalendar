using NodaTime;

namespace PersonalCalendar.Domain.Events;

/// <summary>
/// What makes an event a series (research S3): the rule, the zone it repeats in, and — for timed series only —
/// the first occurrence's local start and end, so a 9:00 AM series stays at 9:00 AM across DST (Constitution II).
/// </summary>
public sealed record SeriesRecurrence(RepeatRule Rule, DateTimeZone TimeZone, LocalDateTime? StartLocal, LocalDateTime? EndLocal);
