---

description: "Task list for 003 Recurring Events"
---

# Tasks: Recurring Events

**Input**: Design documents from `/specs/003-recurring-events/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Required. Constitution Principle I makes test-first mandatory for all date/time logic: `RepeatRule`, `Recurrence.Expand`, the scope operations, and the frontend helpers in `lib/recurrence.ts`. Features 001 and 002 also wrote API and component tests for every story, and this list continues that. Every test task MUST be written and seen to fail before its implementation task.

**Organization**: Tasks are grouped by user story so each story can be implemented and tested independently.

**Design priority** (memory, 002 clarifications, plan): touch and pointer first. Every UI task lists its tap/click behavior first and keeps keyboard work to the Constitution IV baseline. Do not add keyboard shortcuts.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on unfinished tasks)
- **[Story]**: Which user story this task belongs to (US1–US5)
- Every task names its exact file paths

## Path Conventions

- Backend: `backend/src/PersonalCalendar.{Domain,Application,Infrastructure,Api}/…`, with tests in `backend/tests/PersonalCalendar.{Domain,Application,Api}.Tests/…`
- Frontend: `frontend/src/{api,lib,components,test}/…`, with tests colocated as `*.test.ts[x]`
- "Series zone" means `SeriesRecurrence.TimeZone` (research S3). "Original date" means the occurrence key (research S4).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm a green baseline and add the shared test helpers.

- [X] T001 Confirm the baseline on branch `003-recurring-events`: run `dotnet test backend/PersonalCalendar.slnx`, `npm test --prefix frontend`, and `npm run lint --prefix frontend`. All must pass before any change. Record any failure that was already there in the PR description, and don't fix unrelated code here.
- [X] T002 [P] Add `backend/tests/PersonalCalendar.Domain.Tests/TestZones.cs` with static `DateTimeZone` fields `Chicago`, `NewYork`, `Kolkata`, `Adelaide`, and `LordHowe`, all from `DateTimeZoneProviders.Tzdb`. Also add a helper `D(string yyyyMMdd) → LocalDate` and `LDT(string yyyy-MM-ddTHH:mm) → LocalDateTime`, using `LocalDatePattern.Iso` and `LocalDateTimePattern.GeneralIso`. If the existing domain tests already define equivalent helpers, reuse them and only add the missing zones.
- [X] T003 [P] Extend `frontend/src/test/fixtures.ts` with the builders `recurringSummary(overrides)` and `recurringDetails(overrides)`. They return `EventSummary` and `EventDetails` with `isRecurring: true`, `occurrenceDate`, and a weekly `recurrence` (`{frequency:'weekly', interval:1, weekdays:['monday','wednesday','friday'], monthly:null, end:{type:'never'}, timeZone:'America/Chicago'}`), `exceptionCount: 0`, and `isException: false`. The default for non-recurring builders becomes `isRecurring: false` and `occurrenceDate: null`. This depends on T008, the type changes.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add the repeat rule value object, the `CalendarItem` input for the layouts, the schema, and the additive API and type fields. Every story depends on these.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete. After this phase, the app MUST behave exactly as before for one-time events.

### Tests first (Principle I) ⚠️

- [X] T004 [P] Write `backend/tests/PersonalCalendar.Domain.Tests/RepeatRuleTests.cs` (data-model "RepeatRule", research S2):
  - **Validation**, collected into `ValidationResult` with these codes:
    - `Interval` must be "1–99": `0` and `100` → `recurrence.interval.outOfRange`.
    - A weekly rule with no weekdays → `recurrence.weekdays.required`.
    - `AfterCount` must be "1–999": `0` and `1000` → `recurrence.count.outOfRange`.
    - `OnDate` before the start date → `recurrence.until.beforeStart`, and `OnDate` equal to the start date is valid.
    - Monthly `WeekdayPosition`: ordinal `2` for start `2026-10-14` (Wednesday, day 14, `ceil(14/7)=2`) is valid and ordinal `3` is invalid. `-1` is valid for `2026-10-30` (in the last 7 days) and invalid for `2026-10-14`. For start `2026-10-29` (the 5th Thursday), only `-1` is valid → `recurrence.monthly.invalid`.
  - **RRULE round-trip** for every row of the research S2 table, with exact strings:
    - `FREQ=DAILY;INTERVAL=1`
    - `FREQ=WEEKLY;INTERVAL=2;BYDAY=TU,TH;WKST=SU`, with BYDAY in Sunday-first order
    - `FREQ=MONTHLY;INTERVAL=1;BYMONTHDAY=14`
    - `FREQ=MONTHLY;INTERVAL=1;BYMONTHDAY=28,29,30,31;BYSETPOS=-1` (day 31)
    - `FREQ=MONTHLY;INTERVAL=1;BYMONTHDAY=28,29,30;BYSETPOS=-1` (day 30)
    - `FREQ=MONTHLY;INTERVAL=1;BYMONTHDAY=28,29;BYSETPOS=-1` (day 29)
    - `FREQ=MONTHLY;INTERVAL=3;BYDAY=2WE`
    - `FREQ=MONTHLY;INTERVAL=1;BYDAY=-1FR`
    - `FREQ=YEARLY;INTERVAL=1;BYMONTH=2;BYMONTHDAY=29`
    - `;COUNT=10`
    - for a timed series in `America/Chicago` ending `2026-12-31`: `;UNTIL=20270101T055959Z` (the last second of the end date in the series zone, in UTC)
    - for an all-day series: `;UNTIL=20261231`

    `ParseRRule` returns an equal `RepeatRule` for each. It rejects any other part (e.g. `BYHOUR`) or format by throwing `FormatException`, because only stored, trusted values are parsed.
  - **`FirstOccurrence(start)`**: weekly on Thursday only, start Tue `2026-10-06` → `2026-10-08`. Weekly on Tue and Thu, start Tue → the same day. Every other frequency → the start date.
- [X] T005 [P] Write the `CalendarItem` input tests by changing the existing `backend/tests/PersonalCalendar.Domain.Tests/MonthGridTests.cs`, `DayTimelineTests.cs`, and `AllDayLanesTests.cs` to build their inputs with a new test helper `Items.OneTime(CalendarEvent)` / `Items.Of(id, title, schedule, occurrence?)` in `backend/tests/PersonalCalendar.Domain.Tests/Items.cs`. Expected outputs don't change. Add one case to each file: two `CalendarItem`s with the **same id** and different `Occurrence.OriginalDate` are both placed, ordered by the existing rules (research S8). These tests fail to compile until T009.

### Implementation

- [X] T006 Add the error codes to `backend/src/PersonalCalendar.Domain/Validation/ValidationError.cs` (`ErrorCodes`): `recurrence.frequency.invalid`, `recurrence.interval.outOfRange`, `recurrence.weekdays.required`, `recurrence.monthly.invalid`, `recurrence.until.beforeStart`, `recurrence.count.outOfRange`, `occurrence.required`, `occurrence.invalid`, `scope.required`, `scope.invalid`, `scope.thisWithRepeatChange`, `scope.dateChangeRequiresThis`, and `scope.dateAndRepeatChanged` (data-model "Validation codes").
- [X] T007 Implement `backend/src/PersonalCalendar.Domain/Events/RepeatRule.cs` to pass T004. It is a sealed record with:
  - `Frequency` (`RepeatFrequency` enum Daily/Weekly/Monthly/Yearly);
  - `Interval` (int);
  - `Weekdays` (`IReadOnlySet<IsoDayOfWeek>`, empty unless weekly);
  - `Monthly` (`MonthlyPattern?`: `DayOfMonth` or `WeekdayPosition(int Ordinal)`);
  - `End` (`RepeatEnd`: `Never`, `OnDate(LocalDate)`, or `AfterCount(int)`).

  `static Validated<RepeatRule> Create(…, LocalDate startDate)` collects every error. It also has `LocalDate FirstOccurrence(LocalDate start)`, `string ToRRule(LocalDate firstDate, DateTimeZone zone, bool isAllDay)`, and `static RepeatRule ParseRRule(string rrule, LocalDate firstDate, DateTimeZone zone)`. The weekday and month day for monthly and yearly rules come from `firstDate`, so they aren't stored twice. Value equality must compare `Weekdays` by set contents. Also add `backend/src/PersonalCalendar.Domain/Events/SeriesRecurrence.cs`: a record of `(RepeatRule Rule, DateTimeZone TimeZone, LocalDateTime? StartLocal, LocalDateTime? EndLocal)`, where `StartLocal` and `EndLocal` are "Timed series only".
- [X] T008 [P] Extend `frontend/src/api/types.ts` with:
  - `Weekday` (lowercase English names);
  - `Recurrence` (`frequency`, `interval`, `weekdays`, `monthly: {type:'dayOfMonth'} | {type:'weekdayPosition', ordinal: 1|2|3|4|-1} | null`, `end: {type:'never'} | {type:'until', until: DateString} | {type:'count', count:number}`, and an optional `timeZone` in responses);
  - `EditScope = 'this' | 'following' | 'all'`;
  - on `EventSummary`: `isRecurring?: boolean` and `occurrenceDate?: DateString | null`;
  - on `EventDetails`: `recurrence?: Recurrence | null`, `occurrenceDate?`, `seriesStart?`, `seriesStartDate?`, `isException?`, and `exceptionCount?`;
  - on `EventInput`: `recurrence?: Recurrence | null`.

  These match [contracts/http-api.md](./contracts/http-api.md). The fields are optional so that existing fixtures compile.
- [X] T009 Add `backend/src/PersonalCalendar.Domain/Calendar/CalendarItem.cs`: `record CalendarItem(EventId Id, string Title, EventSchedule Schedule, OccurrenceRef? Occurrence)` and `record OccurrenceRef(LocalDate OriginalDate, bool IsException)`, plus `static CalendarItem FromEvent(CalendarEvent e)`. Then change `MonthGrid.cs`, `DayTimeline.cs`, `AllDayLanes.cs`, `EventOrdering.cs`, and the result types (`DayCell.cs`, `TimedSegment.cs`, `AllDayBar.cs`) in `backend/src/PersonalCalendar.Domain/Calendar/` to take and return `CalendarItem` instead of `CalendarEvent`. Ordering ties are now broken by id, then by original date. T005 must pass and every existing domain test must stay green.
- [X] T010 Update `backend/src/PersonalCalendar.Application/Events/GetMonthView.cs`, `GetDaysView.cs`, and `EventMapping.cs` to map repository events with `CalendarItem.FromEvent` before layout. `EventMapping.ToSummary(CalendarItem, zone)` now sets `IsRecurring = item.Occurrence is not null` and `OccurrenceDate = item.Occurrence?.OriginalDate`. Add both fields to `EventSummary` in `backend/src/PersonalCalendar.Application/Events/MonthViewModel.cs`. All existing application and API tests must stay green, with JSON output gaining `"isRecurring": false, "occurrenceDate": null`.
- [X] T011 Add persistence for series in `backend/src/PersonalCalendar.Infrastructure/Persistence/`:
  - **`EventRow.cs`**: add `RecurrenceRule`, `RecurrenceTimeZone`, `StartLocal` (`LocalDateTime?`), `EndLocal`, `SeriesFirstDate` (`LocalDate?`), and `SeriesLastDate`, all nullable.
  - **`NodaTimeConverters.cs`**: add a `LocalDateTime` converter (`uuuu-MM-ddTHH:mm:ss`).
  - **`EventConfiguration.cs`**:
    - the CHECK constraint `CK_Events_Recurrence`: "either `RecurrenceRule IS NULL` and the other five columns are also `NULL`, or `RecurrenceRule`, `RecurrenceTimeZone`, and `SeriesFirstDate` are set; `StartLocal` and `EndLocal` are set exactly when `IsAllDay = 0`; and `SeriesLastDate` is null or ≥ `SeriesFirstDate`";
    - the filtered index `IX_Events_Series (SeriesFirstDate, SeriesLastDate) WHERE RecurrenceRule IS NOT NULL`.
  - **`OccurrenceExceptionRow.cs`** and **`OccurrenceExceptionConfiguration.cs`**: table `OccurrenceExceptions` with PK (`SeriesId`, `OriginalDate`), an FK to `Events.Id` with `ON DELETE CASCADE`, `IsDeleted`, the snapshot columns `Title`, `Location`, `Notes`, `IsAllDay`, `StartUtc`, `EndUtc`, `StartDate`, and `EndDate` (all nullable), the CHECK `CK_OccurrenceExceptions_Shape` ("either it is deleted and every snapshot column is `NULL`, or it is not deleted and has a title and a valid timed or all-day schedule, using 001's `CK_Events_Schedule` expression"), and the indexes `IX_OccurrenceExceptions_StartUtc_EndUtc` and `IX_OccurrenceExceptions_StartDate_EndDate`.
  - **`CalendarDbContext.cs`**: register the new configuration.
- [X] T012 Generate the migration with `dotnet ef migrations add AddRecurrence --project backend/src/PersonalCalendar.Infrastructure --startup-project backend/src/PersonalCalendar.Api`, into `backend/src/PersonalCalendar.Infrastructure/Persistence/Migrations/`. Check that the generated `Up` only adds nullable columns, the CHECK, the index, and the new table, and makes no data change. Then write `backend/tests/PersonalCalendar.Api.Tests/MigrationUpgradeTests.cs`:
  1. Create a SQLite file migrated only to `20260929175058_InitialCreate`.
  2. Insert 2 events with raw SQL.
  3. Migrate to the latest version.
  4. Check that both events load through `GET /api/events/{id}` unchanged, with `recurrence: null`.

**Checkpoint**: all 001 and 002 tests are green, the app behaves as before, and the schema is ready.

---

## Phase 3: User Story 1 - Make an event repeat (Priority: P1) 🎯 MVP

**Goal**: Create a series, or turn an event into one, with frequency, interval, weekdays, monthly mode, and end, and see every occurrence in the month, week, and day views after a reload.

**Independent Test**: Create a weekly Mon and Wed event that ends after 6 occurrences. Exactly those 6 dates appear in the month, week, and day views, and they're still there after a reload (spec US1).

### Tests for User Story 1 (write first, must fail) ⚠️

- [X] T013 [P] [US1] Write `backend/tests/PersonalCalendar.Domain.Tests/RecurrenceExpandTests.cs` for `Recurrence.Expand(series, fromDate, toDate)` (research S7 and S13). Each case asserts the exact list of `(OriginalDate, local start, local end)`:
  - **Daily and weekly**:
    - Daily from `2026-10-06`, COUNT 5 → Oct 6–10.
    - Weekly from `2026-10-06`, UNTIL `2026-10-27` → Oct 6, 13, 20, 27 (inclusive).
    - Weekly M/W/F from Mon `2026-10-12`, Never, range Oct 12–31.
    - Every 2 weeks on Tue and Thu from `2026-10-06` → Oct 6, 8, 20, 22, Nov 3, 5. Weeks start Sunday (WKST=SU).
  - **Monthly by day number**:
    - Monthly day 14 for 12 months.
    - Every 3 months, day 15, from `2027-01-15` → Jan, Apr, Jul, Oct.
    - Day 31 from `2027-01-31` → Jan 31, Feb 28, Mar 31, Apr 30.
    - Day 30 → Feb 28 in 2027 and Feb 29 in 2028.
    - Day 29 → Feb 28, 2027 and Feb 29, 2028.
  - **Monthly by weekday position**:
    - 2nd Wednesday from `2026-10-14` → Nov 11, Dec 9, 2026, and Jan 13, 2027.
    - Last Friday from `2026-10-30` → Nov 27, Dec 25, 2026, and Jan 29, 2027.
  - **Yearly**: from `2027-03-14`, all-day. From `2028-02-29` → 2032-02-29 and 2036-02-29 only.
  - **Fast-forward**: a daily series from `1900-01-01` with no end, queried for `2199-12-01`..`2199-12-31`, returns 31 items in under 50 ms, identical to a reference walk. Do the same for every 3 weeks and every 5 months.
  - **COUNT with a range after the start**: weekly COUNT 6 queried for its 4th–6th weeks returns exactly occurrences 4–6.
  - **DST** (series zone `America/Chicago`):
    - Weekly 09:00–10:00 across `2027-03-14` and `2026-11-01` stays at 09:00 local on both sides, and the elapsed time between the two instants is 167 h and 169 h.
    - Daily 02:30–03:00 on `2027-03-14` resolves to 03:30–04:00 (the gap shifts forward).
    - Daily 01:30 on `2026-11-01` resolves to the earlier 01:30 (−05:00).
    - Overnight 22:00–01:00 keeps a 3-hour wall-clock span, and its elapsed time on `2026-10-31`→`11-01` is 4 h.
  - **`Australia/Lord_Howe`**: a 02:15 daily series on the 30-minute spring-forward day resolves to 02:45.
  - **Multi-day all-day**: a 3-day all-day event monthly on day 10 → `[10,12]` each month. Query `fromDate` = the 11th of a month still returns that month's occurrence (the span pad).
- [X] T014 [P] [US1] Write `backend/tests/PersonalCalendar.Application.Tests/RecurringUseCaseTests.cs`, the create and view part:
  - **`CreateEvent` with a weekly rule**:
    - Normalizes the start to `FirstOccurrence` (FR-009). Start Tue `2026-10-06` with only Thursday → the first occurrence is `2026-10-08` 07:00 local, with `StartLocal` and `EndLocal` set and `RecurrenceTimeZone = America/Chicago`.
    - Rule errors are returned together with title errors.
  - **`GetMonthView` for Oct 2026** with that series and one one-time event: occurrences on every matching day with `IsRecurring = true` and `OccurrenceDate` set. The one-time event is unchanged.
  - **Display in another zone**: a 07:00 Chicago series requested with `timeZone=America/New_York` shows 08:00 (spec Edge Cases).
  - **`GetDaysView`** with `count=7` places occurrences on the time scale.
  - **`UpdateEvent` on a one-time event** that adds a daily rule (scenario 12): it keeps the id, `Version` + 1, and its occurrences start on the original date.

  Extend `backend/tests/PersonalCalendar.Application.Tests/Fakes/InMemoryEventRepository.cs` with series filtering using the same rule as T017.
- [X] T015 [P] [US1] Write `backend/tests/PersonalCalendar.Api.Tests/RecurrenceEndpointTests.cs`, the create and view part:
  - **`POST /api/events`** with the contract's `recurrence` JSON → `201`, and the details have `recurrence` (including `timeZone`) and `occurrenceDate`.
  - **Invalid values**: interval `0`, an empty `weekdays` for weekly, count `1000`, `until` before start, a monthly ordinal that doesn't match, and an unknown `frequency` each return `400` with the matching `errors` key: `recurrence.interval`, `recurrence.weekdays`, `recurrence.count`, `recurrence.until`, `recurrence.monthly`, and `recurrence.frequency`.
  - **`GET /api/calendar/month`** and **`/api/calendar/days`** include the occurrences, with `isRecurring` and `occurrenceDate`.
  - **Persistence**: a new `ApiFactory` client on the same database file still returns them (SC-006).
- [X] T016 [P] [US1] Write `frontend/src/lib/recurrence.test.ts` (research S10), under the fixed `TZ` and `en-US`:
  - **`monthlyOptions('2026-10-14')`** → `[{type:'dayOfMonth', label:'On day 14'}, {type:'weekdayPosition', ordinal:2, label:'On the second Wednesday'}]`.
  - **`monthlyOptions('2026-10-30')`** → `[day 30, {ordinal:-1, label:'On the last Friday'}]`. October 30 is the 5th Friday, so there is no ordinal option.
  - **`monthlyOptions('2026-10-28')`** (the 4th Wednesday and in the last 7 days) → day 28, fourth Wednesday, and last Wednesday.
  - **`firstOccurrence`** with the same cases as T004.
  - **`describeRule`**:
    - "Weekly on Monday, Wednesday, and Friday"
    - "Every 2 weeks on Tuesday and Thursday, 6 times"
    - "Monthly on day 31, or the last day of shorter months"
    - "Monthly on the last Friday, until December 31, 2026"
    - "Yearly on February 29 (leap years only)"
    - "Daily"
    - "Every 3 months on day 15"
- [X] T017 [P] [US1] Write `frontend/src/components/RepeatFields.test.tsx` (contracts/ui-interaction.md "Repeat section"), with taps through `user-event` first:
  - **Defaults**: "Does not repeat" is selected.
  - **Weekly**: tapping Weekly shows 7 toggles with the start date's weekday `aria-pressed=true`. Tapping "Friday" toggles it. Clearing every weekday shows "Choose at least one weekday." next to the toggles.
  - **Interval stepper**: tapping − at 1 stays at 1, and tapping + at 99 stays at 99. The label is "Every 2 weeks".
  - **Monthly**: shows the cards from `monthlyOptions`, with "On day N" selected.
  - **Ends**: choosing "After" shows its stepper at 10, and choosing "On" shows a date input.
  - **Summary**: it updates and includes "Starts Thursday, October 8, 2026" when the start was adjusted.
  - **Start-date change**: the monthly options are refilled, and an explicit weekday choice is kept.
  - **Narrow screen**: at 320 px (`setNarrowViewport(true)`), every target has a min size of 44 px through the class contract.
  - **Keyboard**: arrow keys move within the radio groups, and Space toggles a weekday.
  - **axe**: no violations.
- [X] T018 [P] [US1] Extend `frontend/src/components/EventFormDialog.test.tsx`:
  - Creating with Weekly M/W/F sends `recurrence` in `createEvent`.
  - Editing a one-time event and choosing Daily sends `recurrence` in `updateEvent`, with no scope dialog (a one-time event).
  - Server `400` errors for `recurrence.*` show next to the right control.
  - Discarding unsaved repeat changes asks for confirmation, as for other fields (001 FR-012).
- [X] T019 [P] [US1] Extend `frontend/src/components/CalendarScreen.test.tsx`: the API mock returns month data with occurrences on M/W/F. All appear in the month, week, and day views, and saving a new series reloads the current view.

### Implementation for User Story 1

- [X] T020 [US1] Implement `backend/src/PersonalCalendar.Domain/Events/Recurrence.cs`, `static IEnumerable<CalendarItem> Expand(CalendarEvent series, LocalDate fromDate, LocalDate toDate)`, to pass T013. It follows data-model "Recurrence.Expand" steps 1–5, with the fast-forward from research S7 and `ScheduleResolver`'s `LenientResolver` for timed occurrences. Expand only from the series' own data. Exceptions (steps 3–4) are a no-op until T042, so leave that branch empty but in place.
- [X] T021 [US1] Extend `backend/src/PersonalCalendar.Domain/Events/CalendarEvent.cs`:
  - Add `SeriesRecurrence? Recurrence`, `IReadOnlyList<OccurrenceException> Exceptions` (empty for now), and `static Validated<CalendarEvent> CreateSeries(...)`.
  - `Update(..., SeriesRecurrence? recurrence)` sets or keeps the rule and normalizes the first occurrence. For timed events it sets `StartLocal` and `EndLocal` from the resolved local values. `RecurrenceTimeZone` = the entry zone when the series is created or its times or rule change.
  - Add `LocalDate SeriesFirstDate` and `LocalDate? SeriesLastDate`, computed with `Recurrence.Expand` for COUNT (at most 999) or from UNTIL, as the last occurrence's *end* date in the series zone.
  - Update `Rehydrate` to take the new fields.
- [X] T022 [US1] Extend `backend/src/PersonalCalendar.Infrastructure/Persistence/EventRowMapper.cs` to map the series columns. The rule is written with `RepeatRule.ToRRule` and read with `ParseRRule`. Extend `EfEventRepository.ListOverlappingAsync` with the series condition "`RecurrenceRule IS NOT NULL AND SeriesFirstDate <= toDate + 1 AND (SeriesLastDate IS NULL OR SeriesLastDate >= fromDate − 1)`" (research S7), and document it in `backend/src/PersonalCalendar.Application/Abstractions/IEventRepository.cs`.
- [X] T023 [US1] Add `backend/src/PersonalCalendar.Application/Events/RecurrenceInput.cs`: a record matching the contract's `recurrence` object, with `ToRule(LocalDate start)` giving a `Validated<RepeatRule>` whose error keys are prefixed `recurrence.`. Add `Recurrence` to `EventInput.cs`. Add `backend/src/PersonalCalendar.Application/Events/CalendarItems.cs`, `static IReadOnlyList<CalendarItem> Build(IEnumerable<CalendarEvent> events, LocalDate from, LocalDate to)`. It turns one-time events into one item each, and series into `Recurrence.Expand` items. Then:
  - Use it in `GetMonthView.cs` and `GetDaysView.cs`, replacing the T010 `FromEvent` mapping. Clip timed items to `[from, to)` as before.
  - Update `CreateEvent.cs` and `UpdateEvent.cs`, for a one-time event only (series edits come in US3), to pass the rule into the domain.
  - Extend `EventDetails.cs` and `EventMapping.ToDetails` with `Recurrence` (including `TimeZone`), `OccurrenceDate`, `SeriesStart`, `SeriesStartDate`, `IsException`, and `ExceptionCount`.

  Make T014 pass.
- [X] T024 [US1] Extend `backend/src/PersonalCalendar.Api/Endpoints/EventRequest.cs` to parse `recurrence`: `frequency`, `interval`, lowercase `weekdays`, `monthly`, and `end`. Unknown enum strings return `recurrence.frequency.invalid` (or `.invalid` on the matching field), and `until` must be `yyyy-MM-dd`. Configure the JSON options in `Program.cs` only if needed for nested camelCase. Make T015 pass.
- [X] T025 [P] [US1] Implement `frontend/src/lib/recurrence.ts` to pass T016:
  - `monthlyOptions(start: DateString)`;
  - `firstOccurrence(start, recurrence)`;
  - `describeRule(recurrence, start, locale)`, with `Intl.DateTimeFormat` weekday and month names and `Intl.ListFormat` for lists;
  - `defaultRecurrence(frequency, start)`.

  It uses only the zone-free helpers in `lib/dates.ts`.
- [X] T026 [US1] Implement `frontend/src/components/RepeatFields.tsx` to pass T017. Props are `{ value: Recurrence | null, startDate: DateString, onChange, errors }`. It has:
  - a native radio group styled as a segmented row;
  - an interval stepper with −/+ buttons and an `inputmode="numeric"` input;
  - weekday `<button aria-pressed>` toggles named in full;
  - monthly radio cards;
  - Ends radio cards with an inline date input or count stepper;
  - a summary `<p aria-live="polite">`, debounced to 500 ms.

  Add CSS to `frontend/src/index.css`: `.segmented`, `.weekday-toggle`, `.stepper`, and `.radio-card`, each at least 44 × 44 px, wrapping under 600 px, and fitting 320 px without sideways scrolling.
- [X] T027 [US1] Integrate `RepeatFields` into `frontend/src/components/EventFormDialog.tsx` below the date and time fields. It holds `recurrence` in `FormValues` and sends it in `createEvent` and `updateEvent`. It maps `recurrence.*` errors to the section, and counts repeat changes as unsaved changes (001 FR-012). Add the messages for the new codes to `frontend/src/lib/messages.ts`. Update `frontend/src/api/client.ts` only as needed to send the field. Make T018 pass.
- [X] T028 [US1] Make sure `frontend/src/components/CalendarScreen.tsx` reloads the current view after a series is created or converted, so it handles the multiple items with the same `id` that occurrences produce. Use `${id}:${occurrenceDate ?? ''}` as the React key wherever events are listed: `frontend/src/components/DayCell.tsx`, `TimeGrid.tsx`, `AllDayBars.tsx`, `WeekList.tsx`, and `DayOverflowDialog.tsx`. Make T019 pass.

**Checkpoint**: quickstart rows 1–6 and 13 pass. US1 is the MVP. Tapping an occurrence still opens a details dialog, but for series it is completed in US2.

---

## Phase 4: User Story 2 - Recognize and view a recurring event (Priority: P1)

**Goal**: A repeat icon in every view, and occurrence details that show that occurrence's date and time plus the repeat summary.

**Independent Test**: A weekly series and a one-time event on the same day. Only the occurrence shows the icon, and opening it shows its own date and the summary (spec US2).

### Tests for User Story 2 (write first, must fail) ⚠️

- [X] T029 [P] [US2] Extend `backend/tests/PersonalCalendar.Api.Tests/RecurrenceEndpointTests.cs`, the details part:
  - `GET /api/events/{seriesId}?timeZone=America/Chicago&occurrence=2026-10-21` → `200`, with `start` `2026-10-21T07:00:00-05:00`, `occurrenceDate`, `recurrence`, `seriesStart` `2026-10-12T07:00:00-05:00`, and `exceptionCount: 0`.
  - Missing `occurrence` on a series → `400 occurrence.required`.
  - `occurrence` on a one-time event → `400 occurrence.invalid`.
  - A date the rule doesn't produce (`2026-10-20`) → `404`.
- [X] T030 [P] [US2] Write `frontend/src/components/RepeatIcon.test.tsx`: it renders an `aria-hidden` SVG with no accessible name. Extend `frontend/src/lib/describe.test.ts` (create it if missing): `describeEvent` for a recurring summary ends with ", repeats" (FR-030), and a one-time summary doesn't.
- [X] T031 [P] [US2] Extend the component tests:
  - **`frontend/src/components/MonthView.test.tsx`**: on a wide screen, a recurring label shows the icon and its accessible name contains "repeats". On a narrow screen, markers have no icon and the day's count includes occurrences (spec US2 scenario 4).
  - **`TimeGrid.test.tsx`, `WeekList.test.tsx`, and `WeekView.test.tsx`** (all-day bar): the icon appears and the name contains "repeats".
- [X] T032 [P] [US2] Extend `frontend/src/components/EventDetailsDialog.test.tsx`: opened with `eventId` and `occurrenceDate`, it calls `getEvent(id, tz, occurrenceDate)`. It shows "Wednesday, October 21, 2026, 7:00 AM to 8:00 AM", the icon, and the summary "Weekly on Monday, Wednesday, and Friday". The summary text is in the accessible tree. A one-time event shows no summary. axe has no violations.

### Implementation for User Story 2

- [X] T033 [US2] Extend `backend/src/PersonalCalendar.Application/Events/GetEventDetails.cs` with an `occurrence` parameter. For a series, it requires one, checks that it's a produced and not deleted date (by expanding that single date), and returns the occurrence's schedule with series data. `occurrence` on a one-time event returns `occurrence.invalid`. Extend `backend/src/PersonalCalendar.Api/Endpoints/EventEndpoints.cs` `GET /{id}` with `string? occurrence`, which is parsed as `yyyy-MM-dd` or returns `occurrence.invalid`. Make T029 pass.
- [X] T034 [P] [US2] Create `frontend/src/components/RepeatIcon.tsx`, an inline 16 px SVG with two curved arrows, `aria-hidden="true"`, and `currentColor`. Update `frontend/src/lib/describe.ts` so `describeEvent` appends ", repeats" when `isRecurring`. Make T030 pass.
- [X] T035 [US2] Render `RepeatIcon` after the title for recurring events in `frontend/src/components/EventButton.tsx` (month labels), `TimeGrid.tsx` (hidden when the block is under 24 px tall), `AllDayBars.tsx`, and `WeekList.tsx`. Not in the narrow `DayCell` markers. Make T031 pass.
- [X] T036 [US2] Update `frontend/src/api/client.ts` `getEvent(id, timeZone, occurrenceDate?)` to add `&occurrence=`. Update `frontend/src/components/EventDetailsDialog.tsx` to take `occurrenceDate?`, and show the icon and `describeRule(details.recurrence, seriesStart date)` under the date line. Update `frontend/src/components/CalendarScreen.tsx` to pass `occurrenceDate` from the tapped `EventSummary` when it opens details. Make T032 pass.

**Checkpoint**: quickstart row 16 and the indicator parts of row 1 pass.

---

## Phase 5: User Story 3 - Change a single occurrence, or this and following (Priority: P2)

**Goal**: Save asks for the scope. "This event" creates an exception, "This and following" splits the series, and "All events" updates it. Impossible choices are hidden and refused (FR-016 and FR-016a).

**Independent Test**: Move one occurrence's time with "This event", and only it changes. Retitle from a later occurrence with "This and following", and earlier occurrences keep the old title (spec US3).

### Tests for User Story 3 (write first, must fail) ⚠️

- [X] T037 [P] [US3] Write `backend/tests/PersonalCalendar.Domain.Tests/SeriesScopeTests.cs`, the edit part (research S6):
  - **`EditOccurrence`**:
    - Changing the time of `2026-10-21` upserts a changed exception, and `Expand` shows the new time only on that date.
    - Changing its date to `2026-10-22` moves it, so it's absent on the 21st and present on the 22nd with `OriginalDate` the 21st.
    - It is refused with `scope.thisWithRepeatChange` when the rule input differs.
  - **`SplitAt`**:
    - With COUNT 10 at the 4th occurrence → the original has COUNT 3 and the tail has COUNT 7.
    - With Never at `2026-11-02` → the original has UNTIL `2026-11-01`.
    - Every 2 weeks split at an occurrence keeps the same dates after the split.
    - Split at the first occurrence → `SplitResult.IsWholeSeries`.
  - **`UpdateSeries`, text-only**: the title change propagates into changed exceptions while keeping their times, and deleted exceptions stay (FR-019).
  - **`UpdateSeries`, rule or time change**: clears all exceptions (FR-020).
  - **Version**: each operation increments `Version` once.
  - **FR-016a**: the start date changed with scope Following or All → `scope.dateChangeRequiresThis`. The start date *and* the rule changed → `scope.dateAndRepeatChanged`. Changing only the time or the end date doesn't count as a date change.
- [X] T038 [P] [US3] Extend `backend/tests/PersonalCalendar.Application.Tests/RecurringUseCaseTests.cs`, the edit part: `UpdateEvent` with `scope` and `occurrence`:
  - `this` → one exception stored.
  - `following` → the original is truncated and a new series is created through a single `ReplaceAsync` call. The result's `Id` is the new series.
  - `following` with only text changed moves exceptions on or after the split date to the new series.
  - `all` → an updated series.
  - Missing or invalid scope or occurrence → the codes in the HTTP contract table.
  - Stale version → `Conflict`.
  - **Atomicity**: when `ReplaceAsync` throws, nothing changes (FR-027). The fake repository must support injecting a failure.
- [X] T039 [P] [US3] Extend `backend/tests/PersonalCalendar.Api.Tests/RecurrenceEndpointTests.cs`, the edit part: every `PUT` row of the contract table returns the documented status and code. `PUT …&scope=following` returns a new `id`, and the month view shows "Gym" before and "Swim" from the split (quickstart row 8). Extend `backend/tests/PersonalCalendar.Api.Tests/SaveFailureTests.cs` with a failing split that leaves the original series unchanged.
- [X] T040 [P] [US3] Write `frontend/src/components/ScopeChoiceDialog.test.tsx` (contracts/ui-interaction.md "Scope choice"):
  - **Choices shown**: one case per row of the "Situation → Choices shown" table.
  - **Layout**: buttons are full width and at least 48 px tall (class contract).
  - **Focus**: it moves to the first choice on open and returns to the opener on close.
  - **Escape and Cancel**: both call `onCancel`.
  - **Warning step**: with `needsWarning`, choosing All shows the warning with "Save anyway" and "Back", and Back returns to the choices.
  - **axe**: no violations.
- [X] T041 [P] [US3] Extend `frontend/src/components/EventFormDialog.test.tsx` for series edits:
  - The form opens directly with the occurrence's date and time and the series rule (scenario 11).
  - **Save with only the time changed**: the dialog offers all three. Choosing "This event" calls `updateEvent(id, input, {occurrence, scope:'this'})`.
  - **Date changed**: only "This event" is offered.
  - **Repeat changed**: "This event" is hidden.
  - **Date and repeat changed**: no dialog, and the start date shows the FR-016a message.
  - **Rule change with `exceptionCount > 0`**: after choosing All, the warning text from the UI contract is shown.
  - **Cancel**: returns to the form with the edits kept.
  - **Announcements**: "Changed this event." and the others.

### Implementation for User Story 3

- [X] T042 [US3] Add `backend/src/PersonalCalendar.Domain/Events/OccurrenceException.cs` (data-model "OccurrenceException") and the domain methods `EditOccurrence`, `SplitAt`, and `UpdateSeries` in `CalendarEvent.cs`, which return `ValidationResult` or a `SplitResult`. Fill in the exception step (data-model steps 3–4) in `Recurrence.cs`. Make T037 pass, and keep T013 green.
- [X] T043 [US3] Extend persistence:
  - **`EventRowMapper.cs`**: maps exceptions to and from `OccurrenceExceptionRow`.
  - **`EfEventRepository.cs`**:
    - `GetAsync` includes exceptions.
    - `ListOverlappingAsync` also loads series that have a changed exception overlapping the range, by either exception index (research S7).
    - Add `ReplaceAsync(upserts, deletes, expected)`, which applies all changes and checks the version in one `SaveChangesAsync` (one transaction) and maps `DbUpdateConcurrencyException` to `ConcurrencyConflictException`.
  - **`IEventRepository.cs`** and the fake repository: add `ReplaceAsync`.
- [X] T044 [US3] Add `backend/src/PersonalCalendar.Application/Events/EditScope.cs` (`This`, `Following`, `All`). Extend `UpdateEvent.cs` with `(Guid id, EventInput input, LocalDate? occurrence, EditScope? scope)`. It validates the presence rules, detects "date changed" (the input start date differs from the occurrence's current date) and "rule changed" (the input rule differs from the series rule), and then calls the domain method for the scope. `Following` at the first occurrence acts as `All`. `following` persists with `ReplaceAsync`. Extend `EventEndpoints.cs` `PUT` with `string? occurrence` and `string? scope`. Make T038 and T039 pass.
- [X] T045 [P] [US3] Implement `frontend/src/components/ScopeChoiceDialog.tsx` on top of `Modal`. Props are `{ mode: 'edit' | 'delete', choices: EditScope[], warning?: (scope) => string | null, onChoose(scope), onCancel }`. It has stacked full-width buttons labeled "This event", "This and following events", and "All events", plus a Cancel button. The optional warning step has "Save anyway" (or "Delete anyway" in delete mode) and "Back". Add CSS `.scope-choice` (min-height 48 px) to `frontend/src/index.css`. Make T040 pass.
- [X] T046 [US3] Update `frontend/src/api/client.ts` `updateEvent(id, input, target?: {occurrence: DateString, scope: EditScope})` to add the query parameters. Update `frontend/src/components/EventFormDialog.tsx`:
  - For a series, it computes `dateChanged`, `repeatChanged`, and `timesChanged` against the loaded details.
  - It shows the FR-016a message when the date and repeat both changed.
  - Otherwise it opens `ScopeChoiceDialog` with the choices from the UI contract table, and the warning when `(repeatChanged || timesChanged) && exceptionCount > 0`.
  - It sends the update and announces the result.
  - It handles the `scope.*` server codes by showing the message on the form.

  Make T041 pass.

**Checkpoint**: quickstart rows 7–11 pass.

---

## Phase 6: User Story 4 - Delete a single occurrence, this and following, or the whole series (Priority: P2)

**Goal**: Delete asks for the scope and removes exactly the chosen occurrences.

**Independent Test**: A daily series of 10. Delete the 3rd with This, the 7th with Following, then delete with All. Each step removes exactly the right dates and survives a reload (spec US4).

### Tests for User Story 4 (write first, must fail) ⚠️

- [X] T047 [P] [US4] Extend `backend/tests/PersonalCalendar.Domain.Tests/SeriesScopeTests.cs`, the delete part:
  - `DeleteOccurrence` adds a deleted exception. Deleted occurrences still count toward COUNT: COUNT 10 minus 1 deleted shows 9, and the last date is unchanged.
  - Deleting the last visible occurrence returns `SeriesNowEmpty`.
  - `SplitAt` for delete removes exceptions on or after the date.
  - Following at the first occurrence returns `IsWholeSeries`.
- [X] T048 [P] [US4] Extend `RecurringUseCaseTests.cs` and `RecurrenceEndpointTests.cs` for `DELETE` with `occurrence` and `scope`:
  - Every row of the contract → `204` or its code.
  - `this` on the last remaining occurrence deletes the series, and a later `GET` → `404`.
  - `all` cascades the exceptions, so no orphan rows are left in `OccurrenceExceptions`. Check this with a direct `CalendarDbContext` query.
  - A stale version → `409`.
  - The results survive a new client (US4 scenario 7).
- [X] T049 [P] [US4] Extend `frontend/src/components/DeleteFlow.test.tsx`:
  - For a series, Delete opens `ScopeChoiceDialog` in delete mode with all three choices instead of the 001 `ConfirmDialog`.
  - Each choice calls `deleteEvent(id, version, {occurrence, scope})` and announces "Deleted this event.", "Deleted this and following events.", or "Deleted all events.".
  - Cancel deletes nothing.
  - A one-time event still uses the 001 confirm flow.

### Implementation for User Story 4

- [X] T050 [US4] Add `DeleteOccurrence` in `backend/src/PersonalCalendar.Domain/Events/CalendarEvent.cs`, and the delete variant of `SplitAt`. Make T047 pass.
- [X] T051 [US4] Extend `backend/src/PersonalCalendar.Application/Events/DeleteEvent.cs` with `(Guid id, int version, LocalDate? occurrence, EditScope? scope)`: `this` → upsert a deleted exception, or delete the series when it's empty; `following` → truncate (or delete all at the first occurrence); `all` → delete. Persist with `ReplaceAsync` or `DeleteAsync`. Extend `EventEndpoints.cs` `DELETE` with `occurrence` and `scope`. Make T048 pass.
- [X] T052 [US4] Update `frontend/src/api/client.ts` `deleteEvent(id, version, target?)`. Update `frontend/src/components/CalendarScreen.tsx` to use `ScopeChoiceDialog` (mode `delete`) for recurring details, and keep `ConfirmDialog` for one-time events. Reload the view and announce after a successful delete. Make T049 pass.

**Checkpoint**: quickstart row 12 passes.

---

## Phase 7: User Story 5 - Change or stop repeating for a whole series (Priority: P3)

**Goal**: Change the rule with All or Following, with the warning when exceptions would be discarded, and stop repeating.

**Independent Test**: A weekly Monday series changed to Mon and Thu with All adds Thursdays. Then "Does not repeat" with All leaves only the first occurrence, as a one-time event (spec US5).

### Tests for User Story 5 (write first, must fail) ⚠️

- [X] T053 [P] [US5] Extend `backend/tests/PersonalCalendar.Domain.Tests/SeriesScopeTests.cs`:
  - **`UpdateSeries`** from Monday to Mon and Thu → occurrences on both days from the series start.
  - **`StopRepeating()`** turns a series into a one-time event:
    - Its schedule is the first occurrence's, or that occurrence's changed-exception values if it has one.
    - The rule columns are cleared and the exceptions removed.
    - The version goes up by 1.
  - **Following plus "Does not repeat"** at `d` → the original is truncated before `d`, and `d` becomes a one-time event with its current values (FR-021).
- [X] T054 [P] [US5] Extend `RecurringUseCaseTests.cs` and `RecurrenceEndpointTests.cs`:
  - `PUT` with `recurrence: null` and `scope=all` returns details with `recurrence: null`. The month view shows only the first date, and `GET` without `occurrence` now works (a one-time event).
  - `scope=following` with `recurrence: null` creates a one-time event with a new id.
  - A rule change on a series with exceptions clears them (`exceptionCount: 0`).
- [X] T055 [P] [US5] Extend `frontend/src/components/EventFormDialog.test.tsx`:
  - Choosing "Does not repeat" on a series and Save → the choices are Following and All (repeat changed).
  - Choosing All shows "All events except the first will be removed.", and Following shows "This and following events will be removed, and this one kept as a single event."
  - "Save anyway" sends `recurrence: null`.

### Implementation for User Story 5

- [X] T056 [US5] Implement `StopRepeating()` and the Following and "Does not repeat" path in `backend/src/PersonalCalendar.Domain/Events/CalendarEvent.cs`, and handle `recurrence == null` for a series in `UpdateEvent.cs`: All → `StopRepeating`, and Following → truncate plus a new one-time event through `ReplaceAsync`. Make T053 and T054 pass.
- [X] T057 [US5] Update `frontend/src/components/EventFormDialog.tsx` and `ScopeChoiceDialog` usage so that the FR-021 warning texts appear when the new rule is "Does not repeat". Make T055 pass.

**Checkpoint**: quickstart row 9 (the warning part) and every US5 scenario pass.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [X] T058 [P] Extend `backend/tests/PersonalCalendar.Api.Tests/PerformanceTests.cs`: seed 5,000 one-time events plus 200 series that never end (a mix of daily, weekly with 3 days, monthly by day and by position, and yearly, in `America/Chicago`). Assert `GET /api/calendar/month` and `/api/calendar/days?count=7` each take 300 ms or less, measured as the median of 5 runs after a warm-up (SC-004).
- [X] T059 [P] Add a cross-zone regression test to `backend/tests/PersonalCalendar.Application.Tests/RecurringUseCaseTests.cs`: a series created in `America/Chicago` and viewed with `timeZone=Asia/Kolkata` and `Australia/Adelaide` places each occurrence at the converted instant, which can be on the next local day. Its `occurrenceDate` stays the Chicago original date (research S4).
- [X] T060 [P] Run axe on the form with every repeat mode open and on the scope dialog in both modes in `frontend/src/components/RepeatFields.test.tsx` and `ScopeChoiceDialog.test.tsx`, if not already covered. Fix any violations.
- [X] T061 Update `README.md` (feature list) with one line about recurring events, and confirm `specs/003-recurring-events/quickstart.md` matches the final behavior. Update it if names drifted.
- [ ] T062 Run the full quickstart: the automated checks, then manual rows 1–17 at 375 × 667 touch emulation first and then in a wide window, with the device zone set to `America/Chicago` and then `America/New_York` (row 14). Record the results in the PR description, including the keyboard-only and screen-reader-label checks required by the constitution's merge gate.

---

## Phase 9: Events that run past midnight (FR-032, research S14)

**Goal**: A timed event shows on the day it starts, at its start time. On each following day it covers, it shows from 12:00 AM and is ordered by that time among the day's events.

**Independent Test**: Create a one-time event from 6:00 PM on Oct 8 to 2:00 AM on Oct 9, plus a 12:00 AM and an 8:00 AM event on Oct 9. Check the month view, the phone week list, and the time grid (quickstart row 17).

### Tests (write first, must fail) ⚠️

- [X] T063 [P] Add `TimedEvent_ContinuingFromPreviousDay_IsOrderedAsStartingAtMidnight` to `backend/tests/PersonalCalendar.Domain.Tests/MonthGridTests.cs`. On Oct 14, a 17:00 event comes before "Late" (18:00–01:00). On Oct 15, the order is "A at midnight" (00:00, same time, title first), then "Late", then the 08:00 event.
- [X] T064 [P] Test `continuesFromPreviousDay` in `frontend/src/lib/describe.test.ts`: false on the start day and true on the next day (Chicago). False in `Asia/Kolkata`, where the same instant starts on the 9th. False for all-day events.
- [X] T065 [P] In `frontend/src/components/MonthView.test.tsx`: a 6:00 PM–2:00 AM event shows "6:00 PM" on the 14th and "12:00 AM" (not "6:00 PM") on the 15th. On the 15th its accessible name ends with ", continues from the previous day". In `WeekList.test.tsx`: the 15th lists "12:00 AM – 1:00 AM Late show" with the same suffix.

### Implementation

- [X] T066 `EventOrdering.Order(events, dayStart?)` orders a timed event that began before `dayStart` as if it started at `dayStart`. `MonthGrid.Build` passes `zone.AtStartOfDay(date)` (`backend/src/PersonalCalendar.Domain/Calendar/`).
- [X] T067 Add `continuesFromPreviousDay` and `describeEventOnDay` to `frontend/src/lib/describe.ts`. `EventButton` takes the cell's `date` and shows 12:00 AM on continued days. `DayCell` and `DayOverflowDialog` pass it. `WeekList` passes each row's `TimedSegment` and shows the segment's times when `continuesBefore` is true.
- [ ] T068 Run quickstart row 17 at 375 × 667 touch emulation, then in a wide window.

**Checkpoint**: Placement is unchanged (001 and 002 midnight tests still pass). Only the label and the order on the days after the first change.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies. T003 needs T008's types, so do it right after T008, or include it in T008.
- **Foundational (Phase 2)**: depends on Setup and blocks every story.
- **US1 (Phase 3)**: depends on Foundational. This is the MVP.
- **US2 (Phase 4)**: depends on US1, because it needs series to exist (T021–T024). The frontend icon tasks (T030, T031, T034, T035) only need T008 and fixtures, so they can start right after Foundational.
- **US3 (Phase 5)**: depends on US1 and on US2's occurrence details (T033 and T036), because editing starts from the details of an occurrence.
- **US4 (Phase 6)**: depends on US1, US2 (T033 and T036), and US3's persistence and scope types (T042–T045). It is independent of US3's form logic (T046).
- **US5 (Phase 7)**: depends on US3.
- **Polish (Phase 8)**: depends on every story being done.
- **Past midnight (Phase 9)**: depends only on Foundational (`CalendarItem` layouts). It applies to one-time events and occurrences alike.

### Within each phase

- Test tasks come before implementation and MUST fail first (Principle I). This is mandatory for T004, T005, T013, T016, T037, T047, and T053, which cover date/time logic.
- Backend order: Domain → Infrastructure → Application → Api. Frontend order: types → lib → components → `CalendarScreen` wiring.
- These tasks edit the same file in sequence, so they are not [P] with each other:
  - `CalendarEvent.cs`: T021 → T042 → T050 → T056
  - `Recurrence.cs`: T020 → T042
  - `UpdateEvent.cs`: T023 → T044 → T056
  - `EfEventRepository.cs` and `EventRowMapper.cs`: T011 → T022 → T043
  - `EventEndpoints.cs`: T033 → T044 → T051
  - `RecurrenceEndpointTests.cs`: T015 → T029 → T039 → T048 → T054
  - `RecurringUseCaseTests.cs`: T014 → T038 → T048 → T054 → T059
  - `SeriesScopeTests.cs`: T037 → T047 → T053
  - `EventFormDialog.tsx`: T027 → T046 → T057
  - `EventFormDialog.test.tsx`: T018 → T041 → T055
  - `CalendarScreen.tsx`: T028 → T036 → T052
  - `client.ts`: T027 → T036 → T046 → T052
  - `index.css`: T026 → T045

### Parallel Opportunities

- **Setup**: T002 and T003, once T008 is in.
- **Foundational**: T004 and T005 together, and T008 alongside the backend work. T006 → T007, and T009 → T010, are separate tracks, and T011 → T012 is a third.
- **US1 tests**: T013–T019 can all be written together.
- **US1 implementation**: backend track T020 → T021 → T022 → T023 → T024, and frontend track T025 → T026 → T027 → T028. The two tracks are independent until manual checking.
- **US2**: T029–T032 together, and T034 alongside T033.
- **US3**: T037–T041 together, and T045 (the dialog) alongside the backend T042–T044.
- **US4 and US5**: each story's test tasks are [P].
- **Polish**: T058–T060 in parallel.

---

## Parallel Example: User Story 1

```text
# Write all US1 tests together (they must fail):
Task: T013 RecurrenceExpandTests.cs     (backend/tests/PersonalCalendar.Domain.Tests)
Task: T014 RecurringUseCaseTests.cs     (backend/tests/PersonalCalendar.Application.Tests)
Task: T015 RecurrenceEndpointTests.cs   (backend/tests/PersonalCalendar.Api.Tests)
Task: T016 recurrence.test.ts           (frontend/src/lib)
Task: T017 RepeatFields.test.tsx        (frontend/src/components)
Task: T018 EventFormDialog.test.tsx     (frontend/src/components)
Task: T019 CalendarScreen.test.tsx      (frontend/src/components)

