# Implementation Plan: Calendar Views and Navigation

**Branch**: `002-calendar-views` | **Date**: 2026-10-02 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-calendar-views/spec.md`

## Summary

This feature adds day and week views beside the existing month view. It also adds Previous, Next, Today, and Go to date in every view, a today highlight, opening a day by tapping it in the month view, and an address that names the view and date and survives refreshes and bookmarks. The design is **touch first**: swiping changes the period, every target is fingertip-sized, the week view becomes a stacked list on phones, and the month view shows markers on phones. Keyboard use meets the Constitution IV baseline.

**Technical approach**:
- **Backend**: one new read-only endpoint, `GET /api/calendar/days?start&count=1|7`. Two pure, test-first NodaTime domain functions build it:
  - `DayTimeline`: the real day length (23, 24, or 25 hours), hour marks, timed segments clipped at midnight, and overlap columns.
  - `AllDayLanes`: multi-day bars across the week.

  The schema, the stored data, and the existing endpoints do not change.
- **Frontend**:
  - The view state (`view` and `date`) lives in the URL path, `/{view}/{yyyy-MM-dd}`, and is managed with the History API through a small hook. There is no router library, and the existing server fallback already serves `index.html`.
  - Period arithmetic extends the zone-free `lib/dates.ts`.
  - New components: `DayView`, `WeekView` (time grid), `WeekList` (phone layout), the shared `ViewHeader`, and `GoToDateDialog`.
  - Small hooks with no dependencies: `useSwipe` (Pointer Events), `useNarrowScreen` (`matchMedia`), and `useToday` (a 30-second check).

## Technical Context

**Language/Version**: C# 14 / .NET 10, and TypeScript 6 on Node 24. These are unchanged from 001.

**Primary Dependencies**: no new packages. The backend uses ASP.NET Core Minimal APIs, EF Core 10 + SQLite, and NodaTime 3. The frontend uses React 19 and Vite.

**Storage**: the SQLite file from 001. This feature changes no schema and adds no migration.

**Testing**:
- **Backend**: xUnit with `FakeClock` and explicit zones, including `America/Chicago`, `Asia/Kolkata`, `Australia/Adelaide`, and `Australia/Lord_Howe`, plus `WebApplicationFactory` integration tests.
- **Frontend**: Vitest, Testing Library (with `user-event` and `fireEvent` pointer events for swipes), and `vitest-axe`. The test setup fixes `TZ` and the locale and stubs `matchMedia`.

**Target Platform**: Current evergreen browsers on the user's own machine. This now explicitly includes phone and tablet browsers, and touch laptops, reaching the local app (see Constraints).

**Project Type**: Web application. This is the same local API plus SPA as 001.

**Performance Goals**: Navigation shows the new period in 1 s or less with 5,000 events (SC-002). The `/days?count=7` call takes 300 ms or less. Switching views or periods updates the address and header instantly, before the data arrives.

**Constraints**:
- **Touch first** (spec Clarifications): targets are at least 44 × 44 px, swiping is a shortcut and never the only way to do something, and phone layouts start at 599 px wide and below.
- **Keyboard**: meets the Constitution IV baseline only, with no extra shortcuts.
- **WCAG 2.2 AA**.
- **Loopback only, as in 001**: a phone can reach the app only through the developer's device-emulation or remote-debugging tools. That is enough to validate the touch layouts. Real LAN access would need its own spec, because it brings authentication into scope.

**Scale/Scope**: 1 user, up to about 5,000 events, 3 views × 2 layouts, 1 new endpoint, 1 new dialog, and about 6 new components and 4 new hooks.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | How the plan complies | Status |
|---|---|---|
| **I. Test-First Date & Time Logic** | `DayTimeline` and `AllDayLanes` get failing xUnit tests before they are implemented. The tests cover spring-forward (23 h) and fall-back (25 h, repeated hour), the 30-minute DST shift in `Australia/Lord_Howe`, `Asia/Kolkata`, and `Australia/Adelaide`, midnight crossings, an event ending exactly at midnight, 2028-02-29, month and year boundaries, and all-day events. They use `FakeClock` and explicit zones only. The frontend's pure date functions (`stepPeriod`, `parsePath`, `todayIn`, and `newEventAt`) are also written test-first with injected `now` and a fixed `TZ`. | ✅ |
| **II. UTC Storage + IANA Zone** | Nothing new is stored. The address carries plain calendar dates only (FR-022). Positions on the time scale are elapsed minutes from the real instant where the day starts, so there is no wall-clock offset math. | ✅ |
| **III. Single-Developer Simplicity** | No new dependencies: no router, no gesture library, no date picker, and no state library. The endpoint reuses the 001 repository query. The hooks are each under 60 lines. Each decision is justified in research V1–V12. | ✅ |
| **IV. Accessible by Default** | Fully keyboard operable, with visible focus. Calendar grids (the month grid and the week's day headings) keep arrow-key navigation, and the month grid keeps the APG grid pattern. Every control has an accessible name. Period changes are announced, today is read as "today", and dates are read in full. The target is WCAG 2.2 AA. Target size (2.5.8) and single-pointer alternatives to swipes (2.5.1) are met. The empty-slot tap target is pointer-only, which is acceptable because the always-visible New event button does the same job (research V9). No known exceptions. | ✅ |
| **V. Spec-First, Branch-Per-Feature** | The spec and clarify session are done. The work is on `002-calendar-views` and merges into `main` by PR. | ✅ |
| **VI. Clean Architecture** | `DayTimeline` and `AllDayLanes` are in the Domain. `GetDaysView` is in Application. The endpoint is in Api. The frontend stays an adapter: it only formats with `Intl` and does zone-free date-string math, as 001 R5 allows. | ✅ |
| **Date & Time Standards** | ISO 8601 with offsets across boundaries. Hour-mark labels are `LocalTime`s that travel with the response's `timeZone`. NodaTime is the only zone library, and the clock is injected (`IClock` and the frontend's `now`). | ✅ |

**Post-design re-check (after Phase 1)**: ✅ Every principle still passes. The design adds no stored data, no dependency, and no new process. Two deliberate interaction choices are recorded:
- Keyboard users create events with the New event button instead of per-slot targets (research V9).
- The month grid's Enter key now opens the day view instead of creating an event. This changes a 001 contract and is noted in [contracts/ui-interaction.md](./contracts/ui-interaction.md).

Neither is a constitution violation. Complexity Tracking stays empty.

## Project Structure

### Documentation (this feature)

```text
specs/002-calendar-views/
├── plan.md              # This file
├── research.md          # Phase 0: decisions V1–V12
├── data-model.md        # Phase 1: domain timeline, read models, view state
├── quickstart.md        # Phase 1: automated checks + touch-first manual scenarios
├── contracts/
│   ├── http-api.md      # GET /api/calendar/days, client routes
│   └── ui-interaction.md# touch, pointer, keyboard, ARIA for all views
├── checklists/
│   └── requirements.md  # spec quality checklist
└── tasks.md             # Phase 2 (/speckit-tasks — not created here)
```

### Source Code (repository root; new files marked +, changed files marked ~)

```text
backend/
├── src/
│   ├── PersonalCalendar.Domain/Calendar/
│   │   ├── + DayTimeline.cs, DayTimelineResult.cs, TimedSegment.cs, HourMark.cs
│   │   └── + AllDayLanes.cs, AllDayBar.cs
│   ├── PersonalCalendar.Application/Events/
│   │   ├── + GetDaysView.cs, DaysViewModel.cs
│   │   └── ~ EventMapping.cs (reuse ToSummary), ~ DependencyInjection.cs
│   └── PersonalCalendar.Api/Endpoints/
│       └── ~ CalendarEndpoints.cs (map /api/calendar/days)
└── tests/
    ├── PersonalCalendar.Domain.Tests/        + DayTimelineTests.cs, AllDayLanesTests.cs
    ├── PersonalCalendar.Application.Tests/   + GetDaysViewTests.cs
    └── PersonalCalendar.Api.Tests/           + DaysEndpointTests.cs, ~ HostSmokeTests.cs (deep link), ~ PerformanceTests.cs

