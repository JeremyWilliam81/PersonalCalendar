# Implementation Plan: Event Basics

**Branch**: `001-event-basics` | **Date**: 2026-09-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-event-basics/spec.md`

## Summary

A single-user calendar where you can create, view, edit, and delete timed and all-day events, and see them on an accessible month view. Events are stored on the user's own machine.

**Technical approach**:
- **Backend**: a clean-architecture ASP.NET Core (.NET 10) API with four projects: Domain, Application, Infrastructure, and Api. It saves to a local SQLite file through EF Core and only accepts connections from the same machine.
- **Frontend**: a React 19 + TypeScript (Vite) app, which the API also serves as static files.
- **Date/time handling**:
  - All date/time rules live in the C# domain, built with NodaTime and written test-first. This covers validation, resolving DST gaps and overlaps, the month grid, placing events on days, and ordering them.
  - Timed events are stored as UTC instants. All-day events are stored as local dates.
  - The browser's IANA zone is sent with every request. The frontend only formats values with `Intl`.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (SDK 10.0.112 installed). TypeScript 5.x on Node 24.

**Primary Dependencies**:
- **Backend**: ASP.NET Core Minimal APIs, EF Core 10 + `Microsoft.EntityFrameworkCore.Sqlite`, NodaTime 3.x + `NodaTime.Serialization.SystemTextJson`.
- **Frontend**: React 19, Vite.

**Storage**: SQLite file at `%LOCALAPPDATA%\PersonalCalendar\calendar.db` (EF Core migrations).

**Testing**:
- **Backend**: xUnit, `NodaTime.Testing` (`FakeClock`), and `Microsoft.AspNetCore.Mvc.Testing` against a temporary SQLite file.
- **Frontend**: Vitest, React Testing Library, and `vitest-axe`, with `TZ` and the locale fixed.

**Target Platform**: A Windows desktop (the developer's own machine) running a current evergreen browser. The .NET API is portable to macOS and Linux.

**Project Type**: Web application (a local API plus a SPA) that runs only on one machine.

**Performance Goals**: A month view with up to 5,000 events renders in under 1 s (SC-004). The API's share is under 500 ms.

**Constraints**:
- Loopback only, with no authentication.
- No hosted services (clarification Q1).
- Timed events follow the device's current zone (Q2).
- Formatting follows the device locale (Q3).
- WCAG 2.2 AA with keyboard-only use (Principle IV).

**Scale/Scope**: 1 user, up to about 5,000 events, one screen, 4 dialogs, and 5 HTTP endpoints.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | How the plan complies | Status |
|---|---|---|
| **I. Test-First Date & Time Logic** | All date/time logic lives in the Domain (`ScheduleResolver`, `MonthGrid`, the validators) and gets failing xUnit tests before it is implemented. The tests use `FakeClock` and explicit zones, and none depend on the machine's clock or zone. They cover the required hazard list: DST gaps and overlaps, month and year boundaries, 2028-02-29, all-day events, events crossing midnight, `Asia/Kolkata`, and `Australia/Adelaide`. The only frontend date code (FR-005 defaults) is a pure function tested with injected `now` and a fixed `TZ`. | ✅ |
| **II. UTC Storage + IANA Zone** | Timed events are stored as UTC `Instant`s. All-day events are stored as `LocalDate`s, which is the documented exception for dates whose local meaning must be kept (research R3). The IANA zone is stored as `EntryTimeZone`, and every request includes the IANA zone. No fixed offsets or zone abbreviations are stored. Values are converted only at the API/UI boundary. | ✅ |
| **III. Single-Developer Simplicity** | One process, one SQLite file, no hosted services, no state library, no router, no component library, and no date picker. Every dependency is justified in research R1–R9. The four backend projects are what Principle VI requires, so this is not a violation. | ✅ |
| **IV. Accessible by Default** | The month view uses the APG grid pattern with arrow-key navigation. Dialogs use native `<dialog>` and return focus when closed. Every control has an accessible name, a live region makes announcements, and dates are spoken in full. The target is WCAG 2.2 AA, with no known exceptions. See [contracts/ui-interaction.md](./contracts/ui-interaction.md). | ✅ |
| **V. Spec-First, Branch-Per-Feature** | The spec and clarifications are done, the work is on branch `001-event-basics`, and it merges into `main` by PR. | ✅ |
| **VI. Clean Architecture** | Dependencies point inward: Domain ← Application ← Infrastructure/Api. Domain and Application do not reference ASP.NET, EF Core, or HTTP, and ports (`IEventRepository`, `IClock`) are implemented by the adapters. NodaTime is a pure value library, so the Domain may use it. | ✅ |
| **Date & Time Standards** | ISO 8601 with offsets crosses the boundaries. Local wall-clock inputs always travel with their zone. NodaTime is the single zone library, with no hand-written offset math. The clock is injected, and recurrence is not in scope. | ✅ |

**Post-design re-check (after Phase 1)**: ✅ Every principle still passes. The design added optimistic concurrency (R8), which needs no new dependency, and a `422` confirmation for DST gaps (R4), which is domain logic covered by tests. Nothing needs to go in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/001-event-basics/
├── plan.md              # This file
├── research.md          # Phase 0: decisions R1–R11
├── data-model.md        # Phase 1: domain, ports, SQLite schema
├── quickstart.md        # Phase 1: run + validation scenarios
├── contracts/
│   ├── http-api.md      # REST endpoints, payloads, error types
│   └── ui-interaction.md# keyboard, ARIA, dialogs, announcements
├── checklists/
│   └── requirements.md  # spec quality checklist
└── tasks.md             # Phase 2 (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
backend/
├── PersonalCalendar.slnx
├── src/
│   ├── PersonalCalendar.Domain/          # CalendarEvent, EventSchedule, ScheduleResolver, MonthGrid, validation
│   ├── PersonalCalendar.Application/     # use cases, IEventRepository, read models (MonthView, EventDetails)
│   ├── PersonalCalendar.Infrastructure/  # EF Core DbContext, NodaTime value converters, migrations, repository
│   └── PersonalCalendar.Api/             # Minimal API endpoints, problem details, DI, static SPA hosting (wwwroot)
└── tests/
    ├── PersonalCalendar.Domain.Tests/        # test-first date/time hazard tests
    ├── PersonalCalendar.Application.Tests/   # use cases with FakeClock + in-test repository
    └── PersonalCalendar.Api.Tests/           # WebApplicationFactory + temp SQLite: contracts, persistence, 409/422, perf

frontend/
├── package.json, vite.config.ts            # dev proxy /api → API; build output → backend/src/PersonalCalendar.Api/wwwroot
└── src/
    ├── api/            # typed fetch client for contracts/http-api.md
    ├── components/     # MonthView (grid), DayCell, EventButton, EventDetailsDialog, EventFormDialog, ConfirmDialog, LiveRegion
    ├── lib/            # formatting via Intl, newEventDefaults (FR-005)
    └── App.tsx
    (tests colocated as *.test.tsx; test setup pins TZ and locale)
```

**Structure Decision**: This is a web application with a `backend/` .NET solution split into the four layers Principle VI requires, and a `frontend/` Vite React app. The two are separate folders but run and ship as one local process (research R1).

## Complexity Tracking

No constitution violations, so this section is intentionally empty.
