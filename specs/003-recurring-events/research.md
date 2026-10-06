# Research: Recurring Events

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-10-06

Each entry lists a Decision, its Rationale, and the Alternatives considered. This feature builds on [001 research](../001-event-basics/research.md) (R1–R11) and [002 research](../002-calendar-views/research.md) (V1–V12), which still apply unless an entry below says otherwise. No NEEDS CLARIFICATION items remain.

**Design priority** (memory and the 002 clarifications): tapping and clicking come first. Keyboard use must reach the Constitution IV baseline and no further.

## S1. Recurrence engine: a small domain expander, not a library

- **Decision**: A pure, test-first domain function, `Recurrence.Expand(series, fromDate, toDate)`, produces occurrences for the subset of RFC 5545 that the spec allows. That subset is FREQ DAILY, WEEKLY, MONTHLY, or YEARLY, with INTERVAL; BYDAY for weekly; BYMONTHDAY, BYDAY with an ordinal, and BYSETPOS for monthly; BYMONTH and BYMONTHDAY for yearly; WKST=SU; and COUNT or UNTIL. It is written in C# with NodaTime `LocalDate` and `LocalDateTime` arithmetic, and has no dependencies.
- **Rationale**:
  - The supported subset is small, about 200 lines. Owning it keeps every date calculation in NodaTime, where the Constitution I hazard suite already runs.
  - The constitution requires RFC 5545 *semantics*, not a particular library. S2 makes the stored rule real RRULE text, so a future import or export feature is not blocked.
- **Alternatives considered**:
  - **Ical.Net**: it brings its own date and time-zone types alongside NodaTime, its API is far larger than this need, and its DST behavior would have to be re-verified anyway (Principle III).
  - **rrule.js on the client**: this would put recurrence math in TypeScript, splitting date logic across two languages (001 R5).

## S2. How the rule is stored: RRULE text plus structured API

- **Decision**: A series stores its rule as an RFC 5545 `RRULE` value in one text column. The domain has a `RepeatRule` value object with `ToRRule()` and `RepeatRule.ParseRRule()`. The parser accepts only what the formatter writes, and round-trip tests cover every form. The API and UI never see RRULE text. They exchange a structured `recurrence` object ([contracts/http-api.md](./contracts/http-api.md)).

  | UI choice | RRULE |
  |---|---|
  | Every N days | `FREQ=DAILY;INTERVAL=N` |
  | Every N weeks on Mon, Wed | `FREQ=WEEKLY;INTERVAL=N;BYDAY=MO,WE;WKST=SU` |
  | Every N months on day 1–28 | `FREQ=MONTHLY;INTERVAL=N;BYMONTHDAY=d` |
  | Every N months on day 29, 30, or 31 (last day of shorter months, clarification Q1) | `FREQ=MONTHLY;INTERVAL=N;BYMONTHDAY=28,29,…,d;BYSETPOS=-1` |
  | Every N months on the 2nd Wednesday | `FREQ=MONTHLY;INTERVAL=N;BYDAY=2WE` |
  | Every N months on the last Friday | `FREQ=MONTHLY;INTERVAL=N;BYDAY=-1FR` |
  | Every N years | `FREQ=YEARLY;INTERVAL=N;BYMONTH=m;BYMONTHDAY=d` (Feb 29 occurs only in leap years) |
  | Ends after K | `;COUNT=K` |
  | Ends on a date | `;UNTIL=…` (S3) |

  `INTERVAL=1` is written explicitly so that every stored rule has the same shape.
- **Rationale**: The clamping rule from clarification Q1 is expressed in standard RFC 5545 (`BYSETPOS=-1` over a set of month days), so the constitution's "RFC 5545 semantics" holds literally. A structured API keeps validation codes per field (FR-007) and keeps RRULE parsing out of the frontend.
- **Alternatives considered**: Separate columns for each part were simpler to query but would lose the standard form and need a schema change for any later rule part. Arbitrary RRULE input was rejected because the spec limits the options.

## S3. Anchoring a series in local time (Constitution II, FR-013)

