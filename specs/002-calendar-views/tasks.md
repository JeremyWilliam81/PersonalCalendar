---

description: "Task list for 002 Calendar Views and Navigation"
---

# Tasks: Calendar Views and Navigation

**Input**: Design documents from `/specs/002-calendar-views/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/](./contracts/), [quickstart.md](./quickstart.md)

**Tests**: Required. Constitution Principle I makes test-first mandatory for all date/time logic: the domain timeline, all-day lanes, period arithmetic, `todayIn`, `newEventAt`, and parsing dates in the address. Feature 001 also wrote component tests for every UI story, and this list keeps doing that. Every test task MUST be written and seen to fail before its implementation task.

**Organization**: Tasks are grouped by user story so each story can be implemented and tested independently.

**Design priority** (spec Clarifications, plan): touch and pointer first. Every UI task lists its tap/click behavior first and keeps keyboard work to the Constitution IV baseline. Do not add keyboard shortcuts.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on unfinished tasks)
- **[Story]**: Which user story this task belongs to (US1–US5)
- Every task names its exact file paths

## Path Conventions

- Backend: `backend/src/PersonalCalendar.{Domain,Application,Api}/…`, tests in `backend/tests/PersonalCalendar.{Domain,Application,Api}.Tests/…`
- Frontend: `frontend/src/{lib,hooks,components,api,test}/…`, with tests colocated as `*.test.ts[x]`
- Date strings in the frontend are always `yyyy-MM-dd` (`DateString`), and all zone-free arithmetic stays in `frontend/src/lib/dates.ts` (001 R5, research V2)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Confirm a green baseline and add the shared test helpers.

- [X] T001 Confirm the baseline on branch `002-calendar-views`: run `dotnet test backend/PersonalCalendar.slnx`, `npm test --prefix frontend` and `npm run lint --prefix frontend`. All must pass before any change. Record any pre-existing failure in the PR description, and don't fix unrelated code here.
- [X] T002 [P] Create `frontend/src/test/viewport.ts`, which exports `setNarrowViewport(narrow: boolean)`. It installs a `window.matchMedia` stub where `(max-width: 599px)` matches only when `narrow` is true, and where calling it again fires the registered `change` listeners. Call `setNarrowViewport(false)` in `frontend/src/test/setup.ts` (before each test and in `afterEach`), so the default is a wide screen.
- [X] T003 [P] Create the folder `frontend/src/hooks/` with a placeholder `frontend/src/hooks/README.md`: "Small React hooks with no dependencies (plan.md). One hook per file with a colocated test." Delete the placeholder once the first hook lands.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: View state, period arithmetic, today in the device zone, period titles, the shared header, detecting narrow screens, and a `CalendarScreen` that holds a `ViewState`. Every story depends on these.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Tests first (Principle I) ⚠️

- [X] T004 [P] Extend `frontend/src/lib/dates.test.ts` with these cases:
  - **`isValidDateString(s)`**:
    - Accepts `2026-10-14` and `2028-02-29`.
    - Rejects `2027-02-29`, `2026-02-30`, `2026-13-01`, `2026-1-5`, `''`, and `2026-10-14T00:00`.
  - **`isInSupportedRange(date)`**:
    - True for `1900-01-01` and `2199-12-31`.
    - False for `1899-12-31` and `2200-01-01`.
  - **`MIN_DATE` and `MAX_DATE`**: equal `'1900-01-01'` and `'2199-12-31'`.
  - **`addDays`**: `addDays('2028-02-28', 1) === '2028-02-29'`.
  - **`addMonthsClamped`**: `addMonthsClamped('2028-01-31', 1) === '2028-02-29'`.
- [X] T005 [P] Write `frontend/src/lib/viewState.test.ts` for `periodOf` and `stepPeriod` (data-model "ViewState"):
  - **`periodOf`**:
    - `periodOf({view:'day', date:'2026-10-14'})` gives `{first:'2026-10-14', last:'2026-10-14'}`.
    - `week` gives `2026-10-11`..`2026-10-17`.
    - `week` for `2026-12-30` gives `2026-12-27`..`2027-01-02`.
    - `month` for `2028-02-10` gives `2028-02-01`..`2028-02-29`.
  - **`stepPeriod` (FR-009)**:
    - day +1 from `2028-02-28` gives `2028-02-29`.
    - week +1 from `2026-12-30` gives `2027-01-06`.
    - month +1 from `2026-01-31` gives `2026-02-28`.
    - month −1 from `2026-03-31` gives `2026-02-28`.
    - month +1 from `2026-03-31` gives `2026-04-30`.
  - **`stepPeriod` at the range edges (FR-012)**: it returns `null` when the result would fall outside `1900-01-01`..`2199-12-31`:
    - day −1 from `1900-01-01`
    - week +1 from `2199-12-30`
    - month +1 from `2199-12-15`
  - **At the start of the range**: week −1 from `1900-01-10` gives `1900-01-03`, and week −1 from `1900-01-03` gives `null`, because the result `1899-12-27` is out of range. The rule is simply that the stepped selected date must be in range. A week may still *show* days before 1900-01-01 or after 2199-12-31 (for example, Sunday 1899-12-31).
- [X] T006 [P] Write `frontend/src/lib/today.test.ts` for `todayIn(timeZone: string, now: Date): DateString` (research V2). Every case uses an injected instant:
  - `2026-10-15T04:30:00Z` in `America/Chicago` gives `2026-10-14`.
  - The same instant in `Asia/Kolkata` gives `2026-10-15`.
  - `2026-10-14T13:29:00Z` in `Australia/Adelaide` (+10:30) gives `2026-10-14`, and `13:30Z` gives `2026-10-15`.
  - `2026-11-01T05:30:00Z` in `America/Chicago` gives `2026-11-01`. This is the fall-back day.
- [X] T007 [P] Extend `frontend/src/lib/format.test.ts` (en-US) for `formatPeriodTitle(view, date)` (contracts/ui-interaction "Shared header"):
  - day `2026-10-14` gives `"Wednesday, October 14, 2026"`.
  - week `2026-10-14` gives `"October 11 – 17, 2026"`. Compare after normalizing the thin and narrow no-break spaces that `formatRange` emits.
  - week `2026-12-30` gives a string containing both `2026` and `2027`.
  - week `2026-10-28` gives a string containing both `October` and `November`.
  - month gives `"October 2026"`.
- [X] T008 [P] Write `frontend/src/hooks/useNarrowScreen.test.ts`. With `setNarrowViewport(true)` from T002, the hook returns `true`. Calling `setNarrowViewport(false)` re-renders with `false` without remounting (research V6).

### Implementation

- [X] T009 Add to `frontend/src/lib/dates.ts`:
  - `MIN_DATE = '1900-01-01'` and `MAX_DATE = '2199-12-31'`.
  - `isValidDateString`: a strict `^\d{4}-\d{2}-\d{2}$` check plus a round-trip through `Date.UTC`.
  - `isInSupportedRange`.

  Keep the existing UTC-only approach (no zones). Makes T004 pass.
- [X] T010 Create `frontend/src/lib/viewState.ts`:
  - `type ViewType = 'day' | 'week' | 'month'`.
  - `interface ViewState { view: ViewType; date: DateString }`.
  - `periodOf(state)`.
  - `stepPeriod(state, delta: 1 | -1): ViewState | null`, using `addDays` and `addMonthsClamped` and clamping into range as T005 specifies.
  - `VIEW_TYPES` as a readonly array.

  Makes T005 pass.
- [X] T011 [P] Create `frontend/src/lib/today.ts`, which exports `todayIn(timeZone, now)`. It uses `Intl.DateTimeFormat('en-US', { timeZone, year:'numeric', month:'2-digit', day:'2-digit' }).formatToParts`, and has no offset arithmetic. Makes T006 pass.
- [X] T012 [P] Add `formatPeriodTitle(view: ViewType, date: DateString): string` to `frontend/src/lib/format.ts`:
  - **day**: `formatFullDate`.
  - **week**: `Intl.DateTimeFormat(getLocale(), { month:'long', day:'numeric', year:'numeric', timeZone:'UTC' }).formatRange(first, last)` over `periodOf`.
  - **month**: the existing `formatMonthTitle`.

  Run the result through the existing `normalize`. Makes T007 pass.
- [X] T013 [P] Create `frontend/src/hooks/useNarrowScreen.ts`. It uses `matchMedia('(max-width: 599px)')` through `useSyncExternalStore`, which subscribes to `change`. Delete `frontend/src/hooks/README.md`. Makes T008 pass.
- [X] T014 Create `frontend/src/components/ViewHeader.tsx` (contracts/ui-interaction "Shared header"), taking over the header from `frontend/src/components/MonthView.tsx`.
  - **Props**: `state: ViewState`, `titleId`, `canGoPrevious`, `canGoNext`, `onPrevious`, `onNext`, `onToday`, `onNewEvent`.
  - **Contents**:
    - An `<h2 id={titleId} tabIndex={-1}>` showing `formatPeriodTitle`.
    - Previous and Next icon buttons with `aria-label` "Previous day/week/month" and "Next …" for `state.view`, set to `disabled` when they can't be used.
    - A **Today** button.
    - A **New event** primary button.
  - **Slots for later stories**: leave an `actions` render slot so the view switcher (US1) and Go to date (US2) can be inserted without restructuring.
  - **Header CSS in `frontend/src/index.css`**: every header button has `min-height: 44px; min-width: 44px`. Below 599 px the header wraps into two rows (title and switcher, then the navigation and New event).
- [X] T015 Update `frontend/src/components/MonthView.tsx` so it no longer renders its own header. It receives `titleId` (for the grid's `aria-labelledby`) from its parent. Update `frontend/src/components/MonthView.test.tsx`: tests that clicked the header buttons now render `ViewHeader` alongside `MonthView` through a small test wrapper. All 001 MonthView tests must still pass, except the "Enter or Space creates" test, which US3 (T060) changes.
- [X] T016 Refactor `frontend/src/components/CalendarScreen.tsx`:
  - **View state**: hold `const [state, setState] = useState<ViewState>({ view: 'month', date: todayIn(timeZone, new Date()) })`. This replaces `focusedDate`, and `state.date` is now the selected date (FR-002).
  - **Month data**: derive the month request from `state.date`.
  - **Header**: render `ViewHeader` above the active view. Previous and Next call `stepPeriod`, and Today sets `{ view, date: todayIn(...) }`.
  - **Announcements**: after a period change, announce `formatPeriodTitle` (FR-025).
  - **Keep from 001**: the dialog state, reload after save and delete, and the conflict handling.

  The month view must look and behave exactly as before this task. Run the full frontend suite to confirm.

**Checkpoint**: the foundation is ready. The app still shows only the month view, now driven by `ViewState`, and all tests pass.

---

## Phase 3: User Story 1 - Switch between day, week, and month views (Priority: P1) 🎯 MVP

**Goal**: Day, Week, and Month toggle buttons, with views that show the period containing the selected date:
- **Day view**: a time grid.
- **Week view**: a time grid on wide screens and seven stacked sections on narrow screens.

Events are placed by elapsed time from the real start of the day, overlapping events sit side by side, and all-day events have their own area.

**Independent Test**: Seed the quickstart events. Switch Month → Week → Day → Month on a wide and a narrow viewport. Each view shows the right period and events, and the selected date never changes (quickstart rows 2, 4, 5, and 9).

### Tests for User Story 1 (write first, must fail) ⚠️

- [X] T017 [P] [US1] Write `backend/tests/PersonalCalendar.Domain.Tests/DayTimelineTests.cs` for `DayTimeline.Build(date, zone, events)` (data-model "DayTimeline", research V3 and V4). Use explicit zones and no clock.
  - **Day length and hour marks**:
    - A normal day in `America/Chicago` (2026-10-14): `LengthMinutes == 1440`, 24 `HourMarks` at offsets 0, 60, … 1380 with labels 00:00 … 23:00.
    - Spring-forward, 2027-03-14 in `America/Chicago`: `LengthMinutes == 1380`. There is no mark labelled 02:00, and the mark labelled 03:00 is at offset 120.
    - Fall-back, 2026-11-01 in `America/Chicago`: `LengthMinutes == 1500`. There are two marks labelled 01:00, at offsets 60 and 120, and the mark labelled 02:00 is at offset 180.
    - `Australia/Lord_Howe` on its DST start day, 2026-10-04: `LengthMinutes == 1410`.
    - `Australia/Adelaide` on 2026-10-04: `LengthMinutes == 1380`.
    - `Asia/Kolkata` on 2026-10-14: 1440 minutes, and `DayStart` is `2026-10-13T18:30:00Z`.
  - **Segments**:
    - An event from 09:00 to 10:30 gives `OffsetMinutes 540`, `DurationMinutes 90`, and neither `ContinuesBefore` nor `ContinuesAfter`.
    - An event from 22:00 on Oct 14 to 01:00 on Oct 15:
      - On Oct 14 it gives offset 1320, duration 120, and `ContinuesAfter`.
      - On Oct 15 it gives offset 0, duration 60, and `ContinuesBefore`.
    - An event ending exactly at midnight on Oct 15 does **not** appear on Oct 15.
    - On the fall-back day, an event from 01:30 at the first occurrence (`-05:00`) to 01:30 at the second occurrence (`-06:00`) gives offset 90 and duration 60.
    - A 3-minute event gives `DurationMinutes 3`. A 30-second event gives `DurationMinutes 1`, because the duration is rounded up and is at least 1.
    - An event on `2028-02-29` appears on that date only.
  - **Columns**:
    - Dentist (09:00–10:30), Standup (09:30–09:45), and Lunch (09:15–11:00) form one cluster with `ColumnCount 3`. Dentist is column 0, Lunch is column 1, and Standup is column 2.
    - Two events that only touch, 10:00–11:00 and 11:00–12:00, each have `ColumnCount 1`.
    - Clusters A(09–10), B(09:30–10:30), and C(10:45–11) give A and B `ColumnCount 2` and C `ColumnCount 1`.
  - **Order**: by offset, then the longer duration first, then the title ignoring case, then the id. Assert that the order is the same every time.
  - **All-day**: all-day events covering the date appear in `AllDay` in the 001 R6 order and never in `Timed`.
- [X] T018 [P] [US1] Write `backend/tests/PersonalCalendar.Domain.Tests/AllDayLanesTests.cs` for `AllDayLanes.Build(first, dayCount, events)` (data-model "AllDayLanes", research V5):
  - **Inside the week**: Trip 2026-10-16..2026-10-20 in the week starting 2026-10-11 gives `StartIndex 5`, `Span 2`, `ContinuesAfter true`, `ContinuesBefore false`.
  - **Starting before the week**: an event 2026-10-09..2026-10-12 gives `StartIndex 0`, `Span 2`, `ContinuesBefore true`.
  - **Lanes**: two overlapping bars get lanes 0 and 1. A third bar that doesn't overlap the lane-0 bar reuses lane 0.
  - **Year boundary**: in the week starting 2026-12-27, an event 2026-12-31..2027-01-01 gives `StartIndex 4`, `Span 2`.
  - **Invariants**: for every result, `0 ≤ StartIndex`, `StartIndex + Span ≤ dayCount`, `Span ≥ 1`, and no two bars in the same lane share a day.
- [X] T019 [P] [US1] Write `backend/tests/PersonalCalendar.Application.Tests/GetDaysViewTests.cs` using `Fakes/InMemoryEventRepository` and NodaTime `FakeClock`:
  - **Day request**: `count=1` returns 1 day with `AllDayBars` empty.
  - **Week request**: `count=7` from `2026-10-11` returns 7 days, with Trip in `AllDayBars`, and Trip in each day's `AllDay` for the 16th and 17th.
  - **Today and now**: `Today` and `Now` come from the `FakeClock` in the zone, and `IsToday` is set only on the matching day.
  - **Validation codes**:
    - `timeZone.unknown` for `"Mars/Base"`.
    - `start.invalid` for `"2026-02-30"` and for `null`.
    - `start.outOfRange` for `0000-12-31` and for `9999-01-01`, which are technical limits matching the month endpoint (years 1–9998). Also assert that `1899-12-31` with `count=7` is *accepted*, because the 1900–2199 navigation range is a UI rule and a week may show days outside it.
    - `count.invalid` for `0`, `2`, and `8`.
- [X] T020 [P] [US1] Write `backend/tests/PersonalCalendar.Api.Tests/DaysEndpointTests.cs` with `ApiFactory` (contracts/http-api "GET /api/calendar/days"):
  - **Response shape**: seed 3 events, then `GET /api/calendar/days?timeZone=America/Chicago&start=2026-10-11&count=7` returns `200`. The JSON has camelCase fields, `hourMarks[].label` in `"HH:mm"`, `dayStart` and `dayEnd` with offsets, `timed[].event` as the full `EventSummary`, and `allDayBars[]`.
  - **Fall-back day**: `start=2026-11-01&count=1` returns `lengthMinutes 1500` and two `"01:00"` labels.
  - **Errors**: an unknown zone returns `400` with `type: "validation"` and `errors.timeZone: ["timeZone.unknown"]`, and `count=3` returns `errors.count: ["count.invalid"]`.
- [X] T021 [P] [US1] Write `frontend/src/components/TimeGrid.test.tsx` with a `DaysView` fixture (add `fallBackDay()` and `octoberWeek()` builders to `frontend/src/test/fixtures.ts`):
  - **Placement**: an event block with offset 540 and duration 90 has `style.top === '432px'` and `style.height === '72px'` (0.8 px per minute).
  - **Minimum height**: a 3-minute block is at least `24px` tall.
  - **Columns**: a block with `column 1, columnCount 3` has `left ≈ 33.33%` and `width ≈ 33.33%`.
  - **Labels**: the fall-back fixture renders two "1 AM" labels and a 1200 px-tall column (1500 × 0.8).
  - **Accessible names**: each block is a button named with the 001 full form, e.g. "Dentist, Wednesday, October 14, 2026, 9:00 AM to 10:30 AM".
  - **Clipped segments**: a segment with `continuesAfter` has `data-continues-after`.
  - **Crowded clusters**: a cluster with `columnCount ≥ 3` renders a "+3" chip named "3 events between 9:00 AM and 11:00 AM on Wednesday, October 14, 2026". Clicking it opens the event-list dialog with all 3 events.
  - **Accessibility**: an axe pass.
- [X] T022 [P] [US1] Write `frontend/src/components/WeekView.test.tsx` (wide):
  - **Columns**: 7 columns with headings "Sun 11" … "Sat 17". The headings form a one-row `role="grid"` with exactly one heading in the Tab order.
  - **Keyboard**: → and ← move focus between headings. → from Saturday calls `onMoveDate('2026-10-18', /*leavesPeriod*/ true)`.
  - **All-day bar**: Trip renders as one element spanning grid columns 6–7, with a squared-off right edge and the visually hidden text "continues into the next week".
  - **Opening events**: clicking an event block calls `onOpenEvent(id)`.
  - **Accessibility**: an axe pass.
- [X] T023 [P] [US1] Write `frontend/src/components/WeekList.test.tsx` (narrow, FR-004a):
  - **Sections**: 7 `<section>` elements, Sunday to Saturday. Each heading is a button named with the full date plus ", open in day view".
  - **Event rows**: rows are listed with all-day first (labeled "All day"), then timed events with their range. Each row is a button that calls `onOpenEvent`.
  - **Empty days**: a day with no events shows "No events".
  - **Opening a day**: clicking the "Thursday, October 15" heading calls `onOpenDay('2026-10-15')`.
  - **Accessibility**: an axe pass.
- [X] T024 [P] [US1] Write `frontend/src/components/DayView.test.tsx`:
  - **Layout**: it renders the all-day area above a single `TimeGrid` column.
  - **Tab order**: all-day events first, then timed events in time order.
  - **Opening events**: clicking a block opens its details through `onOpenEvent`.
  - **Accessibility**: an axe pass.
- [X] T025 [P] [US1] Write `frontend/src/components/CalendarScreen.test.tsx` with `stubApi` (add `getDays` to `stubApi` in `frontend/src/test/fixtures.ts`, returning the T021 fixtures):
  - **Day button**: on the month view with selected date 2026-10-14, pressing **Day** calls `getDays(tz, '2026-10-14', 1)` and shows "Wednesday, October 14, 2026".
  - **Week button**: **Week** calls `getDays(tz, '2026-10-11', 7)`.
  - **Month button**: **Month** calls `getMonth(tz, 2026, 10)`.
  - **Pressed state**: the pressed button has `aria-pressed="true"`.
  - **Narrow week**: with `setNarrowViewport(true)`, Week renders `WeekList`. Switching back to wide renders `WeekView` without a new `getDays` call and without changing the selected date.
  - **Announcement**: after switching, the live region contains the period title.
  - **Saving keeps the view**: saving an event from the day view reloads `getDays` and stays on the day view (FR-006).

### Implementation for User Story 1

- [X] T026 [P] [US1] Create the domain records in `backend/src/PersonalCalendar.Domain/Calendar/`, matching data-model.md exactly:
  - `HourMark.cs`: `HourMark(int OffsetMinutes, LocalTime Label)`.
  - `TimedSegment.cs`: `TimedSegment(CalendarEvent Event, int OffsetMinutes, int DurationMinutes, bool ContinuesBefore, bool ContinuesAfter, int Column, int ColumnCount)`.
  - `DayTimelineResult.cs`: `DayTimelineResult(LocalDate Date, Instant DayStart, Instant DayEnd, int LengthMinutes, IReadOnlyList<HourMark> HourMarks, IReadOnlyList<CalendarEvent> AllDay, IReadOnlyList<TimedSegment> Timed)`.
  - `AllDayBar.cs`: `AllDayBar(CalendarEvent Event, int StartIndex, int Span, int Lane, bool ContinuesBefore, bool ContinuesAfter)`.
- [X] T027 [US1] Implement `backend/src/PersonalCalendar.Domain/Calendar/DayTimeline.cs`, a pure static class:
  - **Day bounds**: `DayStart = zone.AtStartOfDay(date).ToInstant()` and `DayEnd = zone.AtStartOfDay(date.PlusDays(1)).ToInstant()`.
  - **Hour marks**: walk from `DayStart` to `DayEnd`, emitting a mark at each instant whose local time in `zone` has minute 0 and second 0. To handle 30-minute DST shifts, step 15 minutes at a time from `DayStart` and keep instants where `LocalTime.Minute == 0`. Do not use hand-written offset math.
  - **Segments**: clip each timed event to `[DayStart, DayEnd)`. Exclude it when `End <= DayStart || Start >= DayEnd`. Compute `OffsetMinutes` with floor and `DurationMinutes` with ceiling, at least 1.
  - **Columns**: pack per research V4.
  - **All-day**: reuse the ordering from `MonthGrid` by extracting its private `Order` into an `internal static` helper in `backend/src/PersonalCalendar.Domain/Calendar/EventOrdering.cs` and calling it from both classes.

  Makes T017 pass without changing `MonthGridTests`.
- [X] T028 [P] [US1] Implement `backend/src/PersonalCalendar.Domain/Calendar/AllDayLanes.cs`. Order the events with `EventOrdering`. Clip to `[first, first + dayCount − 1]`, and assign each bar the lowest lane that is free on every covered index. Use only plain `LocalDate` arithmetic. Makes T018 pass.
- [X] T029 [US1] Create `backend/src/PersonalCalendar.Application/Events/DaysViewModel.cs` (`DaysViewModel`, `TimelineDayModel`, `HourMarkModel`, `TimedSegmentModel`, `AllDayBarModel`, exactly as in data-model.md, reusing `EventSummary`). Then create `backend/src/PersonalCalendar.Application/Events/GetDaysView.cs` with `HandleAsync(string? start, int? count, string? timeZone, CancellationToken)`:
  - **Validation**: in the data-model order (`timeZone.unknown`, `start.invalid`, `start.outOfRange` for "any day in the range has a year outside 1–9998", and `count.invalid` for "not 1 or 7"). Parse with `LocalDatePattern.Iso`.
  - **Loading**: one `ListOverlappingAsync(dayStart(first), dayEnd(last), first, last)` call.
  - **Building**: `DayTimeline.Build` for each day, `AllDayLanes.Build` when `count == 7`, and `EventMapping.ToSummary` for every event.
  - **Today and now**: `Today` and `Now` come from `IClock` in the zone.

  Register it in `backend/src/PersonalCalendar.Application/DependencyInjection.cs`. Makes T019 pass.
- [X] T030 [US1] Map `GET /api/calendar/days` in `backend/src/PersonalCalendar.Api/Endpoints/CalendarEndpoints.cs` with the binding `(string? start, int? count, string? timeZone, GetDaysView useCase, CancellationToken ct)` and `ProblemResults.From(result, Results.Ok)`. Make sure `LocalTime` is serialized as `"HH:mm"`, using a pattern converter for `HourMarkModel.Label` if the default NodaTime converter emits seconds. Makes T020 pass.
- [X] T031 [P] [US1] Add to `frontend/src/api/types.ts` the types `HourMark`, `TimedSegment`, `AllDayBar`, `TimelineDay`, and `DaysView`, matching contracts/http-api.md. Add `getDays(timeZone, start, count: 1 | 7)` to the `CalendarApi` interface and `httpCalendarApi` in `frontend/src/api/client.ts`. Add `getDays` to `stubApi` and the `octoberWeek()` and `fallBackDay()` builders in `frontend/src/test/fixtures.ts`, if T021 or T025 didn't already.
- [X] T032 [P] [US1] Add `formatHourLabel(label: 'HH:mm')` to `frontend/src/lib/format.ts`. Use `Intl` with `hour: 'numeric'` in UTC over `1970-01-01T${label}:00Z`, giving "1 AM" in en-US. Add one case to `frontend/src/lib/format.test.ts`.
- [X] T033 [US1] Create `frontend/src/components/TimeGrid.tsx` (contracts/ui-interaction "Week view: wide" and "Day view"):
  - **Props**: `days: TimelineDay[]`, `timeZone`, `onOpenEvent(id, date)`, and `onShowCluster(date, eventIds)`.
  - **Layout**: an hour-label gutter from `hourMarks` (`formatHourLabel`) and one relatively positioned column per day, with height `lengthMinutes × 0.8 px`.
  - **Event blocks**: `<button>`s positioned by `offsetMinutes × 0.8 px`, `max(durationMinutes × 0.8, 24) px` tall, and `left` and `width` from `column / columnCount`. The visible text is the title and start time. The `aria-label` is `describeEvent` from `frontend/src/lib/describe.ts`. Set `data-continues-before` and `data-continues-after`.
  - **Crowded clusters**: when `columnCount ≥ 3`, a "+N" chip of at least 44 × 44 px opens the existing `frontend/src/components/DayOverflowDialog.tsx` with that cluster's events.
  - **Initial scroll** (FR edge case "Long or busy day"): scroll to the current time if a day `isToday`. Otherwise scroll to the earliest timed segment, or to the 08:00 hour mark.
  - **CSS**: time-grid styles in `frontend/src/index.css`.

  Makes T021 pass.
- [X] T034 [P] [US1] Create `frontend/src/components/AllDayBars.tsx`. It draws a CSS grid of 7 columns with one row per lane. Each bar is a `<button>` placed with `grid-column: startIndex+1 / span span`. Clipped edges get a square corner style and visually hidden text: "continues from the previous week" or "continues into the next week".
- [X] T035 [US1] Create `frontend/src/components/WeekView.tsx` (wide):
  - **Headings**: a one-row `role="grid"` of day headings with a roving `tabindex`. ← and → call `onMoveDate(date, leavesPeriod)`, and Enter calls `onOpenDay(date)`. Mark today's heading with `aria-current="date"`.
  - **Body**: `AllDayBars` above, then `TimeGrid` with the 7 days.

  Makes T022 pass.
- [X] T036 [P] [US1] Create `frontend/src/components/WeekList.tsx` (narrow, FR-004a). Render 7 `<section>`s. Each heading is a full-width `<button>`, at least 44 px tall, named `${formatFullDate(date)}, open in day view`. List all-day rows ("All day"), then timed rows (`formatTimeShort` start–end). Every row is a full-width button at least 44 px tall. Days with no events show a "No events" note. On mount, scroll to today's section, or the selected date's section. Makes T023 pass.
- [X] T037 [P] [US1] Create `frontend/src/components/DayView.tsx`: the all-day events stacked at the top as full-width buttons, then `TimeGrid` with the single day. Makes T024 pass.
- [X] T038 [US1] Add the view switcher to `frontend/src/components/ViewHeader.tsx`. It is a `role="group"` labelled "View" with three toggle buttons (Day, Week, Month) using `aria-pressed`. Below 599 px they show "D", "W", and "M" as visible text, but their accessible names stay the full words. Each is at least 44 × 44 px. A new prop `onChangeView(view)` drives it.
- [X] T039 [US1] Wire the views into `frontend/src/components/CalendarScreen.tsx`:
  - **Loading**: load data per `state.view`. Month uses `getMonth`. Day and week use `getDays(timeZone, periodOf(state).first, 1 | 7)`. Keep the 001 `latestRequest` guard so stale responses are ignored.
  - **Rendering**: `DayView` for day; `WeekView` or `WeekList` for week, chosen by `useNarrowScreen()`; `MonthView` for month.
  - **View switching**: `onChangeView` keeps `state.date` (FR-002).
  - **Opening a day**: `onOpenDay(date)` sets `{ view: 'day', date }`.
  - **Announcements**: announce the period title, plus the view name when the view changed, e.g. "Week of October 11 – 17, 2026".
  - **After save or delete**: reload the current view and keep the state (FR-006).

  Makes T025 pass.

**Checkpoint**: US1 is fully functional. All three views render the correct periods on wide and narrow screens, and the domain DST tests pass.

---

## Phase 4: User Story 2 - Move through time and jump to a date (Priority: P1)

**Goal**: In every view: Previous and Next (buttons, plus a swipe on touch), Today, and Go to date (with the native picker). The ends of the range disable the buttons, and focus follows the contract.

**Independent Test**: In each view, step forward and back, swipe on a touch emulator, tap Today, and go to 2028-02-29 and to an invalid date (quickstart rows 3, 6, 7, and 8).

### Tests for User Story 2 (write first, must fail) ⚠️

- [X] T040 [P] [US2] Write `frontend/src/hooks/useSwipe.test.tsx` (research V7) using `fireEvent.pointerDown`, `pointerMove`, and `pointerUp` with `pointerType: 'touch'` and fake timers:
  - **Valid swipes**: dx −60, dy 10 in 300 ms calls `onNext` once. dx +60 calls `onPrevious`.
  - **Too short**: dx −40 does nothing.
  - **Too steep**: dx −60, dy 40 does nothing, because the movement is not at least twice the vertical.
  - **Too slow**: 800 ms does nothing.
  - **Not touch**: `pointerType: 'mouse'` does nothing.
  - **Ignored targets**: a gesture starting on an `<input>` or inside an open `<dialog>` does nothing.
- [X] T041 [P] [US2] Write `frontend/src/components/GoToDateDialog.test.tsx` (contracts/ui-interaction "Go to date dialog"):
  - **Field**: the dialog is titled "Go to date". The `<input type="date">` is labelled "Date", has `min="1900-01-01"` and `max="2199-12-31"`, and starts with the selected date.
  - **Go**: submitting `2028-02-29` calls `onGo('2028-02-29')`.
  - **Invalid date**: an empty or bad-input value shows "Enter a valid date." with `aria-invalid="true"` linked by `aria-describedby`, and `onGo` is not called.
  - **Out of range**: `2200-01-01` shows "Choose a date between 1900 and 2199."
  - **Cancel**: Cancel and Escape call `onClose`.
  - **Accessibility**: an axe pass.
- [X] T042 [P] [US2] Extend `frontend/src/components/CalendarScreen.test.tsx`:
  - **Next in each view**: Next on the day view `2026-10-14` gives `getDays(…, '2026-10-15', 1)`. Next on the week view `2026-12-30` gives the title "January 3 – 9, 2027" and `getDays(…, '2027-01-03', 7)`. Next on the month view `2026-01-31` gives `getMonth(…, 2026, 2)`, and switching to Day then shows Feb 28.
  - **Today**: with an injected `now`, Today returns to today's period in the same view.
  - **Range edges**: Previous is `disabled` on the day view `1900-01-01`, and Next is `disabled` on the month view `2199-12-15`.
  - **Go to date**: Go to date `2027-03-03` from the week view stays on the week view with the title "February 28 – March 6, 2027".
  - **Swipe**: a touch swipe left on the view body behaves like Next. The view body has the style `touch-action: pan-y`.
  - **Focus after navigation (FR-025)**: after Next, focus stays on Next. After Go, focus is on the selected date's day cell (month), heading (week), or period `<h2>` (day).

### Implementation for User Story 2

- [X] T043 [P] [US2] Create `frontend/src/hooks/useSwipe.ts`, which returns pointer handlers and the CSS `touchAction: 'pan-y'`.
  - **Gesture rule**: track one `touch` pointer. On `pointerup`, fire when `|dx| ≥ 48 && |dx| ≥ 2·|dy| && elapsed ≤ 700ms`. A swipe left (`dx < 0`) calls `onNext` and a swipe right calls `onPrevious`.
  - **Ignored targets**: ignore gestures that start in `input, textarea, select, dialog[open] *`.
  - **Clock**: accept an injectable `now()` for tests.

  Keep it under 60 lines with no dependencies. Makes T040 pass.
- [X] T044 [P] [US2] Create `frontend/src/components/GoToDateDialog.tsx`, using the existing `frontend/src/components/Modal.tsx` (native `<dialog>`).
  - **Validation**: use `input.validity.badInput`, `isValidDateString`, and `isInSupportedRange` from `frontend/src/lib/dates.ts`.
  - **Messages**: use the exact strings from T041 and add them to `frontend/src/lib/messages.ts`.
  - **Buttons**: **Go** (primary) and **Cancel**, both at least 44 px tall.

  Makes T041 pass.
- [X] T045 [US2] Wire navigation into `frontend/src/components/ViewHeader.tsx` and `frontend/src/components/CalendarScreen.tsx`:
  - **Go to date button**: add it to the `ViewHeader` actions. It opens `GoToDateDialog`, and Go sets `{ view: state.view, date }`.
  - **Previous and Next**: `canGoPrevious` and `canGoNext` come from `stepPeriod(...) !== null`.
  - **Swipe**: attach `useSwipe(onPrevious, onNext)` to the view-body wrapper (not the header).
  - **Focus**: apply the "Focus after navigation" table from contracts/ui-interaction.md through a `focusTarget` state:
    - `'control'`: no change.
    - `'date'`: the day cell or week heading with `data-date`, or the day view's `<h2>`.
    - `'heading'`: the period `<h2>`.

  Makes T042 pass.

**Checkpoint**: US1 and US2 together give full navigation through all three views. This is the P1 increment.

---

## Phase 5: User Story 3 - Open a day from the month view (Priority: P2)

**Goal**:
- **Month view**: tapping or clicking anywhere on a day (except an event label) opens the day view, and so do Enter and Space on a focused day.
- **Narrow month view**: phone-sized month cells show event markers.
- **Creating events**: an always-visible New event button, and tapping empty time in the day or week view starts an event at that half-hour.

**Independent Test**: quickstart row 1 (on phone and wide screens), row 5's empty-space click, and US3 acceptance scenarios 1–7.

### Tests for User Story 3 (write first, must fail) ⚠️

- [X] T046 [P] [US3] Extend `frontend/src/lib/newEventDefaults.test.ts` for `newEventAt(localStart: 'yyyy-MM-ddTHH:mm')`:
  - `'2026-10-14T14:00'` gives `{ start: '2026-10-14T14:00', end: '2026-10-14T15:00' }`.
  - `'2026-10-14T23:30'` gives the end `'2026-10-15T00:30'`.
  - `'2028-02-28T23:30'` gives the end `'2028-02-29T00:30'`.
  - `'2026-12-31T23:00'` gives the end `'2027-01-01T00:00'`.
- [X] T047 [P] [US3] Update `frontend/src/components/MonthView.test.tsx`:
  - **Replace** the 001 test "starts creating an event on the focused day with Enter or Space" with "opens the focused day in the day view with Enter or Space": it calls `onOpenDay(date)` and never `onCreate`.
  - **Clicks**: clicking the date number, and clicking the empty cell area, each call `onOpenDay('2026-10-14')`. Clicking an event button calls `onOpenEvent` and not `onOpenDay`. A neighboring-month day (Nov 1) calls `onOpenDay('2026-11-01')`.
  - **Wide accessible name**: "Wednesday, October 14, 2026, open in day view".
  - **Narrow markers**: with `setNarrowViewport(true)`, a day with 5 events (1 all-day) renders 3 `aria-hidden` markers (one with `data-kind="all-day"`) and "+2". It has no event buttons, and its name is "Wednesday, October 14, 2026, 5 events, open in day view". A day with no events reads "…, no events, open in day view".
  - **Accessibility**: an axe pass in both modes.
- [X] T048 [P] [US3] Extend `frontend/src/components/TimeGrid.test.tsx` (FR-013b, research V9):
  - **Slot from tap position**: a `pointerup` or click on the column background at `offsetY = 14 × 24 + 5` px (slot 14) calls `onCreateAt('2026-10-14T07:00')`. The slot is computed from `dayStart + 14 × 30 min`, formatted with `toLocalInputValue` in `America/Chicago`.
  - **Fall-back day**: using the fixture, slot 2 gives `'2026-11-01T01:00'` (the first 1 AM). Slot 4 also gives `'2026-11-01T01:00'` (the second 1 AM, which maps to the same local start, research V9 note). Slot 6 gives `'2026-11-01T02:00'`.
  - **Hidden background**: the background is `aria-hidden` and not focusable.
  - **Events take precedence**: a tap on an event block does **not** call `onCreateAt`.
- [X] T049 [P] [US3] Extend `frontend/src/components/CalendarScreen.test.tsx`:
  - **New event button**: on every view, it opens the event form with the start date set to `state.date` (001 FR-005 defaults).
  - **Empty-slot tap**: tapping the 2:00 PM slot in the day view opens the form with start `2026-10-14T14:00` and end `15:00`.
  - **Tapping a month day**: switches to `{ view: 'day', date }` and moves focus to the day view `<h2>`.
  - **Narrow week heading**: tapping a heading in the narrow week list does the same.

### Implementation for User Story 3

- [X] T050 [P] [US3] Add `newEventAt(localStart)` to `frontend/src/lib/newEventDefaults.ts`, using `addDays` for the rollover. It uses no `Date` local-time methods. Makes T046 pass.
- [X] T051 [US3] Update `frontend/src/components/DayCell.tsx` and `frontend/src/components/MonthView.tsx` (FR-013, FR-003a):
  - **Tap target**: the whole cell is the tap target. The cell's `onClick` calls `onOpenDay(day.date)` unless the event target is inside a `button`.
  - **Accessible name**: `aria-label` is built by a new `describeDay(day, { narrow })` in `frontend/src/lib/describe.ts`:
    - It starts with the full date, adds ", today" when `isToday` (consumed by US4), adds ", N events" or ", no events" when narrow, and ends with ", open in day view".
  - **Keyboard**: in the `MonthView` keyboard map, Enter and Space call `onOpenDay`. Remove `onCreate` from `MonthView`'s props, because creating now goes through the header's New event button.
  - **Narrow**: when `useNarrowScreen()` is true, render up to 3 `aria-hidden` markers instead of `EventButton`s: `<span class="marker" data-kind="timed|all-day">`. Timed events are dots and all-day events are bars. After them comes `+N`, and there is no "+N more" button.
  - **CSS in `frontend/src/index.css`**: markers, and cells at least 44 px in both dimensions on narrow screens.

  Makes T047 pass.
- [X] T052 [US3] Add empty-space creation to `frontend/src/components/TimeGrid.tsx`:
  - **Background**: an `aria-hidden` background layer per column, below the event blocks.
  - **Tap handling**: on click, `slot = floor(offsetY / 24)` and `instant = new Date(Date.parse(day.dayStart) + slot × 30 × 60000)`. Call `onCreateAt(toLocalInputValue(instant.toISOString(), timeZone))`, using the existing helper in `frontend/src/lib/format.ts`.
  - **Press highlight**: show a light highlight of the slot while the pointer is down.

  Makes T048 pass.
- [X] T053 [US3] Update `frontend/src/components/EventFormDialog.tsx` to accept an optional `initialTimes?: { start: LocalDateTimeString; end: LocalDateTimeString }`. When it is given in create mode, it overrides the `newEventDefaults` result. Add one case to `frontend/src/components/EventFormDialog.test.tsx`.
- [X] T054 [US3] Wire everything into `frontend/src/components/CalendarScreen.tsx`:
  - **Creating**: the header's New event button opens `{ kind: 'create', date: state.date }`. `onCreateAt(localStart)` opens `{ kind: 'create', date: localStart.slice(0, 10), times: newEventAt(localStart) }`, which is passed through as `initialTimes`.
  - **Opening a day**: `onOpenDay` comes from `MonthView` and `WeekList`, with the focus target `'heading'`.

  Makes T049 pass.

**Checkpoint**: Choosing a day opens it on every screen size, and creating events works through the button and through empty-time taps.

---

## Phase 6: User Story 4 - See where today is (Priority: P2)

**Goal**: Today is highlighted by shape as well as color in every view and read as "today". The highlight moves at midnight or when the zone changes, within 1 minute and without a reload. A current-time line is shown in the time grid.

**Independent Test**: With an injected clock, open each view on today's period and on a different period. Then advance the clock past midnight and change the zone (quickstart rows 6 and 15).

### Tests for User Story 4 (write first, must fail) ⚠️

- [X] T055 [P] [US4] Write `frontend/src/hooks/useToday.test.tsx` with `vi.useFakeTimers()` and an injected `now()` and `readZone()`:
  - **Initial value**: it returns `{ timeZone, today, now }`.
  - **Midnight**: advancing from `2026-10-15T04:59:30Z` past `05:00Z` (midnight in Chicago) updates `today` to `2026-10-15` within 30 s.
  - **Visibility**: firing `visibilitychange` with `document.visibilityState = 'visible'` re-checks immediately.
  - **Zone change**: changing `readZone()` to `America/New_York` updates `timeZone`.
  - **No needless re-renders**: if nothing changed, the returned object keeps the same identity apart from `now`.
- [X] T056 [P] [US4] Write `frontend/src/components/NowLine.test.tsx`. With `dayStart = '2026-11-01T00:00:00-05:00'` and `now = 2026-11-01T07:30:00Z` (01:30 at the second occurrence, −06:00), the line's `top` is `(150 min) × 0.8 = 120px`. This is computed from elapsed time, not the wall-clock time. The line is not rendered when the day isn't today.
- [X] T057 [P] [US4] Extend `frontend/src/components/CalendarScreen.test.tsx` and the view tests:
  - **Month view**: today's cell has `aria-current="date"` and its name contains ", today". No other cell has either.
  - **Wide week and narrow week**: today's heading has `aria-current="date"`, and its name contains "today".
  - **Day view**: the today day view shows `NowLine`, and the `<h2>` description contains "today".
  - **Another period**: November 2026, while today is 2026-10-14, has no `aria-current`.
  - **Clock rollover**: when the injected clock rolls to the next date, the highlight moves and the current period reloads. `state` and the address are unchanged.

### Implementation for User Story 4

- [X] T058 [P] [US4] Create `frontend/src/hooks/useToday.ts` (research V11):
  - **Signature**: `useToday({ now = () => new Date(), readZone = currentTimeZone } = {})`.
  - **Checks**: a 30-second `setInterval`, plus a check on `visibilitychange`.
  - **Result**: computes `todayIn(zone, now())` and returns `{ timeZone, today, now }` with stable identity where nothing changed.

  Makes T055 pass.
- [X] T059 [P] [US4] Create `frontend/src/components/NowLine.tsx`. Its props are `dayStart` and `now`. It is positioned at `(now − Date.parse(dayStart)) / 60000 × 0.8 px`, with `aria-hidden` and the class `now-line` (a line plus a dot, using the `--today` color). Render it inside `TimeGrid` for the day where `isToday` is set. Makes T056 pass.
- [X] T060 [US4] Apply the today styling in `frontend/src/index.css` and the components:
  - **Styling**: the date number sits inside a filled circle using `--today` with bold white text, in `DayCell`, the `WeekView` headings, the `WeekList` headings, and the `DayView` `<h2>` badge. The difference must not rely on color alone (FR-014).
  - **Attributes**: add `aria-current="date"` and ", today" to the accessible names through `describeDay` in `frontend/src/lib/describe.ts`.
- [X] T061 [US4] Wire `useToday` into `frontend/src/App.tsx` and `frontend/src/components/CalendarScreen.tsx`:
  - **App**: `App` passes `timeZone`, `today`, and `now` down, replacing the one-time `useState(currentTimeZone)`.
  - **CalendarScreen**: when `today` or `timeZone` changes, reload the current period without changing `state` (FR-015). Use `today` for the Today button and for parsing the address (US5).

  Makes T057 pass.

**Checkpoint**: Today is clear in every view and stays correct across midnight and zone changes.

---

## Phase 7: User Story 5 - Refresh and bookmark the view I'm on (Priority: P2)

**Goal**:
- **Address**: `/{view}/{yyyy-MM-dd}` always reflects the state.
- **History**: Back and Forward step through views and periods. Moves within a period replace the history entry instead of adding one.
- **Fallbacks**: `/` and `/{view}` resolve to today. Bad links fall back to the month view with a message and a corrected address.

**Independent Test**: quickstart rows 10, 11, and 12. Also open `/day/2027-03-03` directly in a new tab.

### Tests for User Story 5 (write first, must fail) ⚠️

- [X] T062 [P] [US5] Extend `frontend/src/lib/viewState.test.ts` for `toPath` and `parsePath(pathname, today)`. Cover every row of the data-model table, with `today = '2026-10-14'`:
  - **Valid paths**:
    - `/` gives `{month, 2026-10-14}`.
    - `/week` and `/week/` give `{week, 2026-10-14}`.
    - `/day/2027-03-03` gives `{day, 2027-03-03}`.
    - `/month/2028-02-29` is valid.
  - **Invalid paths**: each of these gives `{month, 2026-10-14}` with `error: 'invalid-link'`:
    - `/Week/2026-10-14` (upper case)
    - `/fortnight/2026-10-14`
    - `/day/2026-02-30`
    - `/day/2027-02-29`
    - `/day/1899-12-31`
    - `/day/2026-10-14/extra`
    - `/day/20261014`
  - **Round trip**: `toPath(parsePath(toPath(s)).state) === toPath(s)` for sample states.
- [X] T063 [P] [US5] Write `frontend/src/hooks/useViewState.test.tsx` against jsdom's `history` (research V1, data-model "State transitions"):
  - **Push**: `setState(next, 'push')` calls `pushState` with `toPath(next)`.
  - **Replace**: `'replace'` calls `replaceState`.
  - **Today unchanged**: pressing Today when the state is unchanged pushes nothing.
  - **Back and Forward**: a `popstate` event restores the state from `location.pathname`.
  - **Bad link on load**: an initial path `/nope` returns `error: 'invalid-link'` and calls `replaceState('/month/2026-10-14')`.
- [X] T064 [P] [US5] Extend `backend/tests/PersonalCalendar.Api.Tests/HostSmokeTests.cs`:
  - `GET /week/2026-10-14` and `GET /fortnight/nope` each return `200` with `text/html`, and the body is the SPA `index.html` (contracts/http-api "Client routes").
  - `GET /api/nope` still returns `404`.
- [X] T065 [P] [US5] Extend `frontend/src/components/CalendarScreen.test.tsx`:
  - **Deep link**: rendering with `window.history.replaceState(null, '', '/week/2026-10-14')` first shows that week.
  - **Address follows navigation**: Next updates `location.pathname` to `/week/2026-10-21`, then Back returns to `/week/2026-10-14` and re-renders that week.
  - **Arrow keys**: an arrow-key move inside the week view's headings uses replace, so `history.length` is unchanged. Moving past Saturday pushes.
  - **Bad link**: `/fortnight/x` shows the notice "That link couldn't be opened, so you're seeing this month." with a **Dismiss** button, announces it once, and the path becomes `/month/<today>`.

### Implementation for User Story 5

- [X] T066 [US5] Add `toPath(state)` and `parsePath(pathname, today)` to `frontend/src/lib/viewState.ts`. Parse with exact lower-case view names, `isValidDateString`, and `isInSupportedRange`. Makes T062 pass.
- [X] T067 [US5] Create `frontend/src/hooks/useViewState.ts`, which is about 60 lines and uses no router. It returns `[state, setState(next, mode: 'push' | 'replace'), error, clearError]`. It parses on mount, replaces the address on an `invalid-link`, listens to `popstate`, and skips pushing when `toPath(next) === location.pathname`. Makes T063 pass.
- [X] T068 [P] [US5] Create `frontend/src/components/InvalidLinkNotice.tsx`. It shows the text "That link couldn't be opened, so you're seeing this month." with a **Dismiss** button at least 44 px tall, and announces it once through `useAnnounce`.
- [X] T069 [US5] Replace `useState<ViewState>` in `frontend/src/components/CalendarScreen.tsx` with `useViewState(today)`:
  - **Push**: view switches, Previous and Next, swipes, Today, Go to date, and opening a day all use `'push'`.
  - **Replace**: arrow-key moves inside the current period use `'replace'`, and moves past its edge use `'push'` (FR-021).
  - **Notice**: render `InvalidLinkNotice` when `error` is set.
  - **Focus**: after a `popstate`, the focus target is `'heading'`.

  Makes T064's frontend counterpart and T065 pass. T064 itself should already pass through the 001 fallback. If it doesn't, fix `backend/src/PersonalCalendar.Api/Program.cs` so `MapFallbackToFile` still runs after `/api` and returns `404` for `/api`.

**Checkpoint**: All five stories work independently and together.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Performance, the accessibility and touch sweep, documentation, and the checks the constitution requires for merging.

- [X] T070 [P] Extend `backend/tests/PersonalCalendar.Api.Tests/PerformanceTests.cs` with `DaysView_With5000Events_RespondsQuickly`. Seed 5,000 events as the existing month test does, then assert that a warm `GET /api/calendar/days?timeZone=America/Chicago&start=2026-10-11&count=7` returns in **300 ms or less** (contracts/http-api, SC-002).
- [X] T071 [P] Extend `frontend/src/App.test.tsx`:
  - **Axe**: run `axe` over the whole page for the month, wide week, narrow week, and day views, and with the Go to date dialog open.
  - **Tab order**: header (title, Previous, Today, Next, Go to date, view switcher, New event), then the view body, then out.
  - **No shortcuts**: no keyboard shortcuts beyond the Constitution IV baseline are registered. Assert that pressing `d`, `w`, `m`, or `t` on the body changes nothing.
- [X] T072 [P] Update `specs/001-event-basics/contracts/ui-interaction.md` with a single note under "Keyboard in the grid": "Superseded in 002: Enter/Space opens the day view; see specs/002-calendar-views/contracts/ui-interaction.md." Change nothing else in 001's documents.
- [X] T073 [P] Update `README.md`: add a "Views & navigation" section with the URL scheme `/{day|week|month}/{yyyy-MM-dd}`, the swipe gesture, and a note that the phone layouts can be checked through browser device emulation, because the API listens on loopback only.
- [X] T074 Run `dotnet test backend/PersonalCalendar.slnx`, `npm test --prefix frontend`, `npm run lint --prefix frontend`, and `npm run build --prefix frontend`, and fix any failures.
- [ ] T075 Work through quickstart.md rows 1–15 by hand. Do the **touch-first** pass on a phone-sized touch emulator (390 × 844), then the mouse pass on a wide window, then the keyboard pass and the screen-reader pass (NVDA or Narrator). Record the results, and any WCAG 2.2 AA exceptions, in the PR description, as Principle IV and the constitution's Merge gate require. Then open the PR `002-calendar-views` → `main` (Principle V).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup and blocks every story.
- **US1 (Phase 3)**: depends on Foundational. This is the MVP.
- **US2 (Phase 4)**: depends on Foundational. It is fully testable on the month view alone. Swipe and Go to date reach the day and week views once US1 is done, and T042's day and week cases need US1 (T039).
- **US3 (Phase 5)**: depends on Foundational.
  - The month-view part (T047 and T051) is independent of the other stories.
  - The empty-slot part (T048 and T052) and the narrow week heading case in T049 need `TimeGrid` and `WeekList` from US1 (T033 and T036).
- **US4 (Phase 6)**: depends on Foundational. Its month-view highlight is independent. The `NowLine`, week, and day cases need US1 (T033, T035, T036, and T037). `describeDay` comes from US3 (T051). If US4 is done before US3, create `describeDay` in T060 instead.
- **US5 (Phase 7)**: depends on Foundational. It replaces the `useState` from T016. T065's arrow-key case uses the `WeekView` headings from US1 (T035).
- **Polish (Phase 8)**: depends on every story being done.

### Within each phase

- Test tasks come before implementation and MUST fail first (Principle I). This is mandatory for T004–T007, T017–T020, T046, T048, T055, T056, and T062.
- Backend order: Domain → Application → Api. Frontend order: lib → hooks → components → `CalendarScreen` wiring.
- These tasks edit the same file in sequence, so they are not [P] with each other:
  - `CalendarScreen.tsx`: T016 → T039 → T045 → T054 → T061 → T069
  - `CalendarScreen.test.tsx`: T025 → T042 → T049 → T057 → T065
  - `ViewHeader.tsx`: T014 → T038 → T045
  - `TimeGrid.tsx`: T033 → T052 → T059 (render `NowLine`)
  - `MonthView.tsx` and `DayCell.tsx`: T015 → T051 → T060
  - `viewState.ts`: T010 → T066
  - `format.ts`: T012 → T032
  - `describe.ts`: T051 → T060
  - `fixtures.ts`: T021/T025 → T031 (whichever runs first creates the builders, and later tasks only add to them)

### Parallel Opportunities

- **Setup**: T002 and T003.
- **Foundational tests**: T004–T008 together. Then T011, T012, and T013 in parallel after T009 and T010.
- **US1 tests**: T017–T025 can all be written together.
- **US1 implementation**: backend track T026 → T027/T028 → T029 → T030, and frontend track T031/T032 → T033 → T034–T037 → T038 → T039. The two tracks are independent until the T039 wiring.
- **US2**: T040–T042 together, then T043 and T044 in parallel.
- **US3–US5**: each story's test tasks are [P]. US4's T058 and T059, and US5's T068, can run alongside other work.
- **Polish**: T070–T073 in parallel.

---

## Parallel Example: User Story 1

```text
# Write all US1 tests together (they must fail):
Task: T017 DayTimelineTests.cs         (backend/tests/PersonalCalendar.Domain.Tests)
Task: T018 AllDayLanesTests.cs         (backend/tests/PersonalCalendar.Domain.Tests)
Task: T019 GetDaysViewTests.cs         (backend/tests/PersonalCalendar.Application.Tests)
Task: T020 DaysEndpointTests.cs        (backend/tests/PersonalCalendar.Api.Tests)
Task: T021 TimeGrid.test.tsx           (frontend/src/components)
Task: T022 WeekView.test.tsx           (frontend/src/components)
Task: T023 WeekList.test.tsx           (frontend/src/components)
Task: T024 DayView.test.tsx            (frontend/src/components)
Task: T025 CalendarScreen.test.tsx     (frontend/src/components)

