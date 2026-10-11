# UI Interaction Contract: Recurring Events

This extends the [002 UI contract](../../002-calendar-views/contracts/ui-interaction.md). Tapping and clicking come first. Keyboard use meets the Constitution IV baseline. Every target is at least 44 × 44 px (002 FR-013c).

## Repeat section in the event form (research S10)

Order, top to bottom, below the existing date and time fields:

| Control | Touch / pointer | Keyboard | Accessible name / state |
|---|---|---|---|
| **Repeat**: Does not repeat · Daily · Weekly · Monthly · Yearly | One tap selects. A segmented row that wraps to 2 rows under 600 px. | Radio group: arrows move, and Tab leaves the group | Group "Repeat"; each option is a radio |
| **Every [−] N [+] days/weeks/months/years** | Tap − or +. Typing is allowed. Values are kept within 1–99 | Tab to each button or the input | "Repeat interval, every 2 weeks" |
| **Weekdays** (weekly only): S M T W T F S | Each tap toggles. All seven fit in one row at 320 px | Tab to each; Space or Enter toggles | Full name, e.g. "Tuesday", with `aria-pressed` |
| **Monthly** (monthly only): "On day 14" · "On the second Wednesday" · ["On the last Wednesday"] | One tap selects. Full-width cards on narrow screens | Radio group | Group "Monthly on" |
| **Ends**: Never · On [date] · After [−] N [+] times | Tap a card. Its field becomes active | Radio group plus fields | Group "Ends" |
| **Summary** | Read-only text, updated as values change | — | `aria-live="polite"`, at most 1 update per 500 ms |

- The default is "Does not repeat". Choosing Weekly preselects the start date's weekday (FR-002). Choosing Monthly preselects "On day N".
- When the start date changes, the weekday default and the monthly options are refilled (FR-003a). A weekly selection the user already made is kept.
- The adjusted first date (FR-009) appears in the summary: "Starts Thursday, October 8, 2026".
- Errors show next to their field and are announced, as in 001.
- When the form is opened from an occurrence, the section shows the **series** rule.

## Scope choice (research S11)

It opens when Save or Delete is pressed on an occurrence of a series. It doesn't appear for one-time events.

```text
┌──────────────────────────────────────┐
│ Change recurring event               │   (Delete: "Delete recurring event")
│ ┌──────────────────────────────────┐ │
│ │ This event                       │ │   full-width buttons, ≥ 48 px tall
│ └──────────────────────────────────┘ │
│ ┌──────────────────────────────────┐ │
│ │ This and following events        │ │
│ └──────────────────────────────────┘ │
│ ┌──────────────────────────────────┐ │
│ │ All events                       │ │
│ └──────────────────────────────────┘ │
│                          [ Cancel ]  │
└──────────────────────────────────────┘
```

| Situation | Choices shown |
|---|---|
| Delete | This · This and following · All |
| Edit: only text or times changed | This · This and following · All |
| Edit: repeat options changed | This and following · All (FR-016) |
| Edit: start date changed | This only (FR-016a) |
| Edit: start date *and* repeat changed | No dialog. A form error on the start date (FR-016a) |
| Editing or deleting the first occurrence | All three are shown, as the spec scenarios expect. "This and following" acts exactly like All (FR-017, FR-024) |

- **Second step (warning)**: after "This and following" or "All", if the rule or times changed and the series has exceptions (`exceptionCount > 0`), or the new rule is "Does not repeat", the dialog shows the warning text with **Save anyway** and **Back**:
  - FR-020: "Changes you made to single events in this series will be lost."
  - FR-021: "All events except the first will be removed." or "This and following events will be removed, and this one kept as a single event."
- **Cancel** or Escape returns to the form with the edits kept (FR-015). For a delete, nothing is deleted.
- When the dialog opens, focus moves to the first choice. When it closes, focus returns to Save or Delete. It is a modal dialog with a focus trap, reusing `Modal`.
- **Announcements** (polite): "Changed this event.", "Changed this and following events.", "Changed all events.", "Deleted this event.", and so on (FR-030).

## Repeat indicator (research S12)

| Where | Shown | Accessible name addition |
|---|---|---|
| Month view labels (wide) | ↻ icon after the title | ", repeats" |
| Month view markers (narrow) | Not shown. Counted as normal | — (the count only) |
| Day and week time-grid blocks, all-day bars, the narrow week list | ↻ icon after the title, or hidden when the block is too small | ", repeats" |
| Event details | The icon plus the summary, e.g. "↻ Weekly on Monday, Wednesday, and Friday" | The summary is read in full |

## Event details for an occurrence

- The occurrence's own date and time are shown first, as in 001, followed by the repeat summary.
- Edit and Delete are the same buttons as in 001. The scope is asked on Save or Delete, never when the form opens (clarification Q4).
