---

description: "Task list for Event Basics implementation"
---

# Tasks: Event Basics

**Input**: Design documents from `/specs/001-event-basics/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/http-api.md](./contracts/http-api.md), [contracts/ui-interaction.md](./contracts/ui-interaction.md), [quickstart.md](./quickstart.md)

**Tests**: REQUIRED. Constitution Principle I (NON-NEGOTIABLE) requires test-first work for all date/time logic, and quickstart.md depends on the API integration and component tests. Within each phase, test tasks come before the implementation tasks they cover. They MUST be written first and MUST fail before the implementation exists (Red → Green → Refactor).

**Test rules for every date/time test**:
- Never read the machine's clock or zone. Use `NodaTime.Testing.FakeClock` and explicit `DateTimeZoneProviders.Tzdb["…"]` zones in C#.
- In the frontend, pass "now" in explicitly, and rely on `TZ=America/Chicago` and the `en-US` locale fixed in the Vitest setup.

**Organization**: Tasks are grouped by user story (US1–US4 from spec.md) so each story can be delivered and tested on its own.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: The user story the task belongs to (US1–US4)

## Path Conventions

- Backend: `backend/src/PersonalCalendar.{Domain,Application,Infrastructure,Api}/`, tests in `backend/tests/PersonalCalendar.{Domain,Application,Api}.Tests/`
- Frontend: `frontend/src/`, with tests next to the code as `*.test.ts(x)`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the solution, the projects, and the tooling.

- [X] T001 Create the .NET 10 solution `backend/PersonalCalendar.slnx` with the class libraries `backend/src/PersonalCalendar.Domain`, `backend/src/PersonalCalendar.Application`, and `backend/src/PersonalCalendar.Infrastructure`, the web project `backend/src/PersonalCalendar.Api` (`dotnet new web`), and the xUnit projects `backend/tests/PersonalCalendar.Domain.Tests`, `backend/tests/PersonalCalendar.Application.Tests`, and `backend/tests/PersonalCalendar.Api.Tests`. Add these project references, which point inward only (plan.md, Principle VI):
  - Application → Domain
  - Infrastructure → Application and Domain
  - Api → Application and Infrastructure
  - Each test project → the project it tests; Api.Tests also → Api
- [X] T002 Add NuGet packages:
  - `NodaTime` to Domain
  - `Microsoft.EntityFrameworkCore.Sqlite` and `Microsoft.EntityFrameworkCore.Design` to Infrastructure
  - `NodaTime.Serialization.SystemTextJson` to Api
  - `NodaTime.Testing` to all three test projects
  - `Microsoft.AspNetCore.Mvc.Testing` to Api.Tests

  Domain and Application MUST NOT reference ASP.NET Core or EF Core.
- [X] T003 [P] Create `backend/Directory.Build.props` with `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, and `<TargetFramework>net10.0</TargetFramework>`. Create `.editorconfig` at the repository root with C# and TS formatting rules.
- [X] T004 [P] Scaffold the React 19 + TypeScript app in `frontend/` with Vite (`npm create vite@latest frontend -- --template react-ts`). Remove the demo content from `frontend/src/App.tsx` and `frontend/src/App.css`.
- [X] T005 [P] Configure `frontend/vite.config.ts`:
  - The dev server passes `/api` through to `http://127.0.0.1:5178`.
  - `build.outDir` is `../backend/src/PersonalCalendar.Api/wwwroot`, with `emptyOutDir: true`.
- [X] T006 [P] Add Vitest, `@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom`, `vitest-axe`, and `jsdom` to `frontend/package.json`. Add the scripts `test` (`vitest run`) and `lint`.
  - Create `frontend/vitest.config.ts` with `environment: "jsdom"`, `env: { TZ: "America/Chicago" }`, and `setupFiles: ["src/test/setup.ts"]`.
  - Create `frontend/src/test/setup.ts`. It registers the jest-dom and vitest-axe matchers, fixes the locale to `en-US` (for example, by stubbing `navigator.language`/`navigator.languages`), and adds a polyfill for `HTMLDialogElement.prototype.showModal` and `close`, which jsdom lacks.
- [X] T007 [P] Add to `.gitignore`: `backend/**/bin/`, `backend/**/obj/`, `backend/src/PersonalCalendar.Api/wwwroot/`, `frontend/node_modules/`, `frontend/dist/`, `*.db`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain types, validation, resolving local times to instants, persistence, the API host, and the frontend shell. Every story depends on these.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Tests first (Principle I) ⚠️

- [X] T008 [P] Write `backend/tests/PersonalCalendar.Domain.Tests/EventScheduleTests.cs`:
  - **`TimedSchedule`**:
    - Rejects `End == Start` and `End < Start`, comparing instants.
    - Accepts `End = Start + 1 minute`.
    - Treats as invalid a pair built from zoned values where the end is 01:45 at the first occurrence and the start is 01:30 at the second occurrence, in `America/Chicago` on 2026-11-01. The instant comparison governs.
  - **`AllDaySchedule`**:
    - Accepts `EndDate == StartDate`.
    - Accepts 2026-10-20 to 2026-10-23.
    - Rejects `EndDate < StartDate`.
    - Accepts `StartDate = EndDate = 2028-02-29`.
