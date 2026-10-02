# Data Model: Calendar Views and Navigation

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

This feature adds **no stored data**. The `CalendarEvent` aggregate, the SQLite schema, and the validation codes from [001 data-model](../001-event-basics/data-model.md) do not change. Everything below is either a pure domain result, a read model, or frontend state.

## Domain (C#, NodaTime, pure, written test-first)

### `DayTimeline` (`Domain/Calendar/DayTimeline.cs`)

`DayTimeline.Build(LocalDate date, DateTimeZone zone, IEnumerable<CalendarEvent> events) → DayTimelineResult`

| Field | Type | Rule |
|---|---|---|
| `Date` | `LocalDate` | The input date. |
| `DayStart` | `Instant` | `zone.AtStartOfDay(date)`. This is usually 00:00 local, but in a zone whose DST change happens at midnight, it is the first instant that exists on that date. |
| `DayEnd` | `Instant` | `zone.AtStartOfDay(date + 1)`, exclusive. |
| `LengthMinutes` | `int` | `(DayEnd − DayStart)` in minutes: 1440, or 1380/1500 on DST days (1410/1470 in `Australia/Lord_Howe`). |
| `HourMarks` | `IReadOnlyList<HourMark>` | One mark for each whole-hour local wall time that occurs in `[DayStart, DayEnd)`, in instant order. `HourMark(int OffsetMinutes, LocalTime Label)`. A local hour that does not exist (spring-forward) has no mark. A repeated hour (fall-back) has two marks with the same `Label` and different offsets. |
| `AllDay` | `IReadOnlyList<CalendarEvent>` | All-day events whose `[StartDate, EndDate]` contains `Date`, in the 001 R6 order. |
| `Timed` | `IReadOnlyList<TimedSegment>` | See below. |

`TimedSegment`:

| Field | Type | Rule |
|---|---|---|
| `Event` | `CalendarEvent` | A timed event that overlaps `[DayStart, DayEnd)`. An event that ends exactly at `DayStart` is **not** included, which matches 001 R6. |
| `OffsetMinutes` | `int` | `(max(Start, DayStart) − DayStart)`, rounded down to whole minutes. |
| `DurationMinutes` | `int` | `(min(End, DayEnd) − max(Start, DayStart))`, rounded up and at least 1. |
| `ContinuesBefore` / `ContinuesAfter` | `bool` | `Start < DayStart` / `End > DayEnd`. |
| `Column` / `ColumnCount` | `int` | Overlap packing from research V4: `0 ≤ Column < ColumnCount`. `ColumnCount` is the same for every segment in a cluster. Segments that only touch end-to-start do not overlap. |

**Ordering**: segments are listed by `OffsetMinutes`, then longer `DurationMinutes`, then `Title` (ignoring case), then `Id`. The result is the same for the same input every time.

### `AllDayLanes` (`Domain/Calendar/AllDayLanes.cs`)

`AllDayLanes.Build(LocalDate first, int dayCount, IEnumerable<CalendarEvent> allDayEvents) → IReadOnlyList<AllDayBar>`

`AllDayBar(CalendarEvent Event, int StartIndex, int Span, int Lane, bool ContinuesBefore, bool ContinuesAfter)`. These rules hold for every bar:
- `0 ≤ StartIndex`, `StartIndex + Span ≤ dayCount`, and `Span ≥ 1`.
- Two bars in the same lane never cover the same day.
- Lanes are assigned greedily in the 001 R6 order (start date, then title, then id), so the lowest free lane is used.
- Only plain `LocalDate` arithmetic is used, so no zone can move a bar (FR-016).

### Existing `MonthGrid`

It does not change. "Today" is still provided by the caller from `IClock`.

## Application

### `GetDaysView` use case (`Application/Events/GetDaysView.cs`)

Input: `start` (`yyyy-MM-dd`), `count` (1 or 7), and `timeZone`.

1. **Validate the input**:

   | Code | Field | When |
   |---|---|---|
   | `timeZone.unknown` | `timeZone` | Not a tzdb id. |
   | `start.invalid` | `start` | Missing, unparseable, or a date that doesn't exist. |
   | `start.outOfRange` | `start` | Any day in the range has a year outside 1–9998, the same technical limit as the month endpoint. The 1900–2199 navigation range (FR-012) is enforced in the UI, because a week near the ends of that range may show days just outside it. |
   | `count.invalid` | `count` | Not 1 or 7. |

