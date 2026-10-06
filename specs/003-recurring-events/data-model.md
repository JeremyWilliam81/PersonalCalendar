# Data Model: Recurring Events

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

This extends the [001 data model](../001-event-basics/data-model.md). Everything there still applies to one-time events.

## Domain (C#, NodaTime types)

### `CalendarEvent` (aggregate root, extended)

A `CalendarEvent` is either a one-time event (`Recurrence` is `null`, exactly as in 001) or a **series**.

| New field | Type | Rules |
|---|---|---|
| `Recurrence` | `SeriesRecurrence?` | `null` means "does not repeat". |
| `Exceptions` | `IReadOnlyList<OccurrenceException>` | Empty for one-time events. Loaded with the series. |

For a series, `Schedule` is the schedule of the **first occurrence**. A timed first occurrence keeps its instants as in 001.

### `SeriesRecurrence` (value object)

| Field | Type | Rules |
|---|---|---|
| `Rule` | `RepeatRule` | See below. |
| `TimeZone` | `DateTimeZone` (stored as its IANA id) | The zone the series repeats in (research S3). It is set when the series is created and replaced only when the times or rule change. |
| `StartLocal` / `EndLocal` | `LocalDateTime?` | Timed series only. The first occurrence's local start and end in `TimeZone`. |

### `RepeatRule` (value object)

| Field | Type | Rules |
|---|---|---|
| `Frequency` | `Daily \| Weekly \| Monthly \| Yearly` | Required. |
| `Interval` | `int` | 1–99 (FR-005). The default is 1. |
| `Weekdays` | `IsoDayOfWeek` set | Weekly only. At least 1 (FR-002). |
| `Monthly` | `DayOfMonth \| WeekdayPosition` | Monthly only. `DayOfMonth` takes its day number from the start date. Days 29–31 clamp to the last day of shorter months (Q1). `WeekdayPosition(Ordinal ∈ {1,2,3,4,−1}, Weekday)` must match the start date: ordinals 1–4 equal `ceil(day / 7)`, and −1 requires the start date to be in the month's last 7 days (FR-003). |
| `End` | `Never \| OnDate(LocalDate) \| AfterCount(int)` | `OnDate` must be ≥ the first occurrence's date. `AfterCount` must be 1–999 (FR-007). |

Methods:
- `ToRRule()` and `ParseRRule(text, zone)` implement the mapping in research S2.
- `FirstOccurrence(startDate)` gives the start date itself, or, for weekly rules, the first chosen weekday after it (FR-009). Creating or editing a series *normalizes* the first occurrence to this date, so the stored start is always a real occurrence, as RFC 5545 expects.

### `OccurrenceException` (entity within the series)

| Field | Type | Rules |
|---|---|---|
| `OriginalDate` | `LocalDate` | Key within the series (research S4). Must be a date the rule produces. |
| `IsDeleted` | `bool` | `true` means the occurrence is hidden (`EXDATE`). |
| `Title`, `Location`, `Notes` | as in 001 | A snapshot. Required when not deleted, with the 001 length rules. |
| `Schedule` | `EventSchedule?` | A snapshot, `TimedSchedule` or `AllDaySchedule`. Required when not deleted. It may fall on any date (spec Edge Cases). |

### `CalendarItem` (read record fed to the layouts, research S8)