frontend/src/
├── api/        ~ client.ts (getDays), ~ types.ts (DaysView …)
├── lib/        ~ dates.ts (stepPeriod, range), + viewState.ts, + today.ts, ~ newEventDefaults.ts (newEventAt), ~ format.ts (period titles, hour labels)
├── hooks/      + useViewState.ts, useSwipe.ts, useNarrowScreen.ts, useToday.ts
├── components/
│   ├── ~ CalendarScreen.tsx   # owns ViewState, data loading per view, dialogs
│   ├── + ViewHeader.tsx, GoToDateDialog.tsx, InvalidLinkNotice.tsx
│   ├── ~ MonthView.tsx, ~ DayCell.tsx   # header extracted; tap/Enter opens day; narrow markers
│   ├── + DayView.tsx, WeekView.tsx, TimeGrid.tsx, AllDayBars.tsx, WeekList.tsx, NowLine.tsx
│   └── ~ DayOverflowDialog.tsx  # reused for crowded clusters
├── ~ App.tsx      # time zone re-read via useToday
└── ~ index.css    # touch targets, time grid, narrow layouts
(tests colocated as *.test.ts[x])
```

**Structure Decision**: This keeps the 001 layout: the `backend/` .NET solution in four layers and the `frontend/` Vite React app, served as one local process. The new domain logic sits next to `MonthGrid` in `Domain/Calendar`. The frontend gets a `hooks/` folder for the four new hooks, so `components/` holds only components.

## Complexity Tracking

No constitution violations, so this section is intentionally empty.