- [X] T009 [P] Write `backend/tests/PersonalCalendar.Domain.Tests/ScheduleResolverTests.cs` (research R4). Unless a case names another zone, it uses `America/Chicago`:
  - **Gap**: 2027-03-14T02:30 to 04:00 with `acceptAdjustedTimes=false` gives `AdjustmentRequired(adjustedStart=03:30, adjustedEnd=04:00)`.
  - **Invalid after adjustment**: 2027-03-14T02:30 to 03:00 adjusts to 03:30 → 03:00. The resolver returns `Invalid(end.notAfterStart)`, not `AdjustmentRequired`, because validity is checked on the adjusted instants before the user is asked to confirm.
  - **Gap accepted**: `acceptAdjustedTimes=true` gives a `TimedSchedule` whose start is `2027-03-14T08:30:00Z`.
  - **Overlap**: 2026-11-01T01:30 resolves to the earlier occurrence (`-05:00`, i.e. `06:30Z`) and returns no adjustment.
  - **`Asia/Kolkata`**: 2026-10-14T09:00 gives `03:30Z`.
  - **`Australia/Adelaide`**: on 2026-10-04, the DST start day, 02:30 falls in the gap and adjusts to 03:30, at `+10:30`. On 2026-10-14, 09:00 gives `+10:30`.
  - **Across midnight**: 2026-10-14T22:00 to 2026-10-15T01:00 is valid.
  - **All-day input** never involves zone rules and returns an `AllDaySchedule` unchanged.
  - An end not after the start gives error code `end.notAfterStart`. An all-day end date before the start date gives `endDate.beforeStart`.
- [X] T010 [P] Write `backend/tests/PersonalCalendar.Domain.Tests/CalendarEventTests.cs`:
  - **Title**: the title is trimmed. An empty or whitespace-only title gives `title.required`. 201 characters gives `title.tooLong`, and exactly 200 is accepted.
  - **Location**: it is trimmed, and whitespace becomes `null`. 201 characters gives `location.tooLong`.
  - **Notes**: 5,001 characters gives `notes.tooLong`, and 5,000 is accepted.
  - **Collecting errors**: several invalid fields return **all** their error codes together, not just the first.
  - **Create**: sets `Version=1`, and sets `CreatedUtc` and `UpdatedUtc` from the `FakeClock`. It stores `EntryTimeZone` as the IANA id.

### Implementation

- [X] T011 [P] Create `backend/src/PersonalCalendar.Domain/Events/EventId.cs`, a record struct that wraps a `Guid` with `New()`. Create `backend/src/PersonalCalendar.Domain/Events/EventSchedule.cs`, a sealed hierarchy:
  - `TimedSchedule(Instant Start, Instant End)`, with the invariant "`End > Start`".
  - `AllDaySchedule(LocalDate StartDate, LocalDate EndDate)`, with the invariant "`EndDate >= StartDate`" (the end date is included).
- [X] T012 [P] Create `backend/src/PersonalCalendar.Domain/Validation/ValidationError.cs` (Field, Code) and `ValidationResult.cs`, which collects every error. Add constants for the codes `title.required`, `title.tooLong`, `location.tooLong`, `notes.tooLong`, `end.notAfterStart`, `endDate.beforeStart`, and `timeZone.unknown`.
- [X] T013 Implement `backend/src/PersonalCalendar.Domain/Events/ScheduleResolver.cs`, a pure static class:
  - `Resolve(bool isAllDay, LocalDateTime? start, LocalDateTime? end, LocalDate? startDate, LocalDate? endDate, DateTimeZone zone, bool acceptAdjustedTimes)` returns one of `Resolved(EventSchedule)`, `AdjustmentRequired(LocalDateTime adjustedStart, LocalDateTime adjustedEnd)`, or `Invalid(ValidationResult)`.
  - It uses `zone.ResolveLocal(local, Resolvers.LenientResolver)`.
  - It detects a gap when `zone.MapLocal(local).Count == 0`.
  - This task makes T008 and T009 pass.
- [X] T014 Implement `backend/src/PersonalCalendar.Domain/Events/CalendarEvent.cs`:
  - `static Create(title, location, notes, EventSchedule, DateTimeZone entryZone, IClock)` returns either `CalendarEvent` or `ValidationResult`.
  - Field rules, quoted from data-model.md:
    - Title: "Leading and trailing spaces are removed. Must have 1–200 characters."
    - Location: "Spaces are trimmed. An empty value becomes `null`. At most 200 characters."
    - Notes: "Spaces are trimmed. An empty value becomes `null`. At most 5,000 characters."
  - Properties: `Id`, `Title`, `Location`, `Notes`, `Schedule`, `EntryTimeZone` (IANA id string), `CreatedUtc`, `UpdatedUtc`, and `Version` (starts at 1).
  - Add a private parameterless constructor and setters for EF, which do not leak into the domain API. *(Implemented instead as a persistence `EventRow` plus `CalendarEvent.Rehydrate`, so the domain type has no EF members at all.)*
  - This task makes T010 pass.
- [X] T015 [P] Create the Application ports and models:
  - `backend/src/PersonalCalendar.Application/Abstractions/IEventRepository.cs`, with `GetAsync(EventId)`, `ListOverlappingAsync(Instant from, Instant to, LocalDate fromDate, LocalDate toDate)`, `AddAsync(CalendarEvent)`, `UpdateAsync(CalendarEvent, int expectedVersion)`, and `DeleteAsync(EventId, int expectedVersion)`.
  - `backend/src/PersonalCalendar.Application/Abstractions/ConcurrencyConflictException.cs`.
  - `backend/src/PersonalCalendar.Application/Events/EventInput.cs`, mirroring `EventInput` in contracts/http-api.md, with `Version` nullable.
  - `backend/src/PersonalCalendar.Application/Events/EventDetails.cs`, with zoned start and end, or dates, plus `Version`.
  - `backend/src/PersonalCalendar.Application/Events/UseCaseResult.cs`: `Ok<T>`, `ValidationFailed`, `AdjustmentRequired`, `NotFound`, `Conflict`.
  - `backend/src/PersonalCalendar.Application/Time/ZoneLookup.cs`, which wraps `IDateTimeZoneProvider` and returns `timeZone.unknown` for ids it doesn't recognize.
