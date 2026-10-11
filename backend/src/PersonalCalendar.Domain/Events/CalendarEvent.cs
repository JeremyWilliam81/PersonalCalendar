using NodaTime;
using NodaTime.TimeZones;
using PersonalCalendar.Domain.Validation;

namespace PersonalCalendar.Domain.Events;

/// <summary>
/// An event on the single user's calendar (data-model.md): a one-time event, or a series when
/// <see cref="Recurrence"/> is set (specs/003-recurring-events/data-model.md).
/// </summary>
public sealed class CalendarEvent
{
    public const int TitleMaxLength = 200;
    public const int LocationMaxLength = 200;
    public const int NotesMaxLength = 5000;

    private List<OccurrenceException> _exceptions;

    private CalendarEvent(
        EventId id,
        string title,
        string? location,
        string? notes,
        EventSchedule schedule,
        string entryTimeZone,
        Instant createdUtc,
        Instant updatedUtc,
        int version,
        SeriesRecurrence? recurrence,
        IEnumerable<OccurrenceException> exceptions)
    {
        Id = id;
        Title = title;
        Location = location;
        Notes = notes;
        Schedule = schedule;
        EntryTimeZone = entryTimeZone;
        CreatedUtc = createdUtc;
        UpdatedUtc = updatedUtc;
        Version = version;
        _exceptions = exceptions.ToList();
        SetRecurrence(recurrence);
    }

    public EventId Id { get; }

    public string Title { get; private set; }

