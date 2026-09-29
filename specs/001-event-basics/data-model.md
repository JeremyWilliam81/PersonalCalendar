# Data Model: Event Basics

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

## Domain (C#, NodaTime types)

### `CalendarEvent` (aggregate root)

| Field | Type | Rules |
|---|---|---|
| `Id` | `EventId` (wraps `Guid`) | Assigned when the event is created. Never changes. |
| `Title` | `string` | Leading and trailing spaces are removed. Must have 1–200 characters (FR-004). |
| `Location` | `string?` | Spaces are trimmed. An empty value becomes `null`. At most 200 characters. |
| `Notes` | `string?` | Spaces are trimmed. An empty value becomes `null`. At most 5,000 characters. |
| `Schedule` | `EventSchedule` | See below. Exactly one of the two kinds. |
| `EntryTimeZone` | `DateTimeZone` (stored as its IANA id) | The zone the event was last entered or edited in (Principle II). It is not used for display (clarification Q2). |
| `CreatedUtc` | `Instant` | Taken from `IClock` when the event is created. |
| `UpdatedUtc` | `Instant` | Taken from `IClock` on every change. |
| `Version` | `int` | Starts at 1 and goes up by 1 on each update. Used as the concurrency token (R8). |

### `EventSchedule` (sealed hierarchy, value objects)

- **`TimedSchedule(Instant Start, Instant End)`**: invariant `End > Start`, meaning a strictly later moment in time (FR-003).
- **`AllDaySchedule(LocalDate StartDate, LocalDate EndDate)`**: invariant `EndDate >= StartDate`. The end date is included (FR-002, FR-003).

The domain does not allow any other combination. Switching between timed and all-day replaces the whole schedule (User Story 4, scenario 5).

### Constructing a schedule from user input (pure domain service `ScheduleResolver`)

Input: local values plus a zone, and `acceptAdjustedTimes`.

1. **All-day**: parse the start and end `LocalDate`s and check that the end is not before the start. Zone rules are never involved.
2. **Timed**: map each `LocalDateTime` to a `ZonedDateTime` in the zone with `Resolvers.LenientResolver` (R4).
   - If either value fell in a gap and `acceptAdjustedTimes` is false, the result is `AdjustmentRequired(adjustedStartLocal, adjustedEndLocal)` and nothing is saved.
   - Otherwise, the result is a `TimedSchedule` built from the resolved instants, which must then satisfy `End > Start`.

### Validation result

Validation gathers **every** field error rather than stopping at the first one, so the UI can show each error next to its field (FR-003):

