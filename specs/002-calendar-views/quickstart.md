# Quickstart & Validation: Calendar Views and Navigation

This guide explains how to check that the feature works from start to finish. For the details behind these steps, see [contracts/ui-interaction.md](./contracts/ui-interaction.md), [contracts/http-api.md](./contracts/http-api.md), and [data-model.md](./data-model.md). The setup and run commands are the same as in [001 quickstart](../001-event-basics/quickstart.md).

## Automated checks

```powershell
dotnet test backend/PersonalCalendar.slnx
npm test --prefix frontend
npm run lint --prefix frontend
```

**Expected result**: all tests pass, including these new ones:
- **Domain**:
  - `DayTimelineTests`:
    - 23-hour and 25-hour days in `America/Chicago`
    - the DST changes in `Australia/Adelaide` and `Australia/Lord_Howe` (whose DST shift is 30 minutes)
    - `Asia/Kolkata`
    - segments that cross midnight, an event ending exactly at midnight, 2028-02-29, and overlap columns
  - `AllDayLanesTests`: lanes, clipping at the edges of the week, and the Dec–Jan week.
- **Application and API**:
  - `GetDaysView` validation and range checks.
  - `/api/calendar/days` contract tests.
  - A deep link such as `/week/2026-10-14` returns `index.html`.
  - A 7-day request with 5,000 events takes 300 ms or less.
- **Frontend**:
  - `viewState` and `dates` (period steps, clamping, Feb 29, 1900 and 2199 limits, `parsePath` for every row of its table)
  - `todayIn` (near midnight, and in zones with a non-whole-hour offset)
  - `newEventAt` (start time rollover after 23:00)
  - `useSwipe` (thresholds, ignoring vertical movement)
  - the narrow and wide layouts (with `matchMedia` stubbed)
  - an axe check of each view

## Seed data

Using the app or `POST /api/events` with `timeZone=America/Chicago`, create:

| Title | When |
|---|---|
| Dentist | 2026-10-14 09:00–10:30 |
| Standup | 2026-10-14 09:30–09:45 |
| Lunch | 2026-10-14 09:15–11:00 |
| Late show | 2026-10-14 22:00 → 2026-10-15 01:00 |
| Trip | all day, 2026-10-16 → 2026-10-20 |
| Fall back | 2026-11-01 01:30–02:30 |

## Manual validation scenarios

**Primary pass**: use a real phone, or a desktop browser's device emulation set to 390 × 844 with touch enabled, and use **touch only**. Then repeat steps 1–6 on a wide window with a **mouse**. Finally, do the short **keyboard** and **screen reader** pass in steps 13–14.

| # | Scenario | Steps | Expected result |
|---|---|---|---|
| 1 | Month → day | Open `/month/2026-10-14` and tap October 14. | The day view for Wed, Oct 14 opens and the address is `/day/2026-10-14`. On a phone, the month cell showed 3 markers and "+1". |
| 2 | Overlaps | In the day view for Oct 14, look at 9–11 AM. | Dentist, Standup, and Lunch sit side by side and are all readable and tappable. A "+N" chip lists all three. |
| 3 | Swipe | In the day view, swipe left, then right. Then scroll up and down with a slight sideways drift. | Oct 15, then Oct 14. Scrolling never changes the day. Late show appears at the end of the 14th and the start of the 15th. |
| 4 | Week, narrow | Switch to Week. | Seven stacked sections from Oct 11 to 17. Oct 14 lists its events in time order. Trip is listed as all-day on the 16th and 17th. Tapping the "Thursday, October 15" heading opens that day. |
| 5 | Week, wide | On a wide window, open `/week/2026-10-14`. | Hourly grid with overlapping events side by side. Trip is one bar from the 16th to the 17th with its right edge squared off. Clicking empty space at 2:00 PM on the 13th opens New event at 2:00–3:00 PM. |
| 6 | Today and Go to date | Tap Go to date, pick 2028-02-29, and tap Go. Then tap Today. | The same view shows the period with Feb 29, 2028. Then today's period appears with today circled and the current-time line shown. |
| 7 | Month clamping | Open `/month/2026-01-31` and tap Next. | February 2026, and switching to Day shows Feb 28. |
| 8 | Year boundary | Open `/week/2026-12-30` and tap Next. | The title reads "December 27, 2026 – January 2, 2027", then the next week is Jan 3–9, 2027. |
| 9 | DST day | Open `/day/2026-11-01`. | The 1 AM label appears twice and the day is 25 hours tall. Fall back sits in the first 1 AM hour. `/day/2026-03-08` has no 2 AM label. |
| 10 | Refresh and bookmark | On `/week/2026-10-14`, refresh. Open `/week` in a new tab. | The same week appears after the refresh. The new tab shows the current week. |
| 11 | Bad link | Open `/fortnight/2026-02-30`. | The month view for today, with the "couldn't be opened" message, and the address corrected to `/month/<today>`. |
| 12 | Back and Forward | Month → tap a day → Next → Back → Back. | Oct 15 day view → Oct 14 day view → October month view. |
| 13 | Keyboard baseline | Tab to the month grid and press → then Enter. In the week view, press ← past Sunday. Use the New event button. | Enter opens the day view. ← loads the previous week. Focus is visible throughout and every control can be reached. |
| 14 | Screen reader (NVDA or VoiceOver) | Move through each view. | Period titles are announced on change, today is read as "today", phone month days read their event counts, and events read their full date and time range. |
| 15 | Midnight rollover | Set the clock (or `useToday`'s clock in dev tools) to 23:59 with the app open, and wait. | Within 1 minute the today highlight moves to the next day and the view and address stay the same. |

**Done when**: every row above passes, and the automated checks are green.