`Id` (the event or series id), `Title`, `Schedule` (this occurrence's), and `Occurrence?` = `(OriginalDate, IsException)`. `MonthGrid`, `DayTimeline`, and `AllDayLanes` now take `IEnumerable<CalendarItem>`. Their ordering and layout rules are unchanged.

### `Recurrence.Expand` (pure domain service)

`Expand(CalendarEvent series, LocalDate fromDate, LocalDate toDate) → IEnumerable<CalendarItem>`

1. Generate the original dates of the rule in `[fromDate − spanDays, toDate]`, in the series zone. Callers pad the range by two days on each side, because the requested zone may differ from the series zone (research S7). For rules without COUNT it jumps forward over whole periods. COUNT rules are walked from the start (research S7).
2. For each date, build the schedule:
   - **All-day**: `[d, d + (EndDate − StartDate)]`.
   - **Timed**: the local start `d + StartLocal.TimeOfDay` and local end `start + Period(StartLocal → EndLocal)`, each resolved in `TimeZone` with `LenientResolver` (001 R4).
3. Replace dates that have an exception: deleted ones are dropped, and changed ones use their snapshot.
4. Add changed exceptions whose snapshot overlaps the range even though their original date is outside it.
5. The caller clips the result to the exact range, as for one-time events.

### Scope operations (domain methods, research S6)

| Method | Effect |
|---|---|
| `EditOccurrence(date, fields, schedule)` | Upserts a changed exception. It is refused when the rule input differs from the series rule. |
| `DeleteOccurrence(date)` | Upserts a deleted exception. If none is left, it returns `SeriesNowEmpty` and the use case deletes the series. |
| `SplitAt(date) → (truncated, tailRule)` | Truncates with `COUNT = k − 1` or `UNTIL = date − 1`, and returns the remaining COUNT for the new series. |
| `UpdateSeries(fields, schedule, recurrence)` | For text-only changes, it propagates the changed fields into the exception snapshots. For rule or time changes, it clears all exceptions. |
| `StopRepeating()` | Becomes a one-time event built from the first occurrence, or from that occurrence's exception values. |

Each change increments `Version` once.

*Implementation note (2026-10-06):* these operations are reached through two public entry points, `CalendarEvent.ApplyEdit(scope, originalDate, OccurrenceInput, zone, clock)` and `ApplyDelete(scope, originalDate, clock)`. Both return a `SeriesChange(Created, DeleteOriginal, ShowDate)`. The FR-016 and FR-016a refusals are checked inside `ApplyEdit`, so the server enforces them whatever the client sends.

### Validation codes (in addition to 001's)

| Code | Field | Message (English UI text) |
|---|---|---|
| `recurrence.frequency.invalid` | `recurrence.frequency` | "Choose how often the event repeats." |
| `recurrence.interval.outOfRange` | `recurrence.interval` | "Enter a number from 1 to 99." |
| `recurrence.weekdays.required` | `recurrence.weekdays` | "Choose at least one weekday." |
| `recurrence.monthly.invalid` | `recurrence.monthly` | "That monthly option doesn't match the start date." |
| `recurrence.until.beforeStart` | `recurrence.until` | "End date must be on or after the start date." |
| `recurrence.count.outOfRange` | `recurrence.count` | "Enter a number from 1 to 999." |
| `occurrence.required` | `occurrence` | Returned when `PUT`/`DELETE`/`GET` on a series is missing `occurrence`. |
| `occurrence.invalid` | `occurrence` | The date isn't an occurrence of the series, or the event isn't a series. |
| `scope.required` / `scope.invalid` | `scope` | Returned when `PUT`/`DELETE` on a series is missing a scope or has an unknown one. |
| `scope.thisWithRepeatChange` | `scope` | "A single event can't have its own repeat settings. Choose This and following events or All events." (FR-016) |
| `scope.dateChangeRequiresThis` | `scope` | "A new date can only apply to this event." (FR-016a) |
| `scope.dateAndRepeatChanged` | `start` / `startDate` | "A new date applies only to this event, but repeat changes apply to the series. Undo one of them." (FR-016a) |

## Application

- **`IEventRepository`**:
  - `ListOverlappingAsync` also returns series whose bounds overlap the padded range, plus series that have a changed exception in the range. Each comes with all of its exceptions.
  - `GetAsync` loads the exceptions too.
  - New: `ReplaceAsync(IReadOnlyList<CalendarEvent> upserts, IReadOnlyList<EventId> deletes, (EventId, int) expectedVersion)`. It runs in one transaction for split, stop-repeating, and series-now-empty (FR-027).
- **New use-case inputs**: `EditScope { This, Following, All }` (a Domain type, because the scope rules are domain methods: `CalendarEvent.ApplyEdit` and `ApplyDelete`), plus `OccurrenceDate` on `UpdateEvent`, `DeleteEvent`, and `GetEventDetails`.
- **Read models**:
  - `EventSummary` gains `OccurrenceDate?` and `IsRecurring`.
  - `EventDetails` gains `Recurrence?` (the structured rule and `TimeZone`), `OccurrenceDate?`, `IsException`, and `ExceptionCount`, which the UI uses for the FR-020 warning.

## Persistence (SQLite)

### `Events` (new nullable columns, migration `AddRecurrence`)

| Column | SQLite type | Null | Notes |
|---|---|---|---|
| `RecurrenceRule` | TEXT | yes | RRULE text (research S2). `NULL` for one-time events. |
| `RecurrenceTimeZone` | TEXT | yes | IANA id. |
| `StartLocal` | TEXT | yes | `uuuu-MM-ddTHH:mm:ss`. Timed series only. |
| `EndLocal` | TEXT | yes | Same format as `StartLocal`. |
| `SeriesFirstDate` | TEXT | yes | `uuuu-MM-dd`, in the series zone. |
| `SeriesLastDate` | TEXT | yes | `NULL` when the series never ends. |

- **New CHECK constraint `CK_Events_Recurrence`**: either `RecurrenceRule IS NULL` and the other five columns are also `NULL`, or `RecurrenceRule`, `RecurrenceTimeZone`, and `SeriesFirstDate` are set; `StartLocal` and `EndLocal` are set exactly when `IsAllDay = 0`; and `SeriesLastDate` is null or ≥ `SeriesFirstDate`.
- **New index**: `IX_Events_Series (SeriesFirstDate, SeriesLastDate) WHERE RecurrenceRule IS NOT NULL`.
- **Existing rows**: they all stay one-time events, so the migration only adds nullable columns and needs no data change.

### `OccurrenceExceptions` (new table)

| Column | SQLite type | Null | Notes |
|---|---|---|---|
| `SeriesId` | TEXT | no | FK → `Events.Id`, `ON DELETE CASCADE`. |
| `OriginalDate` | TEXT | no | `uuuu-MM-dd`. PK is (`SeriesId`, `OriginalDate`). |
| `IsDeleted` | INTEGER | no | 0/1 |
| `Title` | TEXT | yes | Required when `IsDeleted = 0`. |
| `Location`, `Notes` | TEXT | yes | |
| `IsAllDay` | INTEGER | yes | |
| `StartUtc`, `EndUtc`, `StartDate`, `EndDate` | TEXT | yes | Same formats and the same either/or rule as `Events` (001). |

- **CHECK `CK_OccurrenceExceptions_Shape`**: either it is deleted and every snapshot column is `NULL`, or it is not deleted and has a title and a valid timed or all-day schedule, using 001's `CK_Events_Schedule` expression.
- **Indexes**: `IX_OccurrenceExceptions_StartUtc_EndUtc` and `IX_OccurrenceExceptions_StartDate_EndDate`, for moved occurrences (research S7).

## Lifecycle

```text
OneTime --Update(+recurrence)--> Series(v+1)
Series --This(edit|delete)--> Series(v+1, exception upserted) | (deleted when none left)
Series --Following(edit)--> Series(v+1, truncated) + new Series(v1)
Series --Following(delete)--> Series(v+1, truncated)     [Following at first occurrence ≡ All]
Series --All(text only)--> Series(v+1, exceptions' text updated)
Series --All(rule/times)--> Series(v+1, exceptions cleared)
Series --All(does not repeat)--> OneTime(v+1)
Series --All(delete)--> (gone, exceptions cascade)
Invalid input, refused scope, or stale version -> no state change
```