# Then two tracks:
Track A (backend):  T020 Expand → T021 CalendarEvent series → T022 persistence → T023 use cases → T024 request parsing
Track B (frontend): T025 lib/recurrence → T026 RepeatFields → T027 form integration → T028 keys + reload
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1: Setup (T001–T003)
2. Phase 2: Foundational (T004–T012). One-time events behave exactly as before, and the schema is migrated.
3. Phase 3: US1 (T013–T028)
4. **Stop and check**: quickstart rows 1–6 and 13, on a phone-sized emulator first and then on a wide window.

### Incremental Delivery

1. Setup and Foundational give a migrated schema, `RepeatRule`, and layouts that take `CalendarItem`.
2. Adding US1 lets you create series and see them everywhere. This is the MVP.
3. Adding US2 adds the repeat icon and occurrence details, completing the P1 scope.
4. Adding US3 adds editing with This, Following, and All.
5. Adding US4 adds deleting with This, Following, and All.
6. Adding US5 adds rule changes with warnings and stopping a series.
7. Polish adds performance, the cross-zone regression test, documentation, and the full quickstart.

## Notes

- [P] tasks touch different files and have no unfinished dependencies.
- Commit after each task or logical group, on branch `003-recurring-events`.
- Never weaken an existing 001 or 002 test to make a change pass. If a 001 or 002 contract changes, record it in the PR.
- Don't add keyboard shortcuts or gesture-only interactions (design priority).