| Code | Field | Message (English UI text) |
|---|---|---|
| `title.required` | `title` | "Title is required." |
| `title.tooLong` | `title` | "Title must be 200 characters or fewer." |
| `location.tooLong` | `location` | "Location must be 200 characters or fewer." |
| `notes.tooLong` | `notes` | "Notes must be 5,000 characters or fewer." |
| `end.notAfterStart` | `end` | "End must be after start." |
| `endDate.beforeStart` | `endDate` | "End date must be on or after the start date." |
| `timeZone.unknown` | `timeZone` | "Unknown time zone." |
| `start.required` / `end.required` | `start` / `end` | "Start is required." / "End is required." |
| `startDate.required` / `endDate.required` | `startDate` / `endDate` | "Start date is required." / "End date is required." |
| `<field>.invalid` | `start`, `end`, `startDate`, `endDate` | e.g. "Enter a valid start date and time." (the API couldn't parse the value) |
| `version.required` | `version` | Returned by `PUT`/`DELETE` without a version. |
| `year.outOfRange` / `month.outOfRange` | `year` / `month` | Returned by the month view outside 1–9998 / 1–12. |

*Implementation note (2026-09-29):* the `required`, `invalid`, `version` and range codes were added during implementation to cover inputs the original table didn't list.

### Read models (Application layer, never stored)

- **`MonthView`**: contains `Year`, `Month`, `TimeZone`, `Today` (a `LocalDate` from `IClock` in the zone), and `Weeks[]`. Each week has 7 `DayCell`s. Built by the pure function `MonthGrid.Build(year, month, zone, today, events)` (R6).
- **`DayCell`**: contains `Date`, `InMonth`, `IsToday`, and `Events[]`, a list of `EventSummary` ordered all-day first, then timed by start and title.
- **`EventSummary`**: contains `Id`, `Title`, `IsAllDay`, and either the start and end as zoned values (timed) or the start and end dates (all-day).

## Application ports

- `IEventRepository`:
  - `GetAsync(EventId)` returns the event.
  - `ListOverlappingAsync(Instant from, Instant to, LocalDate fromDate, LocalDate toDate)` returns events overlapping either range.
  - `AddAsync(event)` saves a new event.
  - `UpdateAsync(event, expectedVersion)` saves changes and throws `ConcurrencyConflict` if the version is stale.
  - `DeleteAsync(EventId, expectedVersion)` deletes the event and throws `ConcurrencyConflict` if the version is stale.
- `IClock`: NodaTime's clock, injected. `FakeClock` is used in tests.
- `IDateTimeZoneProvider`: NodaTime's `DateTimeZoneProviders.Tzdb`.

Use cases: `CreateEvent`, `UpdateEvent`, `DeleteEvent`, `GetEventDetails`, `GetMonthView`.

## Persistence (SQLite, table `Events`)

| Column | SQLite type | Null | Notes |
|---|---|---|---|
| `Id` | TEXT | no | PK, GUID |
| `Title` | TEXT | no | |
| `Location` | TEXT | yes | |
| `Notes` | TEXT | yes | |
| `IsAllDay` | INTEGER | no | 0/1 |
| `StartUtc` | TEXT | yes | `uuuu-MM-ddTHH:mm:ss.fffZ`. Set only when `IsAllDay = 0` |
| `EndUtc` | TEXT | yes | Same format as `StartUtc` |
| `StartDate` | TEXT | yes | `uuuu-MM-dd`. Set only when `IsAllDay = 1` |
| `EndDate` | TEXT | yes | Same format as `StartDate`. The end date is included |
| `EntryTimeZone` | TEXT | no | IANA id, e.g. `America/Chicago` |
| `CreatedUtc` | TEXT | no | Same format as `StartUtc` |
| `UpdatedUtc` | TEXT | no | Same format as `StartUtc` |
| `Version` | INTEGER | no | Concurrency token |

- **CHECK constraint** (a second layer of protection behind the domain rules): `(IsAllDay = 0 AND StartUtc IS NOT NULL AND EndUtc IS NOT NULL AND StartDate IS NULL AND EndDate IS NULL AND EndUtc > StartUtc) OR (IsAllDay = 1 AND StartDate IS NOT NULL AND EndDate IS NOT NULL AND StartUtc IS NULL AND EndUtc IS NULL AND EndDate >= StartDate)`. The fixed-width text formats compare correctly as text.
- **Indexes**: `IX_Events_StartUtc_EndUtc` and `IX_Events_StartDate_EndDate`.
- **Overlap query for a month grid**: `(StartUtc < @toUtc AND EndUtc > @fromUtc) OR (StartDate <= @toDate AND EndDate >= @fromDate)`. `@fromUtc` and `@toUtc` are the grid's first local midnight and the midnight just after the grid ends, resolved in the requested zone.
- **Mapping**: NodaTime values are converted to and from text with EF Core value converters in Infrastructure. The Domain has no reference to EF. Infrastructure maps a flat `EventRow` to and from `CalendarEvent` (via `CalendarEvent.Rehydrate`), so the domain type needs no EF constructor or setters.

## Lifecycle

```text
(none) --Create(valid)--> Saved(v1) --Update(valid, v=n)--> Saved(v=n+1)
                                     \-Delete(v=n)--> (gone, permanent)
Invalid input or stale version -> no state change
```
