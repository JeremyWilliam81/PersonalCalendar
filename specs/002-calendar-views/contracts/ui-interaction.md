# UI Interaction Contract: Calendar Views and Navigation

This contract describes what users and assistive technology can rely on. It extends and partly replaces [001 ui-interaction](../../001-event-basics/contracts/ui-interaction.md). The event dialogs, the event form, the delete and discard confirmations, and the announcements from 001 are unchanged.

**Priority**: touch and pointer first. Each section starts with tap/click behavior. Keyboard behavior is listed after it and is kept to the Constitution IV baseline.

**Sizes**: "narrow" means a viewport 599 px wide or less, and "wide" means 600 px or more (research V6). Every tappable control is at least 44 × 44 px unless this contract says otherwise (FR-013c, research V10).

## Shared header (all views)

The header contains, in visual and Tab order:

1. **Period title**: an `<h2>`. It is "Wednesday, October 14, 2026" for a day, "October 11 – 17, 2026" for a week (or "December 27, 2026 – January 2, 2027" across years), or "October 2026" for a month. It is formatted with `Intl` in the device locale. Weeks use `formatRange`.
2. **Previous** and **Next**: icon buttons with accessible names that say what they do ("Previous day/week/month", "Next day/week/month"). Each is disabled at the end of the supported range (FR-012).
3. **Today**: a text button.
4. **Go to date**: a button that opens the date dialog described below.
5. **View switcher**: a group of three toggle buttons, **Day**, **Week**, and **Month**, with `aria-pressed` on the active one. On narrow screens they shrink to "D", "W", and "M" visually, but their accessible names stay the full words.
6. **New event**: the primary button. It is always visible and starts an event on the selected date with the 001 FR-005 defaults (FR-013a).

On narrow screens, the header wraps into two rows: the title and view switcher on the first row, and the navigation buttons and New event on the second.

## Swipe (touch only)

On the view body (not the header):
- A swipe to the left goes to Next, and a swipe to the right goes to Previous, exactly like the buttons (FR-008a, research V7).
- A swipe counts when the horizontal movement is at least 48 px, at least twice the vertical movement, and done within 700 ms.
- Vertical scrolling is handled natively by the browser (`touch-action: pan-y`) and never changes the period.
- Mouse dragging does nothing.

## Month view

**Wide**: the same as 001, with these changes:
- Tapping or clicking anywhere on a day cell, including its date number and empty space, **opens that day in the day view** (FR-013). Tapping one of the day's event labels or "+N more" behaves as in 001.
- The day cell's accessible name is "Wednesday, October 14, 2026, today, open in day view". The word "today" appears only for today's date.

**Narrow** (FR-003a):
- Each day shows its date number and up to 3 markers, then "+N". Timed events are filled dots and all-day events are short bars, so the difference doesn't rely on color.
- The markers are `aria-hidden` and are not separate targets. Tapping anywhere on the day opens the day view.
- The accessible name adds the count, for example "Wednesday, October 14, 2026, today, 5 events, open in day view", or "no events".

**Today** (FR-014): the date number is drawn inside a filled circle using the `--today` color, with bold text, so it is marked by shape and not only color. The cell has `aria-current="date"`.

**Keyboard** (the 001 grid baseline): the arrow keys, Home, End, Page Up, and Page Down work as in 001. **Enter or Space opens the focused day in the day view.** This replaces 001's "create event". To create an event, use the **New event** button.

## Week view: wide

- **Day column headings** across the top show the short weekday and the date number. Today's heading has the same filled circle as the month view.
- **All-day row**: bars from `allDayBars` stretch across the columns (FR-005). A clipped edge is drawn square and gets extra screen-reader text ("continues from the previous week" or "continues into the next week"). Tapping a bar opens the event's details.
- **Time scale**: 48 px per hour, with hour labels on the left taken from `hourMarks`.
  - On DST days the column is taller or shorter, and a repeated hour shows its label twice.
  - The view scrolls vertically inside the screen.
  - When the view opens, it scrolls to the current time if today is shown. Otherwise it scrolls to the earliest timed event in the period, or to 8:00 AM if there are none.