- [X] T016 Create `backend/src/PersonalCalendar.Infrastructure/Persistence/CalendarDbContext.cs` and `EventConfiguration.cs`, mapping the `Events` table exactly as in data-model.md:
  - **Columns**: `Id`, `Title`, `Location`, `Notes`, `IsAllDay`, `StartUtc`, `EndUtc`, `StartDate`, `EndDate`, `EntryTimeZone`, `CreatedUtc`, `UpdatedUtc`, `Version`.
  - **Concurrency**: `Version` is marked `IsConcurrencyToken()`.
  - **Value converters** in `backend/src/PersonalCalendar.Infrastructure/Persistence/NodaTimeConverters.cs`:
    - `Instant` ↔ text in the fixed format `uuuu-MM-ddTHH:mm:ss.fffZ` (`InstantPattern.CreateWithInvariantCulture("uuuu'-'MM'-'dd'T'HH':'mm':'ss.fff'Z'")`).
    - `LocalDate` ↔ `uuuu-MM-dd`.
  - **Schedule mapping**: map `EventSchedule` onto the flat columns (`IsAllDay` plus the nullable instant and date columns).
  - **CHECK constraint** `CK_Events_Schedule`: "(IsAllDay = 0 AND StartUtc IS NOT NULL AND EndUtc IS NOT NULL AND StartDate IS NULL AND EndDate IS NULL AND EndUtc > StartUtc) OR (IsAllDay = 1 AND StartDate IS NOT NULL AND EndDate IS NOT NULL AND StartUtc IS NULL AND EndUtc IS NULL AND EndDate >= StartDate)".
  - **Indexes**: `IX_Events_StartUtc_EndUtc` and `IX_Events_StartDate_EndDate`.
- [X] T017 Generate the initial EF Core migration in `backend/src/PersonalCalendar.Infrastructure/Persistence/Migrations/` with `dotnet ef migrations add InitialCreate --project backend/src/PersonalCalendar.Infrastructure --startup-project backend/src/PersonalCalendar.Api`. Check that the CHECK constraint and both indexes are in the migration.
- [X] T018 Implement `backend/src/PersonalCalendar.Infrastructure/Persistence/EfEventRepository.cs` with `GetAsync`, `AddAsync`, and `ListOverlappingAsync`:
  - The overlap query is "`(StartUtc < @toUtc AND EndUtc > @fromUtc) OR (StartDate <= @toDate AND EndDate >= @fromDate)`".
  - For now, `UpdateAsync` and `DeleteAsync` throw `NotImplementedException`; US3 implements them.
  - Register the context and repository in `backend/src/PersonalCalendar.Infrastructure/DependencyInjection.cs` (`AddInfrastructure(connectionString)`).
- [X] T019 Implement the Api host in `backend/src/PersonalCalendar.Api/Program.cs`:
  - Kestrel listens on `http://127.0.0.1:5178` only.
  - The connection string defaults to `Data Source=%LOCALAPPDATA%\PersonalCalendar\calendar.db`, from `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)`; create the directory if needed. It can be overridden with `ConnectionStrings:Calendar`.
  - `Database.MigrateAsync()` runs at startup.
  - Register `IClock` as `SystemClock.Instance` and `IDateTimeZoneProvider` as `DateTimeZoneProviders.Tzdb`.
  - Configure System.Text.Json with `ConfigureForNodaTime`, using camelCase names.
  - Add `UseDefaultFiles`, `UseStaticFiles`, and `MapFallbackToFile("index.html")`.
  - Add `public partial class Program {}` so tests can use it.
- [X] T020 Create `backend/src/PersonalCalendar.Api/Http/ProblemResults.cs`, which maps `UseCaseResult` to RFC 9457 problem+json responses per contracts/http-api.md:
  - `400` with `type: "validation"` and an `errors` map from field to codes (including `timeZone.unknown`).
  - `404`.
  - `409` with `type: "concurrency-conflict"`.
  - `422` with `type: "dst-adjustment-required"` and `adjustedStart`, `adjustedEnd`, and `timeZone`.

  Also add an exception handler that turns `DbUpdateException` and `SqliteException` into `500` with `type: "save-failed"`.
- [X] T021 Create `backend/tests/PersonalCalendar.Api.Tests/ApiFactory.cs`, a `WebApplicationFactory<Program>`:
  - It uses a unique temporary SQLite file per test (`Path.GetTempFileName()`) and deletes it on dispose.
  - It replaces `IClock` with `FakeClock` fixed at `2026-09-29T15:00:00Z`.
  - It exposes `CreateRestartedFactory()`, which returns a new factory on the **same** database file, for the persistence tests.
- [X] T022 [P] Create the typed frontend API layer:
  - `frontend/src/api/types.ts`: `MonthView`, `DayCell`, `EventSummary`, `EventDetails`, `EventInput`, `ValidationProblem`, `DstAdjustmentProblem`, `ConflictProblem`, `SaveFailedProblem`, all per contracts/http-api.md.
  - `frontend/src/api/client.ts`: a `fetch` wrapper that returns a discriminated result (`ok` | `validation` | `dstAdjustment` | `conflict` | `notFound` | `saveFailed` | `network`). A failed `fetch`, such as when the API is stopped, maps to `saveFailed`/`network` and never throws.
  - `frontend/src/api/timeZone.ts`: `currentTimeZone()` returns `Intl.DateTimeFormat().resolvedOptions().timeZone`.
- [X] T023 [P] Write `frontend/src/lib/format.test.ts` first, then `frontend/src/lib/format.ts`, using `Intl.DateTimeFormat(undefined, { timeZone })`:
  - `formatTimeShort(isoWithOffset, zone)` gives "9:00 AM".
  - `formatFullDate(date)` gives "Wednesday, October 14, 2026".
  - `formatTimedRange(start, end, zone)` gives "Wednesday, October 14, 2026, 9:00 AM to 10:00 AM". When the range crosses midnight, the end date is also written in full.
  - `formatAllDayRange(startDate, endDate)`: a single day gives "Wednesday, October 14, 2026". Several days give "Monday, October 12 to Friday, October 16, 2026".
  - `formatMonthTitle(year, month)` gives "October 2026".
  - Tests pin the `en-US` locale and pass the zone explicitly, including `Asia/Kolkata`.