- **Decision**: A series stores:
  - the IANA `RecurrenceTimeZone`, which is the device zone when the series was created or when its times were last changed (spec Assumptions);
  - for a **timed** series, the first occurrence's `StartLocal` and `EndLocal` as `LocalDateTime` in that zone. The existing `StartUtc` and `EndUtc` are also kept, for the first occurrence only;
  - for an **all-day** series, the existing `StartDate` and `EndDate` of the first occurrence.

  Each occurrence date `d` gets the local start `d + StartLocal.TimeOfDay` and the local end `start + (EndLocal − StartLocal)`, computed as a `Period` so that multi-day and overnight events keep their wall-clock length. Both are resolved with the same `LenientResolver` as single events (001 R4): a gap shifts forward by the gap length, and an overlap takes the earlier time. They are then turned into instants. Display converts the instants into the requested (device) zone as before.

  `UNTIL` follows RFC 5545: it is a UTC date-time when DTSTART has a zone, which here means the end of the chosen end date in `RecurrenceTimeZone`, and a plain `DATE` value for all-day series. The domain keeps the user-facing `EndsOn` as a `LocalDate` and converts only at the RRULE boundary.
- **Rationale**: This is exactly the case Constitution II calls out: "a local wall-clock meaning must survive rule changes (e.g., recurring events) … the original local value and its IANA zone MUST be stored". A 9:00 AM series stays at 9:00 AM across DST and after tzdb updates. Showing occurrences in the device zone keeps feature 001's "follow the device" clarification.
- **Alternatives considered**:
  - Repeating a fixed UTC instant every N × 24 hours was rejected, because the event drifts by an hour at every DST change.
  - Repeating in the device's *current* zone was rejected, because a trip would move every occurrence (spec Edge Cases).

## S4. Identifying an occurrence

- **Decision**: An occurrence is identified by `(seriesId, originalDate)`, where `originalDate` is the `LocalDate` the rule produced, in `RecurrenceTimeZone`. The supported rules produce at most one occurrence per date, so this key is unique. It does not change when the occurrence is moved ("This event") or when the device zone changes. The API carries it as `occurrence=yyyy-MM-dd`.
- **Rationale**: A plain date is stable, readable in URLs and tests, and doesn't depend on any zone conversion. It is the date-only equivalent of RFC 5545 `RECURRENCE-ID`.
- **Alternatives considered**: An instant-based RECURRENCE-ID changes meaning when a series' times change. A generated occurrence id would have to be stored for every occurrence, including infinite series.

## S5. Exceptions: one table for changed and deleted occurrences

- **Decision**: A new table `OccurrenceExceptions` holds one row per `(SeriesId, OriginalDate)`, which is unique. A row is either:
  - **Deleted** (`IsDeleted = 1`), the equivalent of RFC 5545 `EXDATE`; or
  - **Changed**: a full snapshot of the occurrence's title, location, notes, and schedule (timed instants or all-day dates, like a single event), the equivalent of a `RECURRENCE-ID` override.

  Deleted occurrences still count toward `COUNT`, as RFC 5545 requires (spec Edge Cases). The series row's `Version` goes up whenever any of its exceptions change, so one concurrency token covers the series and its exceptions (001 R8).
- **Rationale**: A full snapshot makes reading simple: an exception row replaces the generated occurrence. FR-019 propagation is a single update over the snapshots (S6).
- **Alternatives considered**: Storing only the changed fields, with null meaning "inherit", was rejected because it can't tell "inherit the location" apart from "location deliberately cleared". Materializing every occurrence as its own event row cannot represent infinite series.

## S6. Scope operations (FR-015 to FR-024)

All operations are domain methods on the series aggregate, so they can be tested without storage. Each one runs in one `SaveChanges` transaction (FR-027).

- **This event, edit**: upsert the exception snapshot. This is refused when the rule changed (FR-016). A start-date change is allowed only with this scope (FR-016a).
- **This event, delete**: upsert `IsDeleted`. If no visible occurrence would remain, delete the series (FR-024).
- **This and following**, at original date `d`, where `d` is not the first occurrence. Otherwise it is treated as **All**.
  1. Truncate the original series. For a `COUNT` rule, it gets `COUNT = k − 1`, where `k` is `d`'s 1-based position, counting deleted occurrences. Otherwise it gets `UNTIL = d − 1 day`.
  2. On edit, create a new series starting at `d` with the edited values. A count-based series gets `COUNT = original COUNT − (k − 1)`, so the total stays the same (FR-017). Exceptions with an original date on or after `d` move to the new series if only text changed (FR-019). They are dropped if the rule or times changed (FR-020).
  3. On delete, step 1 only, plus removing exceptions on or after `d`.

  The interval phase is preserved because `d` is itself an occurrence, so it starts an included week, month, or year.
