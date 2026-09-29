# Research: Event Basics

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-09-29

Each entry lists a Decision, its Rationale, and the Alternatives considered. No NEEDS CLARIFICATION items remain.

## R1. Overall architecture and deployment

- **Decision**: An ASP.NET Core (.NET 10) API and a React single-page app, both running only on the user's machine. The API listens on loopback (`127.0.0.1`) only. In development, the Vite dev server passes `/api` requests through to the API. For everyday use, the API also serves the built React files, so one `dotnet run` starts the whole app.
- **Rationale**: Matches the README (a clean-architecture .NET backend with a React frontend). It also meets clarification Q1 as refined on 2026-09-29: data stays on the device, with no hosted server and no account. Listening on loopback only means nothing on the network can reach the API, so no authentication is needed.
- **Alternatives considered**: A React-only app with IndexedDB storage was rejected because it drops the .NET backend the README calls for. A hosted API was rejected because it contradicts clarification Q1. An Electron or Tauri wrapper was rejected because it adds a runtime and packaging work that no spec requires (Principle III).

## R2. Persistence

- **Decision**: SQLite through EF Core (`Microsoft.EntityFrameworkCore.Sqlite`), in one database file. By default it lives at `%LOCALAPPDATA%\PersonalCalendar\calendar.db`, and the connection string can override the path. The schema is created and upgraded with EF Core migrations, which run at startup.
- **Rationale**: SQLite is a single file with no service to install, and it meets the durability required by FR-013 (a committed transaction survives a restart). EF Core is the standard .NET data-access tool. Migrations keep future schema changes safe.
- **Alternatives considered**: A JSON file was rejected because writes are not atomic and hand-written locking would be needed. LiteDB was rejected as less mainstream. Dapper with hand-written SQL was rejected because it would need hand-written migrations, and EF Core is enough at this scale.

## R3. Date/time library and storage format (Constitution II, Date & Time Standards)

- **Decision**: NodaTime in the Domain and Application layers, stored and exchanged like this:
  - Timed events are stored as UTC `Instant`s (`StartUtc`, `EndUtc`).
  - All-day events are stored as `LocalDate`s (`StartDate`, `EndDate`, with the end date included).
  - Every event also stores the IANA zone it was entered in (`EntryTimeZone`), for audit and to support a per-event zone later.
  - In SQLite, instants are text in the fixed-width format `uuuu-MM-ddTHH:mm:ss.fffZ`, and dates are text in `uuuu-MM-dd`. Both sort correctly as text.
  - Zones come from the tzdb provider built into NodaTime.
- **Rationale**: NodaTime separates `Instant`, `LocalDateTime`, `LocalDate`, and `ZonedDateTime` into distinct types, which prevents the bugs Principle I is meant to stop. It gives an injectable `IClock` (for the Date & Time Standards) and has built-in rules for DST gaps and overlaps (R4). Storing all-day events as dates is the "local value" exception allowed by Principle II. It guarantees FR-016: an all-day event never moves to a neighboring day.
- **Alternatives considered**: BCL `DateTimeOffset` plus `TimeZoneInfo` was rejected. It needs hand-written gap/overlap handling, zone names depend on the platform, and there is no clock abstraction. Storing all-day events as UTC midnight was rejected because they shift when the zone changes, which violates FR-016.

## R4. DST gap and overlap handling (FR-017, Edge Cases)

- **Decision**: Local times are turned into instants with NodaTime's `Resolvers.LenientResolver`:
  - A time in a spring-forward gap is moved forward by the length of the gap (02:30 becomes 03:30).
  - A time that occurs twice at fall-back resolves to the earlier occurrence.
  - If a submitted time falls in a gap and the request does not set `acceptAdjustedTimes: true`, the API saves nothing. It returns `422` with problem type `dst-adjustment-required` and the adjusted local times. The UI shows the adjusted time, and when the user confirms, it resends the request with the flag set.
  - The overlap case needs no confirmation, because the time the user sees is unchanged.
  - The end-after-start rule is checked on the *adjusted* instants. If they are invalid, the API returns `400 validation` rather than asking the user to confirm an invalid time.
- **Rationale**: This is exactly the behavior the spec describes, and it keeps all resolution in tested domain code. The confirmation round-trip meets "shown the adjusted time before the event is saved."
- **Note**: With the "earlier occurrence" rule, the UI cannot enter the *second* 01:30. The spec's edge case of an end time in the second occurrence before a start in the first is therefore covered by domain tests on `Instant` comparison, not by UI tests.
- **Alternatives considered**: Rejecting times in the gap outright was rejected because it contradicts the spec. Adjusting silently was rejected because it contradicts FR-017.

## R5. Where date/time logic lives

- **Decision**: All calendar date logic is in the C# Domain and covered by test-first xUnit tests with a fixed `FakeClock` and explicit zones. This includes validation, local-to-instant resolution, the month grid range, placing events on days, ordering within a day, and deciding what "today" is. The API returns a ready-to-render month view (weeks → days → event summaries).

  The frontend's date/time code is limited to three things:
  1. Formatting received values with `Intl.DateTimeFormat` in the given zone.
  2. Computing the default start and end for a new event (FR-005). This is a small pure function that is passed "now" and tested with `TZ` fixed in the Vitest config.
  3. Plain calendar-date arithmetic for keyboard navigation in the grid (`lib/dates.ts`: add days, add months, start and end of week). It works on `yyyy-MM-dd` strings in UTC, never involves a zone, and has its own tests. *(Added during implementation.)*