- [X] T024 [P] Create `frontend/src/components/LiveRegion.tsx`, a single visually hidden `aria-live="polite"` region, and the hook `useAnnounce()` in `frontend/src/components/useAnnounce.ts`, exposed through React context. Write `frontend/src/components/LiveRegion.test.tsx`: a message passed to `announce("…")` is rendered in the live region.
- [X] T025 Create the app shell in `frontend/src/App.tsx` and `frontend/src/main.tsx`. It renders the `LiveRegion` provider and a `<main>` with the heading placeholder, reads `currentTimeZone()` once per page load, and passes it down.

**Checkpoint**: The domain tests (T008–T010) pass, the API starts on loopback and creates the database, and the frontend builds.

---

## Phase 3: User Story 1 - Create an event and see it on the month view (Priority: P1) 🎯 MVP

**Goal**: Create a timed event (title, start, end, optional location and notes), see it on the correct day(s) of an accessible month view, move between months, and have it persist across sessions.

**Independent Test**: Start with an empty database, create "Dentist" on 2026-10-14 from 09:00 to 10:00, and check that it shows on Oct 14. Restart the API and reload, and check that it is still there (quickstart scenarios 1–3 and 7).

### Tests for User Story 1 (write first, must fail) ⚠️

- [X] T026 [P] [US1] Write `backend/tests/PersonalCalendar.Domain.Tests/MonthGridTests.cs` for `MonthGrid.Build(year, month, zone, today, events)` (research R6):
  - **Grid size**: February 2026 gives exactly 4 weeks (2026-02-01 to 2026-02-28). October 2026 gives 5 weeks (2026-09-27 to 2026-10-31). August 2026 gives 6 weeks (2026-07-26 to 2026-09-05). Every week runs Sunday to Saturday.
  - **Flags**: `InMonth` is false for days before and after the month. `IsToday` is true only on `today`.
  - **Across midnight**: a timed event from 2026-10-14T22:00 to 2026-10-15T01:00 (Chicago) appears on the 14th and the 15th.
  - **Ends at midnight**: an event ending exactly at 2026-10-15T00:00 local appears on the 14th only.
  - **Across months**: an event from 2026-10-30T10:00 to 2026-11-02T10:00 appears on Oct 30 and 31 in the October grid (which ends on Saturday Oct 31), and on Nov 1 and 2 in the November grid.
  - **Days from neighbouring months**: an event on 2026-10-01 also appears in the September 2026 grid, on the trailing Oct 1 cell with `InMonth=false`. The September grid runs from 2026-08-30 to 2026-10-03.
  - **Year boundary**: an event from 2026-12-31T23:00 to 2027-01-01T01:00 appears on Dec 31 and Jan 1.
  - **Order**: timed events are ordered by start instant, then by title.
  - **Zones**: in the `Asia/Kolkata` zone, the instant `2026-10-14T20:00Z` lands on 2026-10-15.
  - **DST**: on the DST day 2026-11-01 in Chicago, an event from 01:30 (the earlier occurrence) to 02:30 appears once, on Nov 1.
- [X] T027 [P] [US1] Write `backend/tests/PersonalCalendar.Application.Tests/CreateEventTests.cs` and `GetMonthViewTests.cs`, using `FakeClock` and an in-test `InMemoryEventRepository` (in `backend/tests/PersonalCalendar.Application.Tests/Fakes/InMemoryEventRepository.cs`):
  - **Create**:
    - Valid input returns `Ok(EventDetails)` with `Version=1`.
    - An unknown `timeZone` returns `ValidationFailed(timeZone.unknown)`.
    - Input in the DST gap returns `AdjustmentRequired`, and nothing is saved.
  - **Month view**:
    - The UTC query range is computed from the grid's first local midnight up to (but not including) the midnight after its last day. Check this across the 2026-11-01 Chicago DST change.
    - With no year or month, it returns the current month in the zone according to the clock. Test this with the clock set to `2026-10-01T03:00Z`: that is still September 30 in `America/Chicago` but already October 1 in `Asia/Kolkata`.
- [X] T028 [P] [US1] Write `backend/tests/PersonalCalendar.Api.Tests/CreateAndMonthViewEndpointTests.cs` against contracts/http-api.md:
  - `POST /api/events` with valid input returns `201` with a `Location` header and a body matching the contract, with `start` = `2026-10-14T09:00:00-05:00`.
  - An end equal to the start returns `400` with `errors.end = ["end.notAfterStart"]`.
  - A whitespace-only title returns `400` with `title.required`.
  - A gap time returns `422` with `type: "dst-adjustment-required"`. Resending with `acceptAdjustedTimes: true` returns `201`.
  - `timeZone=Mars/Base` returns `400` with `timeZone.unknown`.
  - `GET /api/calendar/month?year=2026&month=10&timeZone=America/Chicago` returns the event on 2026-10-14. Omitting year and month returns September 2026, the month of the fake clock.
  - **Persistence (FR-013)**: create an event, then use `CreateRestartedFactory()`. `GET` still returns the event with the same fields.
- [X] T029 [P] [US1] Write `frontend/src/lib/newEventDefaults.test.ts` for `newEventDefaults(day: string, now: Date)`, which takes the time of day of the next whole hour after `now` and places it on `day`, lasting 1 hour (FR-005, research R5):
  - With now at 14:20 and day 2026-10-20, it returns `start="2026-10-20T15:00"` and `end="2026-10-20T16:00"`.
  - With now at exactly 14:00, it returns 15:00.
  - With now at 23:10 and day 2026-10-20, it returns `start="2026-10-20T00:00"` and `end="2026-10-20T01:00"`.