- **All events**:
  - **Text-only change**: update the series, and write the changed fields into every exception snapshot. An exception keeps its own times, and deleted rows stay (FR-019).
  - **Rule or times change**: update the series and delete all of its exceptions (FR-020). The UI warns first (S11).
- **Does not repeat** (FR-021): with All, the series becomes a one-time event made from the first occurrence (with its exception values, if it has any). Its rule columns are cleared and its exceptions deleted. With This and following, step 1 runs and occurrence `d` becomes a new one-time event.
- **Turning a one-time event into a series** (Story 1, scenario 12): this is a normal update that sets the rule. Its id and version history are kept.

## S7. Querying and expanding efficiently (SC-004)

- **Decision**:
  - Series rows store `SeriesFirstDate` (always set) and `SeriesLastDate` (the last occurrence's *end* date in the series zone, or null when there is no end). Both are computed at save time, with at most 999 occurrences for `COUNT`.
  - `ListOverlappingAsync` adds a third condition: `RecurrenceRule IS NOT NULL AND SeriesFirstDate <= toDate + 2 AND (SeriesLastDate IS NULL OR SeriesLastDate >= fromDate − 2)`. The pad is two days, because UTC offsets span −12 to +14 hours, so one instant can fall on local dates up to two days apart. *(Implementation note, 2026-10-06: the plan first said one day.)*
  - Exceptions whose *changed* schedule overlaps the range are also loaded with their series, because an occurrence may be moved outside the series range (spec Edge Cases).
  - `Expand` jumps forward to the period that contains `fromDate` for rules without `COUNT`, using whole periods of the interval: days, weeks, months, or years. It never walks from 1900. With `COUNT`, it walks at most 999 occurrences.
- **Rationale**: With 200 infinite series, a 6-week month grid expands at most 200 × 42 candidate dates, well under the 1 s budget. The 002 performance test is extended to 5,000 events plus 200 series (SC-004).
- **Alternatives considered**: Caching materialized occurrences adds invalidation for no measurable gain at this scale (Principle III).

## S8. Feeding occurrences to the existing views

- **Decision**: `MonthGrid`, `DayTimeline`, and `AllDayLanes` take `CalendarItem`s instead of `CalendarEvent`s. A `CalendarItem` is a small read record with the id, title, schedule, and `Occurrence?` (`SeriesId`, `OriginalDate`, `IsException`). One-time events map to items with no occurrence. The Application layer builds the list: one-time events from the repository, plus `Recurrence.Expand` for each series, with exceptions applied. `EventSummary` gains `occurrenceDate` and `isRecurring` (contracts).
- **Rationale**: The layout functions don't care where an item came from. A narrow input type keeps them pure and keeps their test suites valid, after a mechanical change of input type.
- **Alternatives considered**: Building fake `CalendarEvent` aggregates for occurrences was rejected because it would break the aggregate's invariants (version, identity).

## S9. API shape

- **Decision**: The existing `/api/events` endpoints are extended. No parallel set is added.
  - `EventInput` gains an optional `recurrence`, where `null` means "does not repeat".
  - `GET /api/events/{id}?occurrence=` returns the occurrence's details and the series rule.
  - `PUT` and `DELETE` take `occurrence` and `scope=this|following|all`. Both are required when the event is a series, and rejected when it isn't.

  Each mismatch has its own validation code, such as `scope.thisWithRepeatChange` and `scope.dateChangeRequiresThis` (data-model). The server enforces FR-016 and FR-016a, and the client also filters the choices before asking (S11).
- **Rationale**: The client already has a single create, edit, and delete flow. Additive fields keep every 001 and 002 contract test valid, because one-time events behave exactly as before.

## S10. Repeat controls in the event form (FR-001 to FR-009, FR-028)

- **Decision**: A `RepeatFields` section sits inside the existing `EventFormDialog`. It uses native elements only (Principle III):
  - **Frequency**: a segmented group of five large buttons (Does not repeat, Daily, Weekly, Monthly, Yearly), built from radio inputs styled as buttons. On narrow screens it wraps to two rows.
  - **Interval**: a "−" button, a number input (`inputmode="numeric"`), and a "+" button, each 44 × 44 px, with the label "Every N weeks".
  - **Weekdays**: seven toggle buttons with `aria-pressed`, a single-letter visible label, and the full weekday name as the accessible name (FR-029). They fit in one row from 320 px wide.
  - **Monthly mode**: two large radio cards, such as "On day 14" or "On the second Wednesday", plus "On the last Wednesday" when applicable (FR-003). They are filled in from the start date.
  - **Ends**: three radio cards: Never, On date (with a native `<input type="date">`), and After N times (with the same stepper).
  - **Summary**: a live text line under the fields (FR-008), which also shows the adjusted first date (FR-009).

  The pure, zone-free helpers in `lib/recurrence.ts` are `monthlyOptions(startDate)`, `firstOccurrence(startDate, rule)`, and `describeRule(rule, locale)`. They use `Intl` for weekday and month names, are written test-first, and match the backend by shared test cases. The server is always authoritative.
- **Rationale**: Every control is a single tap with fingertip-sized targets. There is no custom picker or new dependency, and native radio and toggle semantics give the keyboard and screen-reader baseline for free.
- **Alternatives considered**: A dropdown of presets ("Every weekday", "Every 2 weeks…") is compact but makes weekday choice a second dialog. Free-text rules were rejected.

## S11. Choosing the scope and warning (FR-015, FR-016a, FR-020, FR-021, FR-023)

- **Decision**: A `ScopeChoiceDialog`, built on the existing `Modal`, appears after Save or Delete on an occurrence. Its choices are stacked full-width buttons, at least 48 px tall: "This event", "This and following events", "All events", and Cancel.
  - The client hides "This event" when the repeat options changed, and offers only "This event" when the start date changed.
  - When both changed, the form shows the FR-016a message and doesn't open the dialog.
  - After a choice that would discard exceptions (rule or times changed while `exceptionCount > 0`, from the details) or turn the series into a one-time event, the dialog shows a second step: a warning with Confirm and Back.
  - Cancel returns to the form with the edits kept (FR-015).
- **Rationale**: One reusable dialog covers edit and delete. Hiding impossible choices is clearer on touch than showing them disabled, and the server still enforces the rules (S9).

## S12. The repeat indicator (FR-011, FR-030)

- **Decision**: An inline SVG "repeat" glyph (two curved arrows) after the title in event labels, time-grid blocks, all-day bars, the narrow week list, and the details dialog. It is `aria-hidden`, and the accessible name gains ", repeats". The phone-sized month markers don't show it (FR-011). The details dialog shows the `describeRule` summary under the date.
- **Rationale**: It is a shape, not a color, so it meets the "not color alone" rule. One icon component is reused everywhere.

## S13. Test matrix (Constitution I)

Domain tests are written before the code. They use explicit zones and `FakeClock`:

- **`RepeatRule`**: RRULE round-trip for every row in S2, and validation (interval 1–99, count 1–999, at least one weekday, until ≥ start).
- **`Recurrence.Expand`**:
  - daily, weekly, monthly, and yearly with intervals 1, 2, and 3;
  - weekly with several days and a start that isn't a chosen weekday (FR-009);
  - monthly on day 31 and day 30 across Feb 2027, Feb 2028, and Apr (Q1);
  - monthly 2nd Wednesday and last Friday (Q2), and a 5th weekday start that offers only "last";
  - yearly Feb 29 (leap years only);
  - COUNT with deleted occurrences, UNTIL inclusive, and fast-forward correctness (the result is identical to walking from the start);
  - year boundaries and the 2199-12-31 limit.
- **DST**: a 9:00 weekly series across the spring and fall changes in `America/Chicago`; occurrences at 02:30 on a gap day (shifted to 03:30) and 01:30 on an overlap day (the earlier one); `Australia/Lord_Howe` (30-minute shift); `Asia/Kolkata` and `Australia/Adelaide` displayed from a series in `America/Chicago`; and an overnight series (22:00–01:00) whose occurrence spans a DST change.
- **Scope operations**: every bullet in S6, including the split total for COUNT, propagation of exceptions, the first-occurrence equivalence, the last remaining occurrence, and FR-016a refusals.
- **Frontend**: `monthlyOptions`, `firstOccurrence`, and `describeRule` are tested with the same dates as the backend, under a fixed `TZ` and locale.