- **Rationale**: It gives one tested source of truth for Principle I, and the React layer stays a thin adapter (Principle VI). It also avoids adding a Temporal polyfill to the frontend.
- **Alternatives considered**: Building the grid on the client with Temporal or date-fns-tz was rejected because the date logic would then live in two languages, each needing its own hazard tests.

## R6. Placing events on days and ordering them (FR-007)

- **Decision**:
  - A timed event appears on every local date from the date of its start to the date of `End − 1 tick`, all in the requested zone. An event ending exactly at midnight therefore does not appear on the next day.
  - An all-day event appears on every date from `StartDate` to `EndDate`, including both.
  - Within each day, all-day events come first, ordered by start date and then title. Timed events follow, ordered by start instant and then title.
  - The grid starts on the Sunday on or before the 1st and ends on the Saturday on or after the last day, so it has 4 to 6 weeks. Days outside the month are flagged `inMonth: false` but still show their events, as the spec's "Events spanning months" edge case requires.
- **Rationale**: This is the usual calendar behavior, and every rule can be tested directly.
- **Alternatives considered**: Always showing 6 weeks was rejected because the spec only needs the weeks that cover the month.

## R7. The user's time zone and locale at runtime

- **Decision**: The browser sends `Intl.DateTimeFormat().resolvedOptions().timeZone` on every request that depends on a zone (a `timeZone` query parameter, or a body field on writes). The API validates it against tzdb and returns `400` for an unknown zone. Nothing about the zone is stored as a setting, which matches clarification Q2. Formatting uses the browser's default locale, which meets FR-021 and clarification Q3.
- **Rationale**: The zone is read fresh in every session, so events follow the device. The server stays stateless about the user's location.
- **Alternatives considered**: Using the server machine's zone was rejected because it is the same machine but not the same source of truth, and it makes tests depend on the machine. Storing a user setting was rejected as out of scope.

## R8. Two browser tabs editing the same event (open item from clarify)

- **Decision**: Optimistic concurrency. Each event has an integer `Version`, which EF Core uses as a concurrency token. `PUT` and `DELETE` require the version the client last saw and return `409` when it is stale. The UI shows "This event was changed elsewhere. Reload to see the latest version."
- **Rationale**: It costs one column and one check, and it prevents silently losing a user's work. That fits the spirit of FR-014.
- **Alternatives considered**: Last write wins was rejected because a change can be lost silently. Locking was rejected as unnecessary.

## R9. Frontend stack

- **Decision**:
  - React 19 with TypeScript, built with Vite.
  - Server calls use plain `fetch` wrapped in a small typed API module. There is no router, since the app is one screen whose dialogs are driven by state.
  - Dialogs use the native `<dialog>` element with `showModal()`, which provides focus containment and Escape to close. Focus goes back to the control that opened the dialog.
  - Date and time fields use the native `<input type="datetime-local">` and `<input type="date">`, which display in the browser's locale and are accessible out of the box.
  - Tests use Vitest, React Testing Library, and `vitest-axe`, with `TZ` and the locale fixed in the test setup.
- **Rationale**: This is the fewest dependencies that still meet Principle IV (Principle III).
- **Alternatives considered**: TanStack Query, a component library, and a date-picker package were all rejected. None is needed for five API calls on one screen, and custom date pickers are a common source of accessibility problems.

## R10. Accessibility of the month grid (FR-018 to FR-020)

- **Decision**:
  - The month grid follows the WAI-ARIA APG "grid" pattern with a roving `tabindex`. Arrow keys move by day or week, Home and End go to the start or end of the week, and Page Up and Page Down change the month.
  - Enter on a day starts a new event. Each event inside a day cell is a button.
  - Every day and event has an accessible name with the full date and time, built with `Intl.DateTimeFormat` using `dateStyle: "full"` and `timeStyle: "short"`.
  - A single polite live region announces saves, deletes, and validation errors.
  - The "N more" control opens a list of that day's events.
- **Rationale**: This is the standard, well-understood pattern, and it meets Principle IV directly.
- **Alternatives considered**: A plain table of links was rejected because it cannot meet the arrow-key navigation Principle IV requires.

## R11. Backend testing

- **Decision**:
  - The Domain and Application layers get xUnit unit tests using NodaTime's `FakeClock` and explicit zones. These cover `America/Chicago` DST dates, `Asia/Kolkata`, `Australia/Adelaide`, Feb 29 2028, midnight crossings, and month and year boundaries.
  - The API gets integration tests using `WebApplicationFactory`, each running against a fresh temporary SQLite file. These cover the contracts, persistence across restarts (by disposing the factory and creating a new one), and returning `409` or `422`.
- **Rationale**: Principle I requires tests written first, with the clock and zone fixed. Real SQLite in the integration tests catches problems that an in-memory provider would hide.
- **Alternatives considered**: EF Core's InMemory provider was rejected because it behaves differently from SQLite (constraints, text ordering).
