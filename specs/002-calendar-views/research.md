# Research: Calendar Views and Navigation

**Feature**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md) | **Date**: 2026-10-02

Each entry lists a Decision, its Rationale, and the Alternatives considered. This feature builds on the architecture of feature 001 ([001 research](../001-event-basics/research.md), R1–R11), which still applies unless an entry below says otherwise. No NEEDS CLARIFICATION items remain.

**Design priority** (spec Clarifications): tapping and clicking come first. Keyboard use must reach the Constitution IV baseline and no further.

## V1. Address format and history handling (FR-017 to FR-022)

- **Decision**: The view and selected date go in the URL path, for example `/day/2026-10-14`, `/week/2026-10-14`, and `/month/2026-10-14`. The month URL keeps the full selected date, so FR-002 holds when you switch views.
  - `/{view}` with no date shows that view for today. `/` shows the month view for today.
  - Any other path shows the month view for today, displays the message "That link couldn't be opened, so you're seeing this month.", and corrects the address with `history.replaceState`.
  - A change of view or period calls `history.pushState`. Moving the selected date within the period already shown calls `replaceState` (FR-021). A `popstate` event (Back or Forward) re-renders from the address.
  - There is no router library. A small `useViewState` hook (about 60 lines) does the parsing, formatting, and history calls, and `lib/viewState.ts` holds the pure parse and format functions with their tests.
- **Rationale**:
  - The API already serves `index.html` for every path outside `/api` (`MapFallbackToFile`, from feature 001), and Vite's dev server does the same. Path URLs therefore need no server change, and they read well as bookmarks.
  - The address carries only a view and a plain date, never an instant, so FR-022 holds.
  - Three views and one screen do not justify a router dependency (Principle III).
- **Alternatives considered**:
  - Query parameters (`/?view=week&date=…`) would work equally well but are harder to read as bookmarks.
  - Hash routing (`/#/week/…`) is a dated pattern and is not needed, because the server fallback already exists.
  - React Router was rejected as a dependency for three routes.

## V2. Where the view-period date math lives (Constitution I, V)

- **Decision**: The frontend owns the *view state*: the view type plus the selected date as a `yyyy-MM-dd` string. Period arithmetic stays in `lib/dates.ts`, which already does zone-free calendar-date math in UTC (001 R5, item 3). The new pure functions are `periodOf(view, date)`, `stepPeriod(view, date, ±1)`, `isInSupportedRange(date)` (1900-01-01 to 2199-12-31, FR-012), and `parseDateInput`. They are written test-first with the hazard cases: Feb 29 in 2028, the nonexistent Feb 29 in 2027, Jan 31 + 1 month, the Dec–Jan week, and the edges of the supported range.

  **Today** in the frontend comes from a `todayIn(timeZone, now)` function that reads the date parts with `Intl.DateTimeFormat(…, { timeZone })`. It is tested with injected instants in `America/Chicago`, `Asia/Kolkata`, and `Australia/Adelaide` near midnight.
- **Rationale**: Navigation has to be instant and must work before any data loads. These functions do plain calendar arithmetic with no zone math. Everything that involves zones or instants, such as where events fall on the time scale and DST day lengths, stays in the C# domain (V3).
- **Alternatives considered**: Asking the API for "the previous or next period" was rejected because it adds a round trip to every tap and moves a trivial pure function behind HTTP.

## V3. Day and week data: one API endpoint with a domain-built timeline

- **Decision**: A new endpoint, `GET /api/calendar/days?timeZone=…&start=yyyy-MM-dd&count=1|7`, returns `DaysView` ([contracts/http-api.md](./contracts/http-api.md)). Both the day view and the week view use it, including the stacked phone layout of the week view.

  The pure domain function `DayTimeline.Build(date, zone, events)` works out, for each day:
  - The day's actual start and end instants (`zone.AtStartOfDay(date)` and the next day's start), and from them its length in minutes: 1380, 1440, or 1500. In `Australia/Lord_Howe` the half-hour DST shift gives 1410 or 1470.
  - **Hour marks**: the minute offset and local wall time of every whole-hour boundary that actually occurs on that day. On a spring-forward day there is no mark for 02:00, and on a fall-back day 01:00 appears twice.
  - **Timed segments**: each event is clipped to the day, `[max(start, dayStart), min(end, dayEnd))`, and given its offset and duration in minutes. Flags `continuesBefore` and `continuesAfter` mark segments that run past midnight.
  - **Overlap columns** (V4).

  All-day events are listed per day, and for the week `AllDayLanes.Build` packs them into bars (V5).