- [X] T030 [P] [US1] Write `frontend/src/components/MonthView.test.tsx`, following contracts/ui-interaction.md "Month view", with a stubbed API client and fixed `MonthView` data:
  - **Roles**: the grid has `role="grid"` and is labelled "October 2026". The weekday column headers run Sunday to Saturday.
  - **Tab order**: exactly one day cell has `tabIndex=0`.
  - **Keys**: ←/→ move by a day, ↑/↓ by a week, and Home/End go to the start or end of the week. Page Up and Page Down call `onChangeMonth(-1/+1)`, and so does moving the focus off the edge of the grid.
  - **Enter** on a day calls `onCreate(date)`.
  - **Names**: each day's accessible name is the full date. An event button's accessible name is "Dentist, Wednesday, October 14, 2026, 9:00 AM to 10:00 AM".
  - **Overflow**: with 6 events on a day and `maxVisible=3`, a button named "3 more events on Wednesday, October 14, 2026" appears, and clicking it opens a dialog that lists all 6.
  - **Month buttons**: Previous month, Today, and Next month have accessible names.
  - `axe` reports no violations.
- [X] T031 [P] [US1] Write `frontend/src/components/EventFormDialog.test.tsx` for create mode, following contracts/ui-interaction.md "Event form":
  - **Fields**: every field has a visible label. When opened from a day, the start date is preset, and focus starts on Title.
  - **Validation**: submitting a `validation` result shows "Title is required." and "End must be after start." under their fields. Those fields get `aria-invalid="true"` and `aria-describedby`, focus moves to the first field with an error, and "2 errors. Fix the highlighted fields." is announced.
  - **DST adjustment**: a `dstAdjustment` result shows the adjusted-time message with **Save with adjusted time**. Clicking it resends with `acceptAdjustedTimes: true`.
  - **Save failed**: a `saveFailed` result shows "Couldn't save the event. Your changes are still here. Try again.", and every input keeps its value (FR-014).
  - **Discard**: Cancel or Escape after an edit opens "Discard your changes?". **Keep editing** returns to the form (FR-012). With no changes, Cancel closes without asking.
  - **Success**: announces "Saved Dentist, Wednesday, October 14, 2026, 9:00 AM to 10:00 AM." and returns focus to the control that opened the dialog.
  - `axe` reports no violations.

### Implementation for User Story 1

- [X] T032 [US1] Implement `backend/src/PersonalCalendar.Domain/Calendar/MonthGrid.cs`, a pure static function. Also add `DayCell.cs` and `MonthGridResult.cs`:
  - `GridRange(year, month)` returns the grid's first date (the Sunday on or before the 1st) and last date (the Saturday on or after the last day of the month).
  - A timed event covers the local dates from `Start.InZone(zone).Date` to `(End − 1 tick).InZone(zone).Date`.
  - Each day orders all-day events first (by start date, then title), then timed events by start instant, then title.
  - This task makes T026 pass.
- [X] T033 [US1] Implement the use cases in `backend/src/PersonalCalendar.Application/Events/`:
  - `CreateEvent.cs`: `HandleAsync(EventInput)`. It resolves the zone through `ZoneLookup`, calls `ScheduleResolver.Resolve`, calls `CalendarEvent.Create` with `IClock`, calls `AddAsync`, and returns `EventDetails` in the zone.
  - `GetMonthView.cs`: `HandleAsync(int? year, int? month, string timeZone)`. It computes the grid range, converts the first date's start-of-day and the day after the last date to instants with `zone.AtStartOfDay`, calls `ListOverlappingAsync`, and calls `MonthGrid.Build` with `today = clock.GetCurrentInstant().InZone(zone).Date`.
  - `EventMapping.cs`: maps domain events to `EventDetails` and `EventSummary`, with offset date-times in the zone.
  - Register the use cases in `backend/src/PersonalCalendar.Application/DependencyInjection.cs`.
  - These make T027 pass.
- [X] T034 [US1] Map `POST /api/events` and `GET /api/calendar/month` in `backend/src/PersonalCalendar.Api/Endpoints/EventEndpoints.cs` and `CalendarEndpoints.cs`, called from `Program.cs`:
  - Read the local `start` and `end` as `LocalDateTime` (pattern `uuuu-MM-ddTHH:mm`) and `startDate`/`endDate` as `LocalDate`.
  - Year must be 1–9998 and month 1–12, or the endpoint returns `400`.
  - Use `ProblemResults` for every non-OK result.
  - This task makes T028 pass.
- [X] T035 [P] [US1] Implement `frontend/src/lib/newEventDefaults.ts` so that T029 passes.
- [X] T036 [US1] Implement `frontend/src/components/MonthView.tsx`, `MonthHeader.tsx`, `DayCell.tsx`, `EventButton.tsx`, and `DayOverflowDialog.tsx`, following contracts/ui-interaction.md:
  - **Grid**: an APG grid with a roving `tabindex` and the keyboard map (←/→/↑/↓/Home/End/Page Up/Page Down/Enter/Space). It fetches `GET /api/calendar/month` for the displayed year and month plus the zone. The Today button requests the month without year or month.
  - **Navigation**: after a month change, focus stays on the equivalent day, or the last day of the month when that day doesn't exist. "October 2026" is announced.
  - **Styling**: days outside the month look muted. Long titles are shortened visually with CSS ellipsis, while the full title stays in the accessible name.
  - **Overflow**: an "N more" button uses `maxVisible` computed from the cell height, with a default of 3.
  - This task makes T030 pass.
- [X] T037 [US1] Implement `frontend/src/components/ConfirmDialog.tsx`, a reusable native `<dialog>` with `showModal()`. It takes a message, confirm and cancel labels, and `initialFocus: "confirm" | "cancel"`, and returns focus to the control that opened it when it closes.
- [X] T038 [US1] Implement `frontend/src/components/EventFormDialog.tsx` in create mode:
  - **Fields**: Title (`required`, `maxLength=200`), Start and End (`datetime-local`), Location (`maxLength=200`), Notes (`textarea`, `maxLength=5000`).
  - **Initial values** come from `newEventDefaults(day, new Date())`.
  - **Submitting** calls `POST /api/events` with `timeZone` from `currentTimeZone()`.
  - **Results** are handled per T031: field errors, the DST adjustment confirmation, save failed while keeping all input, and a discard confirmation through `ConfirmDialog`.
  - **On success**, it announces the save and refreshes the month view.
  - Map error codes to the messages in data-model.md: "Title is required.", "Title must be 200 characters or fewer.", "Location must be 200 characters or fewer.", "Notes must be 5,000 characters or fewer.", "End must be after start.", "End date must be on or after the start date.", "Unknown time zone."
  - This task makes T031 pass.