2. **Load the events**: query the repository with `ListOverlappingAsync(dayStart(first), dayEnd(last), first, last)`. This method already exists from 001.
3. **Build each day**: run `DayTimeline.Build` for each day. When `count = 7`, also run `AllDayLanes.Build`.
4. **Find today and now**: `today` and `now` come from `IClock`, converted into the zone.

### `DaysViewModel` (read model, never stored)

```
DaysViewModel(string TimeZone, LocalDate Today, OffsetDateTime Now,
              IReadOnlyList<TimelineDayModel> Days, IReadOnlyList<AllDayBarModel> AllDayBars)
TimelineDayModel(LocalDate Date, bool IsToday, OffsetDateTime DayStart, OffsetDateTime DayEnd, int LengthMinutes,
          IReadOnlyList<HourMarkModel> HourMarks, IReadOnlyList<EventSummary> AllDay,
          IReadOnlyList<TimedSegmentModel> Timed)
HourMarkModel(int OffsetMinutes, string Label)   // Label formatted as "HH:mm"
TimedSegmentModel(EventSummary Event, int OffsetMinutes, int DurationMinutes,
                  bool ContinuesBefore, bool ContinuesAfter, int Column, int ColumnCount)
AllDayBarModel(EventSummary Event, int StartIndex, int Span, int Lane, bool ContinuesBefore, bool ContinuesAfter)
```

`EventSummary` is the read model from 001, unchanged. `AllDayBars` is empty when `count = 1`.

## Frontend state (TypeScript, never stored)

### `ViewState` (`lib/viewState.ts`)

| Field | Type | Rule |
|---|---|---|
| `view` | `'day' \| 'week' \| 'month'` | |
| `date` | `DateString` (`yyyy-MM-dd`) | The selected date. Always within 1900-01-01 to 2199-12-31. |

Derived values (pure functions in `lib/dates.ts` and `lib/viewState.ts`):
- `periodOf(state) → { first, last }`. For `day`, both are `date`. For `week`, they are `startOfWeek(date)` to `+6` days. For `month`, they are the 1st to the last day of the month. The month view still loads its grid range from the API.
- `stepPeriod(state, ±1)`: adds or subtracts 1 day, 7 days, or 1 month with clamping (FR-009). The result is `null` when it would leave the supported range, and the Previous or Next button is then disabled (FR-012).
- `toPath(state)`: `/{view}/{date}`.
- `parsePath(pathname, today) → { state, error?: 'invalid-link' }`. The rules:

| Path | Result |
|---|---|
| `/` | `{ month, today }` |
| `/{view}` | `{ view, today }` |
| `/{view}/{yyyy-MM-dd}` with a real date in range | `{ view, date }` |
| Anything else: unknown view, a malformed or nonexistent date, out of range, or extra segments | `{ month, today }` with `error: 'invalid-link'` |

  A trailing slash is accepted. View names are matched in lowercase only, so the canonical address is the one shown.

**State transitions** (in `useViewState`):

| Trigger | New state | History |
|---|---|---|
| View switcher | `{ newView, date }` | push |
| Previous or Next (button or swipe) | `stepPeriod` | push |
| Today | `{ view, today }` | push, unless the state is unchanged |
| Go to date | `{ view, chosenDate }` | push |
| Tap a month day, or a phone-week heading | `{ 'day', thatDate }` | push |
| Arrow-key move inside the current period | `{ view, newDate }` | replace |
| Arrow-key move past the edge of the period | `{ view, newDate }` | push |
| `popstate` | `parsePath(location.pathname)` | (none) |
| Invalid address on load | `{ month, today }` | replace, and show a message |

### `Today` (`hooks/useToday.ts`)

`{ timeZone: string; today: DateString; now: Date }`. It is re-checked every 30 s and when the page becomes visible again (research V11), and the clock is injectable.

### Layout mode

`isNarrow: boolean` comes from `useNarrowScreen()`, which is true when the viewport is 599 px wide or less (research V6). It never changes `ViewState`.