# Then two tracks:
Track A (backend):  T026 records → T027 DayTimeline ∥ T028 AllDayLanes → T029 GetDaysView → T030 endpoint
Track B (frontend): T031 types/client ∥ T032 hour labels → T033 TimeGrid → T034 AllDayBars ∥ T036 WeekList ∥ T037 DayView → T035 WeekView → T038 switcher
Join:               T039 CalendarScreen wiring
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1: Setup (T001–T003)
2. Phase 2: Foundational (T004–T016). The month view behaves exactly as before.
3. Phase 3: US1 (T017–T039)
4. **Stop and check**: quickstart rows 2, 4, 5, and 9, on a phone-sized emulator first and then on a wide window.

### Incremental Delivery

1. Setup and Foundational give a `ViewState`-driven month view.
2. Adding US1 adds the day and week views, which is the MVP.
3. Adding US2 adds Previous, Next, swipe, Today, and Go to date, completing the P1 scope.
4. Adding US3 adds tap-a-day, the phone month markers, and creating from empty time.
5. Adding US4 adds the today highlight, the now line, and the midnight and zone rollover.
6. Adding US5 adds the URL, history, and bookmarks.
7. Polish: performance, the axe and Tab sweep, docs, the manual touch-first quickstart pass, and the PR to `main` (Principle V).

---

## Notes

- [P] means the task touches a different file and has no dependency on an unfinished task.
- Commit after each task or logical group, on branch `002-calendar-views`.
- **Touch first**: when a task's details leave a choice open, pick the option that is better for tapping and clicking. Never add keyboard shortcuts beyond the Constitution IV baseline (spec Clarifications).
- Every date/time bug found later MUST come with a regression test that reproduces it (Principle I).
- Do not add dependencies beyond those in plan.md (none are added) without recording the justification in plan.md's Complexity Tracking section (Principle III).