- [X] T039 [US1] Wire US1 into `frontend/src/App.tsx`: render `MonthView`, which opens on the current month through the Today request. `onCreate(date)` opens `EventFormDialog`, and after a save the displayed month is refetched.

**Checkpoint**: US1 is fully usable. Quickstart scenarios 1, 2, 3, and 7 pass. This is the MVP.

---

## Phase 4: User Story 2 - View an event's details (Priority: P1)

**Goal**: Selecting an event shows all its details, and empty optional fields are left out.

**Independent Test**: With one event that has every field filled, select it and check that each field is shown. Close the dialog and check that focus returns to the event and the same month is shown (quickstart scenario 4).

### Tests for User Story 2 (write first, must fail) ⚠️

- [X] T040 [P] [US2] Write `backend/tests/PersonalCalendar.Application.Tests/GetEventDetailsTests.cs`:
  - Returns the details of a timed event with zoned start and end in the requested zone. Test the same event in `America/Chicago` and `America/New_York`, expecting 9:00 AM and 10:00 AM (clarification Q2).
  - Returns `NotFound` for an unknown id.
  - An unknown zone returns `timeZone.unknown`.
- [X] T041 [P] [US2] Write `backend/tests/PersonalCalendar.Api.Tests/GetEventEndpointTests.cs`:
  - `GET /api/events/{id}?timeZone=America/Chicago` returns `200` with the contract body (`location`/`notes` are `null` when empty, and `version` is included).
  - An unknown id returns `404`.
  - A missing `timeZone` returns `400`.
- [X] T042 [P] [US2] Write `frontend/src/components/EventDetailsDialog.test.tsx`:
  - It shows the title as a heading, the full range "Wednesday, October 14, 2026, 9:00 AM to 10:00 AM", the location, and the notes.
  - When location and notes are `null`, their labels are **not** rendered.
  - It has **Edit**, **Delete**, and **Close** buttons, with Edit and Delete hidden or disabled until US3 provides the handlers.
  - Escape and Close return focus to the event button that opened the dialog.
  - `axe` reports no violations.

### Implementation for User Story 2

- [X] T043 [US2] Implement `backend/src/PersonalCalendar.Application/Events/GetEventDetails.cs` with `HandleAsync(EventId id, string timeZone)`, reusing `EventMapping`. Register it in `DependencyInjection.cs`. This task makes T040 pass.
- [X] T044 [US2] Map `GET /api/events/{id}` in `backend/src/PersonalCalendar.Api/Endpoints/EventEndpoints.cs`. This task makes T041 pass.
- [X] T045 [US2] Implement `frontend/src/components/EventDetailsDialog.tsx`, a native `<dialog>` that fetches `GET /api/events/{id}` for the current zone. It formats values with `format.ts` and leaves out empty optional fields. The optional `onEdit` and `onDelete` props render Edit and Delete only when provided. This task makes T042 pass.
- [X] T046 [US2] Wire `EventButton` and `DayOverflowDialog` items in `frontend/src/App.tsx` so they open `EventDetailsDialog`, and closing it returns focus to the event button that opened it.

**Checkpoint**: US1 and US2 both work. Quickstart scenario 4 passes.

---

## Phase 5: User Story 3 - Edit and delete events (Priority: P2)

**Goal**: Change any field of an event or delete it, with confirmation, protection against a stale version from another tab, and persistence.

**Independent Test**: Create an event, then edit its title, time, and location, and check that the month view and details show the change and that it persists. Delete it, cancelling once and confirming the second time, and check that it stays gone after a restart (quickstart scenarios 5 and 13).

### Tests for User Story 3 (write first, must fail) ⚠️

- [X] T047 [P] [US3] Extend `backend/tests/PersonalCalendar.Domain.Tests/CalendarEventTests.cs` with `Update(...)` tests:
  - A valid update replaces the fields and schedule, increments `Version` by 1, and sets `UpdatedUtc` from the `FakeClock` while leaving `CreatedUtc` unchanged.
  - An invalid update returns every error and leaves the event unchanged.
- [X] T048 [P] [US3] Write `backend/tests/PersonalCalendar.Application.Tests/UpdateEventTests.cs` and `DeleteEventTests.cs`:
  - **Update**: returns `Ok` with `Version = n+1`. A stale version returns `Conflict`, an unknown id returns `NotFound`, an invalid end returns `ValidationFailed` and leaves the stored event unchanged, and a gap time returns `AdjustmentRequired`.
  - **Delete**: with the current version it removes the event. A stale version returns `Conflict`, and an unknown id returns `NotFound`.
- [X] T049 [P] [US3] Write `backend/tests/PersonalCalendar.Api.Tests/UpdateDeleteEndpointTests.cs`:
  - **PUT** `/api/events/{id}`:
    - Moving the event from Oct 14 to Oct 15 returns `200` with `version: 2`, and `GET /api/calendar/month` shows it on Oct 15 only.
    - A stale `version` returns `409` with `type: "concurrency-conflict"`.
    - An end not after the start returns `400`, and a following `GET` still returns the old values.
    - An unknown id returns `404`.
  - **DELETE** `/api/events/{id}?version=n`:
    - Returns `204`. After `CreateRestartedFactory()`, `GET` returns `404` (permanent deletion).
    - A stale version returns `409`.
- [X] T050 [P] [US3] Extend `frontend/src/components/EventFormDialog.test.tsx` with edit-mode tests:
  - Opening it with existing `EventDetails` pre-fills every field, converting the offset date-times to `datetime-local` values in the zone.
  - Saving sends `PUT` with `version`.
  - A `conflict` result shows "This event was changed in another window." with a **Reload event** button that re-fetches the event and fills the form again.
  - Cancel after a change asks for confirmation. After **Discard**, nothing is sent.