- **Rationale**: The time axis is *elapsed time from the day's real start*, not wall-clock hours. That makes the 23- and 25-hour DST days correct by construction, which the spec's DST edge cases and Constitution I require, and it keeps all zone logic in NodaTime with the test-first hazard suite. The frontend only does `top = offsetMinutes × pixelsPerMinute`.
- **Alternatives considered**:
  - Separate `/day` and `/week` endpoints were rejected because they would duplicate the same model, and `count` already covers both.
  - Computing the layout on the client was rejected because zone and DST math would then live in two languages (001 R5).
  - Reusing the month endpoint was rejected because it has no time-of-day positions.

## V4. Placing overlapping timed events side by side (FR-004)

- **Decision**: The standard column-packing approach, implemented in the domain:
  1. Sort the day's segments by start, then by longer duration, then by title, then by id.
  2. Split them into *clusters* of segments that overlap one another, directly or through a chain.
  3. Within a cluster, give each segment the first column whose last segment has already ended.
  4. Set `columnCount` to the cluster's number of columns.

  Segments that only touch (one ends at 10:00 and the next starts at 10:00) do not overlap. The UI places each segment at `left = column / columnCount` with `width = 1 / columnCount`.
- **Rationale**: It is deterministic, easy to test, and it is the layout people know from mainstream calendars. Every event stays visible and tappable.
- **Alternatives considered**: Letting events expand into free columns to their right looks slightly better but adds complexity that no requirement asks for (Principle III).

## V5. Multi-day all-day bars in the wide week view (FR-005)

- **Decision**: `AllDayLanes.Build(weekStart, days, allDayEvents)` clips each all-day event to the week. It returns `startIndex` (0–6), `span`, `lane` (assigned greedily in the same order as month-view sorting, 001 R6), and the `continuesBefore` and `continuesAfter` flags. The UI draws each bar across CSS grid columns and marks a clipped edge with a squared-off end and the screen-reader text "continues from the previous week" or "continues into the next week".
- **Rationale**: It meets "one continuous item across those days" with plain-date math only, so it is DST-proof by design (FR-016).
- **Alternatives considered**: Repeating the event in each day's cell was rejected because the spec asks for one continuous item.

## V6. Telling narrow (phone) screens from wide ones (FR-003a, FR-004a)

- **Decision**: A `useNarrowScreen()` hook uses `matchMedia('(max-width: 599px)')`. It updates on `change`, so turning or resizing the screen switches layouts without changing the view state. The narrow layouts are a separate `WeekList` component and a marker mode of `DayCell`. CSS alone is not enough, because the narrow week view has a different structure and different accessible names. Tests stub `matchMedia`.
- **Rationale**: The spec sets the threshold at "about 600 pixels". One media query keeps the behavior predictable, and the existing 640 px CSS breakpoint is aligned to the same value.
- **Alternatives considered**: Rendering both layouts and hiding one with CSS was rejected because it doubles the accessibility tree and the test surface. Container queries were rejected because the whole screen width is what matters here.

## V7. Swipe navigation (FR-008a)

- **Decision**: A small `useSwipe(onPrevious, onNext)` hook built on Pointer Events, used only for `pointerType === 'touch'`.
  - A swipe counts when the horizontal movement is at least 48 px, at least twice the vertical movement, and done within 700 ms.
  - The view body sets `touch-action: pan-y`, so the browser handles vertical scrolling natively and never fights the gesture.
  - Swipes that start on a text field or inside an open dialog are ignored.
  - The visible Previous and Next buttons remain the single-pointer alternative (WCAG 2.5.1).
- **Rationale**: About 40 lines with no dependency. The 2:1 angle rule and the native `pan-y` handling meet the spec's requirement that scrolling with a slight sideways drift does not change the period.
- **Alternatives considered**: Gesture libraries such as `@use-gesture/react` and Hammer.js were rejected because they are a dependency for one gesture (Principle III). Touch Events were rejected because Pointer Events cover touch, pen, and mouse with one API.

## V8. Jump to a date on touch (FR-011; outstanding item from clarify)

- **Decision**: A "Go to date" button opens a small native `<dialog>` with an `<input type="date" min="1900-01-01" max="2199-12-31">` and **Go** and **Cancel** buttons.
  - On phones, the browser shows its own large, touch-friendly date picker.
  - On desktop, a typed value that does not exist, such as Feb 30, leaves the input empty or invalid (`validity.badInput`). The dialog then shows "Enter a valid date." and the view stays where it is.
  - A value outside the range shows "Choose a date between 1900 and 2199."