    public string? Location { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>For a series, the schedule of its first occurrence.</summary>
    public EventSchedule Schedule { get; private set; }

    /// <summary>IANA id of the zone the event was last entered in. Not used for display (clarification Q2).</summary>
    public string EntryTimeZone { get; private set; }

    public Instant CreatedUtc { get; }

    public Instant UpdatedUtc { get; private set; }

    /// <summary>For a series, covers the series and all of its exceptions (research S5).</summary>
    public int Version { get; private set; }

    /// <summary>Null for a one-time event.</summary>
    public SeriesRecurrence? Recurrence { get; private set; }

    /// <summary>Changed and deleted occurrences. Always empty for a one-time event.</summary>
    public IReadOnlyList<OccurrenceException> Exceptions => _exceptions;

    /// <summary>The first occurrence's date in the series zone. Null for a one-time event.</summary>
    public LocalDate? SeriesFirstDate { get; private set; }

    /// <summary>The last occurrence's end date in the series zone; null when the series never ends (research S7).</summary>
    public LocalDate? SeriesLastDate { get; private set; }

    /// <param name="repeat">A rule makes the event a series; its start is moved to the first real occurrence (FR-009).</param>
    public static Validated<CalendarEvent> Create(
        string? title,
        string? location,
        string? notes,
        EventSchedule schedule,
        DateTimeZone entryZone,
        IClock clock,
        RepeatRule? repeat = null)
    {
        var fields = ValidateFields(title, location, notes);
        if (!fields.Errors.IsValid) return Validated<CalendarEvent>.Failure(fields.Errors);

        var (anchored, recurrence) = Anchor(schedule, repeat, entryZone);
        var now = clock.GetCurrentInstant();
        return Validated<CalendarEvent>.Success(new CalendarEvent(
            EventId.New(), fields.Title, fields.Location, fields.Notes, anchored, entryZone.Id, now, now, version: 1, recurrence, []));
    }

    /// <summary>
    /// Replaces every editable field. With a rule the event is (or stays) a series that starts at
    /// <paramref name="schedule"/>; without one it is a one-time event. On invalid input nothing changes.
    /// </summary>
    public ValidationResult Update(
        string? title,
        string? location,
        string? notes,
        EventSchedule schedule,
        DateTimeZone entryZone,
        IClock clock,
        RepeatRule? repeat = null)
    {
        var fields = ValidateFields(title, location, notes);
        if (!fields.Errors.IsValid) return fields.Errors;

        var (anchored, recurrence) = Anchor(schedule, repeat, entryZone);
        Title = fields.Title;
        Location = fields.Location;
        Notes = fields.Notes;
        Schedule = anchored;
        EntryTimeZone = entryZone.Id;
        if (recurrence is null) _exceptions.Clear();
        SetRecurrence(recurrence);
        Touch(clock);
        return fields.Errors;
    }

    /// <summary>Recreates an event that was already validated and stored. Used by persistence only.</summary>
    public static CalendarEvent Rehydrate(
        EventId id,
        string title,
        string? location,
        string? notes,
        EventSchedule schedule,
        string entryTimeZone,
        Instant createdUtc,
        Instant updatedUtc,
        int version,
        SeriesRecurrence? recurrence = null,
        IEnumerable<OccurrenceException>? exceptions = null) =>
        new(id, title, location, notes, schedule, entryTimeZone, createdUtc, updatedUtc, version, recurrence, exceptions ?? []);

    /// <summary>Validates the text fields only, e.g. to report them alongside schedule errors.</summary>
    public static ValidationResult ValidateTextFields(string? title, string? location, string? notes) =>
        ValidateFields(title, location, notes).Errors;

    /// <summary>
    /// Saves an edited occurrence with the chosen scope (research S6, FR-015 to FR-021). <paramref name="zone"/> is the
    /// zone the values were entered in. On any error nothing changes.
    /// </summary>
    public Validated<SeriesChange> ApplyEdit(EditScope scope, LocalDate date, OccurrenceInput input, DateTimeZone zone, IClock clock)
    {
        var recurrence = Recurrence ?? throw new InvalidOperationException("The event is not a series.");
        var fields = ValidateFields(input.Title, input.Location, input.Notes);
        if (!fields.Errors.IsValid) return Validated<SeriesChange>.Failure(fields.Errors);

        var current = CurrentOccurrence(date);
        var dateChanged = StartDateOf(input.Schedule, zone) != StartDateOf(current.Schedule, zone);
        var ruleChanged = !Equals(input.Repeat, recurrence.Rule);

        if (dateChanged && ruleChanged)
        {
            return Refuse(input.Schedule is AllDaySchedule ? "startDate" : "start", ErrorCodes.ScopeDateAndRepeatChanged);
        }

        if (scope == EditScope.This)
        {
            if (ruleChanged) return Refuse("scope", ErrorCodes.ScopeThisWithRepeatChange);

            Upsert(OccurrenceException.Changed(date, fields.Title, fields.Location, fields.Notes, input.Schedule));
            Touch(clock);
            return Validated<SeriesChange>.Success(new SeriesChange(null, false, date));
        }

        if (dateChanged) return Refuse("scope", ErrorCodes.ScopeDateChangeRequiresThis);

        var edit = new SeriesEdit(
            fields.Title != current.Title ? fields.Title : null,
            fields.Location != current.Location,
            fields.Location,
            fields.Notes != current.Notes,
            fields.Notes,
            !SameShape(current.Schedule, input.Schedule, zone),
            ruleChanged);

        var change = scope == EditScope.Following && date != SeriesFirstDate
            ? EditFollowing(date, edit, input, zone, clock)
            : EditAll(date, edit, input, zone, clock);
        return Validated<SeriesChange>.Success(change);
    }

    /// <summary>Deletes occurrences with the chosen scope (FR-023, FR-024).</summary>
    public SeriesChange ApplyDelete(EditScope scope, LocalDate date, IClock clock)
    {
        var recurrence = Recurrence ?? throw new InvalidOperationException("The event is not a series.");

        if (scope == EditScope.All || (scope == EditScope.Following && date == SeriesFirstDate))
        {
            return new SeriesChange(null, true, null);
        }

        if (scope == EditScope.Following)
        {
            Truncate(recurrence, date);
            _exceptions.RemoveAll(e => e.OriginalDate >= date);
        }
        else
        {
            Upsert(OccurrenceException.Deleted(date));
        }

        Touch(clock);
        return new SeriesChange(null, !HasVisibleOccurrence(), null);
    }

    /// <summary>The local date the schedule starts on in <paramref name="zone"/> (all-day dates are zone-free).</summary>
    public static LocalDate StartDateOf(EventSchedule schedule, DateTimeZone zone) => schedule switch
    {
        AllDaySchedule allDay => allDay.StartDate,
        TimedSchedule timed => timed.Start.InZone(zone).Date,
        _ => throw new InvalidOperationException($"Unknown schedule type {schedule.GetType().Name}."),
    };

    /// <summary>What changed relative to the occurrence the form was loaded with. Null title: unchanged.</summary>
    private sealed record SeriesEdit(
        string? Title,
        bool LocationChanged,
        string? Location,
        bool NotesChanged,
        string? Notes,
        bool TimesChanged,
        bool RuleChanged)
    {
        public string TitleOr(string title) => Title ?? title;

        public string? LocationOr(string? location) => LocationChanged ? Location : location;

        public string? NotesOr(string? notes) => NotesChanged ? Notes : notes;

        public OccurrenceException ApplyTo(OccurrenceException exception) =>
            exception.IsDeleted ? exception : exception.WithText(TitleOr(exception.Title!), LocationOr(exception.Location), NotesOr(exception.Notes));
    }

    private sealed record Occurrence(string Title, string? Location, string? Notes, EventSchedule Schedule);

    private static Validated<SeriesChange> Refuse(string field, string code) =>
        Validated<SeriesChange>.Failure(ValidationResult.Single(field, code));

    private Occurrence CurrentOccurrence(LocalDate date)
    {
        var exception = _exceptions.FirstOrDefault(e => e.OriginalDate == date && !e.IsDeleted);
        return exception is null
            ? new Occurrence(Title, Location, Notes, Events.Recurrence.ScheduleOn(this, date))
            : new Occurrence(exception.Title!, exception.Location, exception.Notes, exception.Schedule!);
    }

    /// <summary>"All events": text-only changes keep exceptions; time or rule changes start over (FR-019 to FR-021).</summary>
    private SeriesChange EditAll(LocalDate date, SeriesEdit edit, OccurrenceInput input, DateTimeZone zone, IClock clock)
    {
        var recurrence = Recurrence!;
        var first = SeriesFirstDate!.Value;

        if (input.Repeat is null)
        {
            // Only the first occurrence is kept, as a one-time event, with this edit applied to it.
            var firstOccurrence = CurrentOccurrence(first);
            Title = edit.TitleOr(firstOccurrence.Title);
            Location = edit.LocationOr(firstOccurrence.Location);
            Notes = edit.NotesOr(firstOccurrence.Notes);
            Schedule = edit.TimesChanged ? MoveTo(input.Schedule, StartDateOf(firstOccurrence.Schedule, zone), zone) : firstOccurrence.Schedule;
            EntryTimeZone = zone.Id;
            _exceptions.Clear();
            SetRecurrence(null);
            Touch(clock);
            return new SeriesChange(null, false, null);
        }

        Title = edit.TitleOr(Title);
        Location = edit.LocationOr(Location);
        Notes = edit.NotesOr(Notes);

        if (!edit.TimesChanged && !edit.RuleChanged)
        {
            for (var i = 0; i < _exceptions.Count; i++) _exceptions[i] = edit.ApplyTo(_exceptions[i]);
            Touch(clock);
            return new SeriesChange(null, false, date);
        }

        // New times are entered in the device zone; a rule change alone keeps the series' own zone and times.
        var anchorZone = edit.TimesChanged ? zone : recurrence.TimeZone;
        var firstSchedule = edit.TimesChanged ? MoveTo(input.Schedule, first, zone) : Schedule;
        var (anchored, newRecurrence) = Anchor(firstSchedule, input.Repeat, anchorZone);
        Schedule = anchored;
        EntryTimeZone = zone.Id;
        _exceptions.Clear();
        SetRecurrence(newRecurrence);
        Touch(clock);
        return new SeriesChange(null, false, Events.Recurrence.Produces(this, date) ? date : SeriesFirstDate);
    }

    /// <summary>"This and following": end this series before the occurrence and start a new one there (FR-017).</summary>
    private SeriesChange EditFollowing(LocalDate date, SeriesEdit edit, OccurrenceInput input, DateTimeZone zone, IClock clock)
    {
        var recurrence = Recurrence!;
        var position = Events.Recurrence.PositionOf(recurrence.Rule, SeriesFirstDate!.Value, date);
        var ownSchedule = Events.Recurrence.ScheduleOn(this, date);
        var later = _exceptions.Where(e => e.OriginalDate >= date).ToList();

        CalendarEvent created;
        if (input.Repeat is null)
        {
            // The occurrence stays as a one-time event with its current values and this edit (FR-021).
            var current = CurrentOccurrence(date);
            created = Create(
                edit.TitleOr(current.Title), edit.LocationOr(current.Location), edit.NotesOr(current.Notes),
                input.Schedule, zone, clock).Value!;
        }
        else
        {
            var tailRule = input.Repeat;
            if (recurrence.Rule.End is RepeatEnd.AfterCount total && Equals(tailRule.End, recurrence.Rule.End))
            {
                tailRule = tailRule.WithEnd(new RepeatEnd.AfterCount(total.Count - (position - 1)));
            }

            var anchorZone = edit.TimesChanged || edit.RuleChanged ? zone : recurrence.TimeZone;
            created = Create(
                edit.TitleOr(Title), edit.LocationOr(Location), edit.NotesOr(Notes),
                edit.TimesChanged ? input.Schedule : ownSchedule, anchorZone, clock, tailRule).Value!;

            if (!edit.TimesChanged && !edit.RuleChanged)
            {
                created._exceptions.AddRange(later.Select(edit.ApplyTo));
            }
        }

        Truncate(recurrence, date);
        _exceptions.RemoveAll(e => e.OriginalDate >= date);
        Touch(clock);
        return new SeriesChange(created, false, created.SeriesFirstDate);
    }

    /// <summary>Ends the series just before <paramref name="date"/>, which must not be its first occurrence.</summary>
    private void Truncate(SeriesRecurrence recurrence, LocalDate date)
    {
        var rule = recurrence.Rule;
        var end = rule.End is RepeatEnd.AfterCount
            ? new RepeatEnd.AfterCount(Events.Recurrence.PositionOf(rule, SeriesFirstDate!.Value, date) - 1)
            : (RepeatEnd)new RepeatEnd.OnDate(date.PlusDays(-1));
        SetRecurrence(recurrence with { Rule = rule.WithEnd(end) });
    }

    private void Upsert(OccurrenceException exception)
    {
        _exceptions.RemoveAll(e => e.OriginalDate == exception.OriginalDate);
        _exceptions.Add(exception);
    }

    /// <summary>A changed occurrence, or a produced date that was not deleted. Series without an end always have one.</summary>
    private bool HasVisibleOccurrence()
    {
        if (_exceptions.Any(e => !e.IsDeleted)) return true;
        if (SeriesLastDate is null) return true;

        var deleted = _exceptions.Select(e => e.OriginalDate).ToHashSet();
        return Events.Recurrence
            .OriginalDates(Recurrence!.Rule, SeriesFirstDate!.Value, SeriesFirstDate.Value, SeriesLastDate.Value)
            .Any(d => !deleted.Contains(d));
    }

    /// <summary>True when both schedules have the same kind, time of day and length (a different date does not count).</summary>
    private static bool SameShape(EventSchedule a, EventSchedule b, DateTimeZone zone) => (a, b) switch
    {
        (AllDaySchedule x, AllDaySchedule y) =>
            Period.Between(x.StartDate, x.EndDate, PeriodUnits.Days) == Period.Between(y.StartDate, y.EndDate, PeriodUnits.Days),
        (TimedSchedule x, TimedSchedule y) => LocalShape(x, zone) == LocalShape(y, zone),
        _ => false,
    };

    private static (LocalTime Start, Period Length) LocalShape(TimedSchedule schedule, DateTimeZone zone)
    {
        var start = schedule.Start.InZone(zone).LocalDateTime;
        var end = schedule.End.InZone(zone).LocalDateTime;
        return (start.TimeOfDay, Period.Between(start, end, PeriodUnits.Days | PeriodUnits.AllTimeUnits));
    }

    /// <summary>The same times of day and length on another date, resolved like any occurrence.</summary>
    private static EventSchedule MoveTo(EventSchedule schedule, LocalDate date, DateTimeZone zone) => schedule switch
    {
        AllDaySchedule allDay => AllDaySchedule.Create(date, allDay.EndDate + Period.Between(allDay.StartDate, date, PeriodUnits.Days)).Value!,
        TimedSchedule timed => Events.Recurrence.TimedOn(date, timed.Start.InZone(zone).LocalDateTime, timed.End.InZone(zone).LocalDateTime, zone),
        _ => throw new InvalidOperationException($"Unknown schedule type {schedule.GetType().Name}."),
    };

    private void Touch(IClock clock)
    {
        UpdatedUtc = clock.GetCurrentInstant();
        Version++;
    }

    private void SetRecurrence(SeriesRecurrence? recurrence)
    {
        Recurrence = recurrence;
        if (recurrence is null)
        {
            SeriesFirstDate = SeriesLastDate = null;
            return;
        }

        var first = StartDateOf(Schedule, recurrence.TimeZone);
        SeriesFirstDate = first;
        SeriesLastDate = Events.Recurrence.Range(recurrence.Rule, first, Events.Recurrence.SpanDays(this)).Last;
    }

    /// <summary>
    /// Moves the schedule to the rule's first occurrence and records the local times the series repeats at
    /// (research S3). A one-time event is returned unchanged.
    /// </summary>
    private static (EventSchedule Schedule, SeriesRecurrence? Recurrence) Anchor(EventSchedule schedule, RepeatRule? repeat, DateTimeZone zone)
    {
        if (repeat is null) return (schedule, null);

        var startDate = StartDateOf(schedule, zone);
        var shift = Period.Between(startDate, repeat.FirstOccurrence(startDate), PeriodUnits.Days);

        switch (schedule)
        {
            case AllDaySchedule allDay:
                return (AllDaySchedule.Create(allDay.StartDate + shift, allDay.EndDate + shift).Value!, new SeriesRecurrence(repeat, zone, null, null));
            case TimedSchedule timed:
                var startLocal = timed.Start.InZone(zone).LocalDateTime + shift;
                var endLocal = timed.End.InZone(zone).LocalDateTime + shift;
                var anchored = shift.Days == 0
                    ? timed
                    : Events.Recurrence.TimedOn(startLocal.Date, startLocal, endLocal, zone);
                return (anchored, new SeriesRecurrence(repeat, zone, startLocal, endLocal));
            default:
                throw new InvalidOperationException($"Unknown schedule type {schedule.GetType().Name}.");
        }
    }

    private static (string Title, string? Location, string? Notes, ValidationResult Errors) ValidateFields(
        string? title, string? location, string? notes)
    {
        var errors = new ValidationResult();

        var trimmedTitle = title?.Trim() ?? string.Empty;
        if (trimmedTitle.Length == 0) errors.Add("title", ErrorCodes.TitleRequired);
        else if (trimmedTitle.Length > TitleMaxLength) errors.Add("title", ErrorCodes.TitleTooLong);

        var trimmedLocation = NullIfEmpty(location);
        if (trimmedLocation?.Length > LocationMaxLength) errors.Add("location", ErrorCodes.LocationTooLong);

        var trimmedNotes = NullIfEmpty(notes);
        if (trimmedNotes?.Length > NotesMaxLength) errors.Add("notes", ErrorCodes.NotesTooLong);

        return (trimmedTitle, trimmedLocation, trimmedNotes, errors);
    }

    private static string? NullIfEmpty(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