- [X] T051 [P] [US3] Write `frontend/src/components/DeleteFlow.test.tsx` for delete from the details dialog:
  - It opens `ConfirmDialog` with the text 'Delete "Dentist"? This can't be undone.' and focus starts on **Cancel**.
  - Cancel keeps the event and makes no request.
  - Delete sends `DELETE` with the version, announces "Deleted Dentist.", closes the dialogs, and refreshes the month.
  - A `409` shows the conflict message.

### Implementation for User Story 3

- [X] T052 [US3] Add `Update(title, location, notes, EventSchedule, DateTimeZone entryZone, IClock)` to `backend/src/PersonalCalendar.Domain/Events/CalendarEvent.cs`. It uses the same validation as `Create`, increments `Version`, and sets `UpdatedUtc`. This task makes T047 pass.
- [X] T053 [US3] Implement `UpdateAsync` and `DeleteAsync` in `backend/src/PersonalCalendar.Infrastructure/Persistence/EfEventRepository.cs`:
  - Set the original value of the `Version` concurrency token to `expectedVersion`.
  - Translate `DbUpdateConcurrencyException` into `ConcurrencyConflictException`.
  - `DeleteAsync` with a stale version MUST NOT delete the event.
- [X] T054 [US3] Implement `backend/src/PersonalCalendar.Application/Events/UpdateEvent.cs` (`HandleAsync(EventId, EventInput)`, where `Version` is required) and `DeleteEvent.cs` (`HandleAsync(EventId, int version)`), and register both. These make T048 pass.
- [X] T055 [US3] Map `PUT /api/events/{id}` and `DELETE /api/events/{id}` in `backend/src/PersonalCalendar.Api/Endpoints/EventEndpoints.cs`. A missing `version` returns `400`. This task makes T049 pass.
- [X] T056 [US3] Add edit mode to `frontend/src/components/EventFormDialog.tsx`:
  - It takes an `event?: EventDetails` prop and pre-fills the fields from it.
  - It sends `PUT` with `version`, handles the `conflict` result with **Reload event**, and keeps the discard confirmation.
  - This task makes T050 pass.
- [X] T057 [US3] Wire **Edit** and **Delete** in `frontend/src/App.tsx`:
  - **Edit**: `EventDetailsDialog` `onEdit` opens `EventFormDialog` in edit mode.
  - **Delete**: `onDelete` opens `ConfirmDialog` with `initialFocus: "cancel"`. On confirm, it sends `DELETE` with `version`, then announces the deletion, closes the dialogs, and refetches the month.
  - This task makes T051 pass.

**Checkpoint**: US1–US3 work. Quickstart scenarios 5 and 13 pass.

---

## Phase 6: User Story 4 - All-day events (Priority: P2)

**Goal**: Mark an event as all-day. It then has only dates, which include the end date and can span several days, never shift between time zones, and are shown before timed events.

**Independent Test**: Create a one-day all-day event and one from Oct 20 to Oct 23. Check that both show as all-day on each day they cover, with no times, before any timed events, and that they are unchanged after a restart and after a zone change (quickstart scenarios 6 and 8).

### Tests for User Story 4 (write first, must fail) ⚠️

- [X] T058 [P] [US4] Extend `backend/tests/PersonalCalendar.Domain.Tests/MonthGridTests.cs` with all-day cases:
  - An all-day event from 2026-10-20 to 2026-10-23 appears on the 20th, 21st, 22nd, and 23rd, and a one-day all-day event appears on its day only.
  - All-day events come before timed events, ordered by start date and then title.
  - An all-day event on 2026-10-20 lands on the 20th for the zones `America/Chicago`, `Asia/Kolkata`, `Australia/Adelaide`, and `Pacific/Kiritimati` (FR-016).
  - An all-day event on 2028-02-29 appears in the February 2028 grid.
  - An all-day event from 2026-10-30 to 2026-11-02 appears in both months' grids.
- [X] T059 [P] [US4] Write `backend/tests/PersonalCalendar.Api.Tests/AllDayEndpointTests.cs`:
  - `POST` with `isAllDay: true`, `startDate` = `endDate` = `2026-10-14` returns `201`.
  - An `endDate` before the start returns `400` with `errors.endDate = ["endDate.beforeStart"]`.
  - `GET` month returns the event on each covered day with `startDate`/`endDate` and no `start`/`end`.
  - `PUT` switching a timed event to all-day returns `200`, with `start`/`end` `null` and `startDate`/`endDate` set.
  - Creating the event in `America/Chicago` and reading the month in `Asia/Tokyo` shows the same dates.
- [X] T060 [P] [US4] Extend `frontend/src/components/EventFormDialog.test.tsx` with all-day tests:
  - Checking **All day** replaces the Start and End `datetime-local` fields with Start date and End date (`date`) fields. These keep the date parts of the previous values (User Story 4, scenario 5).
  - Unchecking **All day** brings back `datetime-local` fields with empty times, and submitting without times shows an error.
  - The payload sends `isAllDay: true` with `startDate`/`endDate`.
  - The error "End date must be on or after the start date." appears on End date.
- [X] T061 [P] [US4] Extend `frontend/src/components/MonthView.test.tsx` and `EventDetailsDialog.test.tsx` with all-day tests:
  - An all-day event button shows no time, uses the all-day style, and has the accessible name "Vacation, all day, Monday, October 12 to Friday, October 16, 2026".
  - The details dialog shows the date range with no times.

### Implementation for User Story 4

- [X] T062 [US4] Extend `backend/src/PersonalCalendar.Domain/Calendar/MonthGrid.cs` so it places `AllDaySchedule` on every date from `StartDate` to `EndDate`, both included, never through instants or zones, and orders all-day events first. This task makes T058 pass.
- [X] T063 [US4] Check and fix the all-day path end to end:
  - `EventMapping.cs` maps an all-day event to `startDate`/`endDate` with `start`/`end` `null`.
  - `EventEndpoints.cs` parses `startDate`/`endDate`.
  - `EfEventRepository.ListOverlappingAsync` includes the date-range clause.

  This task makes T059 pass.
