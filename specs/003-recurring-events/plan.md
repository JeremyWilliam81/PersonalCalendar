# Implementation Plan: Recurring Events

**Branch**: `003-recurring-events` | **Date**: 2026-10-06 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/003-recurring-events/spec.md`

## Summary

Events can repeat daily, weekly on chosen weekdays, monthly (by day number, clamped to the last day of shorter months, or by Nth/last weekday), or yearly. Each repeat has an interval of 1–99 and ends never, on a date, or after 1–999 occurrences. Occurrences appear in the month, week, and day views with a repeat icon. Editing or deleting an occurrence asks at Save or Delete for "This event", "This and following events", or "All events", and hides the choices that don't apply.

**Technical approach**:
- **Backend**:
  - A series is a `CalendarEvent` with a `SeriesRecurrence`: an RFC 5545 RRULE stored as text, the series' IANA zone, and the first occurrence's local start and end (Constitution II).
  - The pure, test-first domain service `Recurrence.Expand` generates occurrences for a date range in the series zone with NodaTime. It handles DST like single events (001 R4).
  - Changed and deleted occurrences live in a new `OccurrenceExceptions` table, keyed by `(SeriesId, OriginalDate)`.
  - Scope operations (this, following, all, stop repeating) are domain methods that are saved in one transaction.
  - The month and days layouts take a new `CalendarItem` input, so they lay out occurrences and one-time events alike.
  - The existing `/api/events` endpoints gain `recurrence`, `occurrence`, and `scope`. One-time events are unchanged.
- **Frontend**:
  - A `RepeatFields` section in `EventFormDialog` uses native radio buttons, toggles, and −/+ steppers sized for fingertips.
  - A `ScopeChoiceDialog` adds a warning step.
  - A shared `RepeatIcon`.
  - Zone-free helpers in `lib/recurrence.ts` for monthly options, the first-occurrence preview, and the `Intl`-based summary.

## Technical Context

**Language/Version**: C# 14 / .NET 10, and TypeScript 6 on Node 24, unchanged.

**Primary Dependencies**: no new packages. The backend uses ASP.NET Core Minimal APIs, EF Core 10 + SQLite, and NodaTime 3.3. The frontend uses React 19 and Vite 8. An RRULE library was considered and rejected (research S1).

**Storage**: the same SQLite file. The migration `AddRecurrence` adds 6 nullable columns, a CHECK constraint, and an index to `Events`, and adds the table `OccurrenceExceptions`. Existing rows stay one-time events.

**Testing**:
- **Backend**: xUnit with `FakeClock` and explicit zones (`America/Chicago`, `Asia/Kolkata`, `Australia/Adelaide`, `Australia/Lord_Howe`), plus `WebApplicationFactory` API tests and a migration upgrade test.
- **Frontend**: Vitest, Testing Library with `user-event`, and `vitest-axe`, under a fixed `TZ` and locale.

**Target Platform**: Evergreen desktop and phone browsers on the local app, unchanged from 002. Phones are reached through device emulation.

**Project Type**: Web application: the local API plus SPA, unchanged.

**Performance Goals**: with 5,000 one-time events plus 200 series that never end, the month and 7-day endpoints each take 300 ms or less, and the views appear in 1 s or less (SC-004). Expanding a series costs about the number of candidate dates in the range (research S7).

**Constraints**:
- **Touch first**: targets are at least 44 × 44 px, scope buttons are at least 48 px tall, the repeat section fits 320 px without scrolling sideways, and nothing works by gesture only.
- **Keyboard**: the Constitution IV baseline only.
- **WCAG 2.2 AA**.
- **Atomicity**: multi-row changes are all-or-nothing (FR-027).
- **Occurrence identity**: an occurrence is keyed by its original date, which must stay stable across edits and zone changes.

**Scale/Scope**: 1 user, about 5,000 events, and up to a few hundred series.
- **Backend**: 1 migration; 4 new domain types (`RepeatRule`, `SeriesRecurrence`, `OccurrenceException`, `CalendarItem`) and 1 domain service; 4 changed use cases.
- **Frontend**: 3 new components and 1 new library module.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | How the plan complies | Status |
|---|---|---|
| **I. Test-First Date & Time Logic** | `RepeatRule`, `Recurrence.Expand`, and the scope operations each get failing xUnit tests first. Research S13 lists them: spring-forward and fall-back occurrences (shifted 02:30, earlier 01:30), the 30-minute Lord Howe shift, display from Chicago in `Asia/Kolkata` and `Australia/Adelaide`, overnight occurrences across DST, day 29–31 clamping, Feb 29 in leap years only, month and year boundaries, and all-day series. Only `FakeClock` and explicit zones are used. The frontend helpers in `lib/recurrence.ts` are written test-first under a fixed `TZ`. | ✅ |
| **II. UTC Storage + IANA Zone** | One-time events and exceptions keep UTC instants. A series stores its rule, its IANA `RecurrenceTimeZone`, and the first occurrence's **local** start and end. This is the case Principle II names ("recurring events … the original local value and its IANA zone MUST be stored"), and the choice is documented in research S3. RRULE `UNTIL` is UTC, as RFC 5545 requires. | ✅ |
| **III. Single-Developer Simplicity** | No new dependencies: no RRULE library (S1), no date picker, and no state library. One new table is justified by RFC 5545 exceptions (S5), and a fully materialized alternative cannot represent infinite series. There are no caches (S7). | ✅ |
| **IV. Accessible by Default** | Native radios and `aria-pressed` toggles give keyboard operation and states for free. Weekday buttons are named in full. The summary is live and read in full. The scope dialog reuses `Modal` with its focus trap and focus return. Results are announced. The repeat icon is not color-only and adds ", repeats" to the accessible name. No known WCAG exceptions. | ✅ |
| **V. Spec-First, Branch-Per-Feature** | The spec and clarify session (5 Q&A) are done. The work is on branch `003-recurring-events` and merges by PR. | ✅ |
| **VI. Clean Architecture** | `RepeatRule`, `Recurrence`, the scope operations, and `CalendarItem` are in Domain. Expansion orchestration and scope use cases are in Application. EF mapping, RRULE storage, and the migration are in Infrastructure. Endpoints are in Api. The frontend only does zone-free date-string math and `Intl` formatting (001 R5). | ✅ |
| **Date & Time Standards** | ISO 8601 with offsets across boundaries. Local values are always paired with a zone: the request's `timeZone`, or the series' `timeZone` in responses. NodaTime is the only zone library, and the clock is injected. **Recurrence follows RFC 5545 and is expanded in the event's IANA zone**, as the standard requires. | ✅ |

**Post-design re-check (after Phase 1)**: ✅ Every principle still passes. The data model adds one table and nullable columns in the existing SQLite file, with no new process or dependency. The API changes are additive, so 001 and 002 contracts and tests stay valid. One deliberate behavior is recorded: a series repeats in the zone where it was created, while occurrences are *displayed* in the device zone. This keeps 001's "follow the device" clarification for display and Principle II's wall-clock rule for repetition. It is not a violation, so Complexity Tracking stays empty.

## Project Structure

### Documentation (this feature)

```text
specs/003-recurring-events/
├── plan.md              # This file
├── research.md          # Phase 0: decisions S1–S13
├── data-model.md        # Phase 1: series, rule, exceptions, schema, lifecycle
├── quickstart.md        # Phase 1: automated checks + touch-first manual scenarios
├── contracts/
│   ├── http-api.md      # recurrence object, occurrence + scope on /api/events
│   └── ui-interaction.md# repeat section, scope dialog, repeat indicator
├── checklists/
│   └── requirements.md  # spec quality checklist
└── tasks.md             # Phase 2 (/speckit-tasks — not created here)
```

### Source Code (repository root; new files marked +, changed files marked ~)

```text
backend/
├── src/
│   ├── PersonalCalendar.Domain/
│   │   ├── Events/
│   │   │   ├── + RepeatRule.cs, SeriesRecurrence.cs, OccurrenceException.cs
│   │   │   ├── + Recurrence.cs              # Expand (pure)
│   │   │   ├── ~ CalendarEvent.cs           # Recurrence, Exceptions, scope operations
│   │   │   └── ~ ScheduleResolver.cs        # reuse for occurrence resolution
│   │   ├── Calendar/
│   │   │   ├── + CalendarItem.cs
│   │   │   └── ~ MonthGrid.cs, DayTimeline.cs, AllDayLanes.cs, EventOrdering.cs  # take CalendarItem
│   │   └── Validation/ ~ ErrorCodes (recurrence.*, occurrence.*, scope.*)
│   ├── PersonalCalendar.Application/
│   │   ├── Abstractions/ ~ IEventRepository.cs  # ReplaceAsync, series in ListOverlapping
│   │   └── Events/
│   │       ├── + RecurrenceInput.cs, CalendarItems.cs  # one-time + expanded occurrences (EditScope lives in Domain/Events/SeriesEditing.cs)
│   │       ├── ~ CreateEvent.cs, UpdateEvent.cs, DeleteEvent.cs, GetEventDetails.cs
│   │       ├── ~ GetMonthView.cs, GetDaysView.cs, EventMapping.cs
│   │       └── ~ EventDetails.cs, MonthViewModel.cs (EventSummary), EventInput.cs
│   ├── PersonalCalendar.Infrastructure/Persistence/
│   │   ├── + OccurrenceExceptionRow.cs, OccurrenceExceptionConfiguration.cs
│   │   ├── ~ EventRow.cs, EventConfiguration.cs, EventRowMapper.cs, EfEventRepository.cs, CalendarDbContext.cs
│   │   └── + Migrations/*_AddRecurrence.cs
│   └── PersonalCalendar.Api/Endpoints/
│       ├── ~ EventEndpoints.cs  # occurrence + scope query params
│       └── ~ EventRequest.cs    # recurrence object parsing
└── tests/
    ├── PersonalCalendar.Domain.Tests/       + RepeatRuleTests.cs, RecurrenceExpandTests.cs, SeriesScopeTests.cs; ~ layout tests (CalendarItem)
    ├── PersonalCalendar.Application.Tests/  + RecurringUseCaseTests.cs; ~ Fakes/InMemoryEventRepository.cs
    └── PersonalCalendar.Api.Tests/          + RecurrenceEndpointTests.cs, MigrationUpgradeTests.cs; ~ PerformanceTests.cs

frontend/src/
├── api/        ~ types.ts (Recurrence, EditScope, summary/details fields), ~ client.ts (occurrence + scope)
├── lib/        + recurrence.ts (monthlyOptions, firstOccurrence, describeRule), ~ describe.ts (", repeats"), ~ messages.ts
├── components/
│   ├── + RepeatFields.tsx, ScopeChoiceDialog.tsx, RepeatIcon.tsx
│   ├── ~ EventFormDialog.tsx    # hosts RepeatFields; detects date/repeat changes; FR-016a message
│   ├── ~ EventDetailsDialog.tsx # occurrence dates + summary
│   ├── ~ CalendarScreen.tsx     # passes occurrence; runs scope flow for save/delete
│   └── ~ EventButton.tsx, TimeGrid.tsx, AllDayBars.tsx, WeekList.tsx  # RepeatIcon
└── ~ index.css    # segmented control, weekday toggles, steppers, scope buttons
(tests colocated as *.test.ts[x])
```

**Structure Decision**: This keeps the 001 and 002 layout: the `backend/` .NET solution in four layers and the `frontend/` Vite React app, served as one local process. Recurrence types sit beside `CalendarEvent` in `Domain/Events`, because a series is that aggregate. `CalendarItem` sits beside the layouts that use it in `Domain/Calendar`.

## Complexity Tracking

No constitution violations, so this section is intentionally empty.