- **Event blocks** are buttons placed at `top = offsetMinutes × 0.8 px` with a height of `durationMinutes × 0.8 px` (at least 24 px), and `left` and `width` from `column / columnCount`.
  - The visible text is the title and start time, as far as there is room.
  - The accessible name is the 001 full form, for example "Dentist, Wednesday, October 14, 2026, 9:00 AM to 10:30 AM".
  - `continuesBefore` and `continuesAfter` are shown as a squared-off top or bottom.
- **Crowded clusters** with 3 or more columns show a "+N" chip (at least 44 × 44 px, accessible name "N events between 9:00 AM and 11:00 AM on Wednesday, October 14, 2026") that opens the event list dialog (research V10).
- **Empty space** in a column: a tap or click starts a new event at the 30-minute slot under the pointer, lasting one hour (FR-013b, research V9). During the press, the slot is highlighted.
- **Current time**: a line with a dot is drawn across today's column, positioned from `now − dayStart`, and updated every 30 s (FR-016).

**Keyboard**: the day column headings form a one-row grid with a roving `tabindex`. ← and → move by one day, and moving past Saturday or Sunday loads the next or previous week (FR-024). Enter on a heading opens that day in the day view. Tab then moves through that day's events (all-day first, then timed in time order).

## Week view: narrow (FR-004a)

- Seven stacked sections, Sunday to Saturday. Each section heading is a full-width button, at least 44 px tall, showing "Wednesday, October 14". Its accessible name is the full date, plus "today" where it applies, plus "open in day view".
- Under each heading, the day's events are listed as full-width rows at least 44 px tall: all-day events first (labeled "All day"), then timed events with their time range. Tapping a row opens the event's details.
- A day with no events shows "No events" in muted text.
- Today's section heading uses the filled circle and has `aria-current="date"`.
- When the view opens, it scrolls to today's section if today is in the week, and to the selected date's section otherwise.

**Keyboard**: Tab moves through the headings and their events in reading order. Enter on a heading opens the day view. ← / → (and ↑ / ↓) on a heading move to the neighboring day heading, loading the next or previous week past the edge (FR-024).

## Day view

- It is the same as one wide week column, but full width, with the all-day events stacked at the top. It uses the same layout on narrow and wide screens.
- The period heading has `tabIndex=-1` and receives focus after any change of period, so keyboard and screen-reader users land at the top.
- Tab moves through the all-day events, then the timed events in time order. ← and → go to the previous or next day (FR-024). Empty-space taps work as in the week view.
- When today is shown, the title shows a visible **Today** badge and is read as "…, today".

## Go to date dialog (FR-011, research V8)

- A native `<dialog>` titled "Go to date", containing a labeled `<input type="date">` with `min="1900-01-01"` and `max="2199-12-31"` that starts with the selected date. Buttons: **Go** (primary) and **Cancel**.
- On phones, the browser's own date picker opens when the field is tapped.
- An invalid or incomplete value shows "Enter a valid date." under the field (linked with `aria-describedby`, with `aria-invalid="true"`), and the view does not change.
- A date out of range shows "Choose a date between 1900 and 2199."
- **Go** keeps the current view and shows the period containing the chosen date. Focus then moves as described under Focus after navigation.

## Address and history (FR-017 to FR-022, research V1)

- The address is always `/{view}/{yyyy-MM-dd}`.
- The browser's Back and Forward buttons step through views and periods. Moving within a period with the arrow keys replaces the current history entry instead of adding one.
- An invalid link shows the month view for today, with a message at the top: "That link couldn't be opened, so you're seeing this month." The message has a **Dismiss** button and is also announced once. The address is corrected.

## Focus after navigation (FR-025)

| After | Focus goes to |
|---|---|
| Previous, Next, or Today button | Stays on the button that was used. |
| Swipe | Unchanged. A touch user has no visible focus to keep. |
| View switcher | Stays on the pressed view button. |
| Go to date (Go) | The selected date: the day cell in the month view, the heading in the week view, the period heading in the day view. |
| Tapping or pressing Enter on a month day or a week heading | The day view's period heading. |
| Back or Forward | The period heading. |

## Announcements (one polite live region, from 001)

- After any change of period or view: the period title, plus the view name if the view changed, for example "Week of October 11 – 17, 2026".
- After an invalid link: the message from the Address and history section.
- Saves and deletes from 001 are announced as before. The view stays where it is (FR-006).
