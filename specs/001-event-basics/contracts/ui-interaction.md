# UI Interaction Contract: Event Basics

This contract describes what users and assistive technology can rely on. It is the basis for component tests and the manual keyboard and screen-reader check (Constitution IV).

## Month view

- **Header**: the month and year, shown as a heading (e.g., "October 2026" in the device locale), followed by the buttons **Previous month**, **Today**, and **Next month**, each with an accessible name.
- **Grid**: `role="grid"` labelled by the month heading. The row of weekday names starts on Sunday and has `role="columnheader"`, with the full day name as the accessible name.
- **Day cell** (`role="gridcell"`): its accessible name is the full date, e.g., "Wednesday, October 14, 2026". Days outside the month look muted but work the same way.

**Keyboard in the grid** (roving `tabindex`, so one day cell can be tabbed to):

> Superseded in 002: Enter/Space opens the day view; see specs/002-calendar-views/contracts/ui-interaction.md.

| Key | Action |
|---|---|
| ← / → | Previous or next day |
| ↑ / ↓ | Same weekday in the previous or next week |
| Home / End | First or last day of the week |
| Page Up / Page Down | Previous or next month. The same day number is kept, or the last day of the month if that day doesn't exist |
| Enter / Space | Create a new event on the focused day, with the start date preset (FR-005) |
| Tab | Move into the focused day's event buttons, then out of the grid |

Moving the focus off the edge of the month grid loads the neighboring month.

**Events in a cell**: each event is a `<button>`.
- The visible text is the start time plus the title for a timed event, or the title with the all-day style for an all-day event.
- The accessible name is the title plus the full date and time range, e.g., "Dentist, Wednesday, October 14, 2026, 9:00 AM to 10:00 AM", or for all-day events "Vacation, all day, Monday, October 12 to Friday, October 16, 2026".

**Overflow**: when a day has more events than fit, a **"N more"** button appears. Its accessible name is "N more events on Wednesday, October 14, 2026", and it opens a dialog listing all of that day's events.

## Dialogs (native `<dialog>` with `showModal`)

When any dialog opens, focus moves to its first control. **Escape** closes the dialog, or asks to discard changes if there are unsaved edits (FR-012). When the dialog closes, focus returns to the control that opened it.

1. **Event details**: shows the title (heading), the full date and time range, the location, and the notes, with empty fields left out. Buttons: **Edit**, **Delete**, **Close**.
2. **Event form (create/edit)**: fields with visible labels:
   - Title (required)
   - All day (checkbox)
   - For a timed event, Start and End (`datetime-local`). For an all-day event, Start date and End date (`date`).
   - Location, Notes (`textarea`)

   Errors appear under their field, linked with `aria-describedby`, and the field gets `aria-invalid="true"`. On a failed submit, focus moves to the first field with an error. Buttons: **Save**, **Cancel**.
   - **DST adjustment** (`422 dst-adjustment-required`): an inline message says, for example, "2:30 AM doesn't exist on Sunday, March 8, 2026 because of the daylight saving time change. The event will start at 3:30 AM." Buttons: **Save with adjusted time** and **Change time**.
   - **Save failed** (`500`): an inline error says "Couldn't save the event. Your changes are still here. Try again." Nothing in the form is cleared (FR-014).
   - **Conflict** (`409`): an inline error says "This event was changed in another window." Button: **Reload event**.
3. **Confirm delete**: says "Delete "Dentist"? This can't be undone." Buttons: **Delete** and **Cancel**, with focus starting on **Cancel**.
4. **Confirm discard**: says "Discard your changes?" Buttons: **Discard** and **Keep editing**.

## Announcements (one polite `aria-live` region)

- After a save: "Saved Dentist, Wednesday, October 14, 2026, 9:00 AM to 10:00 AM."
- After a delete: "Deleted Dentist."
- On a failed submit: "N errors. Fix the highlighted fields."
- After changing months: "October 2026."

## Formatting

All dates and times are formatted with `Intl.DateTimeFormat(undefined, …)` in the zone the API returns. This means formats follow the device locale (FR-021), and every example above is in `en-US`. The accessible names always use the full forms (`dateStyle: "full"`), as FR-020 requires.