- [X] T064 [US4] Add an **All day** checkbox to `frontend/src/components/EventFormDialog.tsx`:
  - It switches the inputs between `datetime-local` and `date`. When switching to all-day, it keeps the dates; when switching back, it clears the times.
  - It sends the matching payload.
  - This task makes T060 pass.
- [X] T065 [US4] Update `frontend/src/components/EventButton.tsx` (all-day style, no time, accessible name from `formatAllDayRange`) and `frontend/src/components/EventDetailsDialog.tsx` (all-day date range, no times). These make T061 pass.

**Checkpoint**: All four stories work on their own. Quickstart scenarios 6 and 8 pass.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Performance, the hazard sweep across the whole stack, documentation, and the final checks the constitution requires.

- [X] T066 [P] Write `backend/tests/PersonalCalendar.Api.Tests/PerformanceTests.cs`. `MonthView_With5000Events_RespondsQuickly` adds 5,000 events directly through `CalendarDbContext` across 2026–2027, then asserts that a warm `GET /api/calendar/month?year=2026&month=10&timeZone=America/Chicago` returns in under 500 ms (SC-004).
- [X] T067 [P] Write `backend/tests/PersonalCalendar.Api.Tests/SaveFailureTests.cs`. Make the database file read-only, or use a connection string that points to a path that doesn't exist, so the save fails. Assert that `POST` returns `500` with `type: "save-failed"` and no event is stored (FR-014).
- [X] T068 [P] Write `frontend/src/App.test.tsx`, a smoke test that runs `axe` over the whole page with the month view, the details dialog, and the form dialog open in turn. It asserts that there are no violations and that Tab order goes header → grid → out.
- [X] T069 [P] Update `README.md` with prerequisites, the commands to run, test, and build as a single process (copied from quickstart.md), the database location `%LOCALAPPDATA%\PersonalCalendar\calendar.db`, and a note that the API listens only on 127.0.0.1.
- [X] T070 Run `dotnet test backend/PersonalCalendar.slnx`, `npm test --prefix frontend`, and `npm run lint --prefix frontend`, and fix any failures.
- [ ] T071 Work through quickstart.md scenarios 1–13 by hand, keyboard only. For scenario 11, use Narrator or NVDA. Record the results and any WCAG 2.2 AA exceptions in the PR description, as Principle IV and the Merge gate in the constitution require.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup and blocks every story.
- **US1 (Phase 3)**: depends on Foundational. This is the MVP.
- **US2 (Phase 4)**: depends on Foundational. Wiring it into the UI (T046) needs the event buttons from US1 (T036).
- **US3 (Phase 5)**: depends on Foundational. Its UI tasks (T056, T057) build on the form from US1 (T038) and the details dialog from US2 (T045).
- **US4 (Phase 6)**: depends on Foundational. It extends the grid from US1 (T032, T036) and the form (T038). T060's timed-to-all-day switch covers edit mode, so it assumes US3 (T056) is done.
- **Polish (Phase 7)**: depends on every story being done.

### Within each phase

- Test tasks come before implementation and MUST fail first (Principle I).
- The order is Domain → Application → Infrastructure/Api → Frontend.
- These tasks edit the same file in sequence, so they are not [P] with each other:
  - `EventFormDialog.tsx`: T038 → T056 → T064
  - `EventEndpoints.cs`: T034 → T044 → T055 → T063
  - `MonthGrid.cs`: T032 → T062
  - `EfEventRepository.cs`: T018 → T053
  - `App.tsx`: T039 → T046 → T057

### Parallel Opportunities

- **Setup**: T003–T007 can run in parallel after T001 and T002.
- **Foundational**: tests T008–T010 run in parallel. T011, T012, T015, and T022–T024 can also run in parallel.
- **Test tasks**: within each story, all test tasks are [P] and can be written together (for example, T026–T031 for US1).
- **Backend and frontend**: for each story, the backend implementation (for example, T032–T034) and the frontend implementation (T035–T038) touch separate trees.

---

## Parallel Example: User Story 1

```text
# Write all US1 tests together (they must fail):
Task: T026 MonthGridTests.cs            (backend/tests/PersonalCalendar.Domain.Tests)
Task: T027 CreateEventTests.cs, GetMonthViewTests.cs (backend/tests/PersonalCalendar.Application.Tests)
Task: T028 CreateAndMonthViewEndpointTests.cs (backend/tests/PersonalCalendar.Api.Tests)
Task: T029 newEventDefaults.test.ts     (frontend/src/lib)
Task: T030 MonthView.test.tsx           (frontend/src/components)
Task: T031 EventFormDialog.test.tsx     (frontend/src/components)

# Then backend and frontend in parallel:
Track A: T032 MonthGrid → T033 use cases → T034 endpoints
Track B: T035 newEventDefaults → T036 MonthView → T037 ConfirmDialog → T038 EventFormDialog
Join:    T039 App wiring
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1: Setup (T001–T007)
2. Phase 2: Foundational (T008–T025). The domain hazard tests pass.
3. Phase 3: US1 (T026–T039)
4. **Stop and check**: quickstart scenarios 1, 2, 3, and 7. At this point you can create events, see them, and they persist.

### Incremental Delivery

1. Setup and Foundational give the foundation.
2. Adding US1 gives the MVP: create an event and see it in the month view.
3. Adding US2 lets you view details.
4. Adding US3 lets you edit and delete.
5. Adding US4 adds all-day events.
6. Polish: performance, the save-failure test, the accessibility sweep, the README, and the manual quickstart pass. Then open the PR to `main` (Principle V).

---

## Notes

- [P] means the task touches a different file and has no dependency on an unfinished task.
- Commit after each task or logical group, on branch `001-event-basics`.
- Every date/time bug found later MUST come with a regression test that reproduces it (Principle I).
- Do not add dependencies beyond those in plan.md without recording the justification in plan.md's Complexity Tracking section (Principle III).