- **Rationale**: The native picker is the most usable on touch, accessible by default, and consistent with 001 R9, which ruled out a date-picker package.
- **Alternatives considered**: An inline mini-month picker was rejected because it needs custom grid code and adds accessibility risk. A plain text field was rejected because typing a date is poor on touch.

## V9. Tapping empty time slots and the "New event" button (FR-013a, FR-013b)

- **Decision**: In each day column of the time scale, the empty background is **one large tap target**. The 30-minute slot is taken from where the tap lands: `slotIndex = floor(offsetY / pixelsPerSlot)` and `slotStart = dayStart + slotIndex × 30 min`, an instant. That instant becomes the form's local start through the existing `toLocalInputValue(instant, zone)` helper, and the event lasts one hour (`newEventAt(localStart)`, a pure function tested across the 23:30 → next-day rollover).
  - Slots are counted from the day's real start instant, so a spring-forward day has 46 slots and a fall-back day has 50. On the fall-back day, a slot in the second 01:00 hour gives a local time that resolves to the *earlier* occurrence when saved (001 R4). This is accepted because the form shows the time before saving.
  - During the press, a light highlight shows which half-hour slot will be used.
  - The background is not a keyboard stop and has no per-slot accessible names. Keyboard and screen-reader users create events with the always-visible **New event** button (FR-013a), which uses `newEventDefaults(selectedDate, now)` from 001 and has the same result apart from the preset time. This keeps the Tab order short. The background is marked `aria-hidden`, so it does not clutter the accessibility tree.
- **Rationale**: Whole-column targets are as easy to hit as possible on touch (V10), and no slot grid is needed in the DOM.
- **Alternatives considered**: One button per 30-minute slot was rejected. At a readable 48 px per hour, a slot is only 24 px tall, under the 44 px minimum, and 48–50 buttons per day would flood the accessibility tree.

## V10. Touch target sizes (FR-013c)

- **Decision**:
  - Every header control (Previous, Today, Next, the view switcher, Go to date, New event), every month day cell, and every heading in the phone week list is at least 44 × 44 px.
  - The time scale uses 48 px per hour, and the empty-slot target is the whole column (V9).
  - Event blocks have a minimum height of 24 px, so a 5-minute event is still visible. Where a block is shorter than 44 px or narrower than 44 px, the spec's exception applies.
  - When a cluster of overlapping events (V4) has 3 or more columns, a "+N" chip of at least 44 × 44 px appears at the top of the cluster. It opens the existing `DayOverflowDialog`, which lists every event in that cluster with full-size rows. This covers the Edge Case "Tapping a crowded time slot lists every event in it."
- **Rationale**: Uses the WCAG 2.5.8 / spec exception only where events are packed in, and always offers a full-size path to every event.

## V11. Keeping "today" and the zone current (FR-015, FR-016)

- **Decision**: A `useToday(timeZone)` hook re-checks the time every 30 seconds and when the page becomes visible again (`visibilitychange`).
  - If the device zone (`currentTimeZone()`) has changed, `App` stores the new zone, which triggers a reload (001 clarification Q2).
  - If today's date in the zone has changed, the today highlight moves and the current period is reloaded, so the server's `isToday` flags agree. The view state is left alone.

  The current-time line (FR-016) is positioned from the elapsed time since the day's `dayStart` instant (`(now − dayStart) / 60 000`). That is instant arithmetic, not offset math, so it is correct on DST days.
- **Rationale**: A 30-second check is enough for the 1-minute limit in FR-015 and costs almost nothing. The clock is injected (`now: () => Date`) so tests can drive it.

## V12. Keyboard baseline in the new views (FR-023, FR-024, FR-026)

- **Decision**: The week view and the stacked phone week reuse the month view's roving-`tabindex` day navigation from 001 R10. ← and → move by one day, and leaving the period loads the neighboring one. In the month view, Enter or Space now *opens the day view* (FR-013), which replaces 001's "create event" action.

  The day view has no day grid. Tab moves through all-day events, then timed events in time order. ← and → anywhere in the day view go to the previous or next day (FR-024; added after /speckit-analyze finding C1). In the phone week list, ← and → (and ↑/↓) on a day heading move to the neighboring heading, loading the next week past the edge. After any change of period, a polite announcement reads the period title. Focus moves to the selected date in the month and week views, and to the period heading in the day view (`tabIndex=-1`).

  No extra shortcuts are added (spec Clarifications). Home, End, Page Up, Page Down, ↑, and ↓ stay in the month grid as they are from 001 and are not added to the new views.
- **Rationale**: This meets Constitution IV (keyboard operable, arrow keys in calendar grids) and the touch-first priority the user set.
