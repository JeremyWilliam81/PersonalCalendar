# Quickstart & Validation: Recurring Events

This guide explains how to check that the feature works from start to finish. For the details, see [contracts/ui-interaction.md](./contracts/ui-interaction.md), [contracts/http-api.md](./contracts/http-api.md), and [data-model.md](./data-model.md). The setup and run commands are the same as in the [001 quickstart](../001-event-basics/quickstart.md). The new migration (`AddRecurrence`) is applied automatically at startup, as in 001.

## Automated checks

```powershell
dotnet test backend/PersonalCalendar.slnx
npm test --prefix frontend
npm run lint --prefix frontend
```

**Expected result**: all tests pass, including the existing 001 and 002 suites with no change in behavior, plus these new ones:
- **Domain**:
  - `RepeatRuleTests`: RRULE round-trip for every row of research S2, and validation limits.
  - `RecurrenceExpandTests`: the S13 matrix, including intervals, day 29–31 clamping, Nth and last weekday, Feb 29, COUNT with deletions, inclusive UNTIL, fast-forward equal to walking from the start, and the DST cases in `America/Chicago`, `Australia/Lord_Howe`, `Asia/Kolkata`, and `Australia/Adelaide`.
  - `SeriesScopeTests`: every S6 operation.
- **Application and API**:
  - The scope and occurrence validation table in the HTTP contract.
  - Split atomicity: a forced save failure leaves the original series unchanged.
  - 001 and 002 contract tests unchanged.
  - The performance test with 5,000 events plus 200 series that never end: month and 7-day views each in 300 ms or less.
- **Migration**: a database created by the 001 migration upgrades with its events intact.
- **Frontend**:
  - `lib/recurrence` (`monthlyOptions`, `firstOccurrence`, `describeRule`) with the same dates as the backend.
  - `RepeatFields` and `ScopeChoiceDialog`: the choice filtering table, the warning step, and Cancel keeping the edits.
  - The repeat indicator's accessible name.
  - axe checks of the form and dialog.

## Manual scenarios

Run the app with the device zone set to `America/Chicago`. Use the browser's device emulation at 375 × 667 with touch for the phone checks, and a normal window for the wide checks.

| # | Steps | Expected |
|---|---|---|
| 1 | On Mon Oct 12, 2026, create "Gym" 7:00–8:00, Weekly, tap W and F so M, W, and F are selected, Ends Never | The summary reads "Weekly on Monday, Wednesday, and Friday". It appears on every M/W/F in the month, week, and day views, with the ↻ icon |
| 2 | Create an event on Tue Oct 6, 2026: Every 2 weeks on Tue and Thu, After 6 times | Oct 6, 8, 20, 22, Nov 3, 5, and nothing after |
| 3 | Create an event on Jan 31, 2027: Monthly, "On day 31" | Jan 31, Feb 28, Mar 31, Apr 30… The summary mentions the last day of shorter months |
| 4 | Create an event on Fri Oct 30, 2026: Monthly | It offers "On day 30" and "On the last Friday". Choosing "last Friday" gives Nov 27 and Dec 25 |
| 5 | Create an all-day event on Feb 29, 2028, Yearly | It appears in 2032 and 2036 only |
| 6 | Navigate to the week of Mar 7–13, 2027 (DST starts Mar 14) and the next week | "Gym" is at 7:00 AM in both weeks |
| 7 | Open the Wed Oct 21 occurrence of "Gym", change the time to 18:00, Save, and tap **This event** | Only Oct 21 moves. The details still show the repeat summary |
| 8 | Open the Nov 2 occurrence, change the title to "Swim", Save, and choose **This and following events** | Before Nov 2 it's "Gym"; from Nov 2 on it's "Swim" |
| 9 | Open any "Swim" occurrence, change the weekdays to M and Th, and Save | "This event" isn't offered. Choosing All shows the "single changes will be lost" warning |
| 10 | Open an occurrence, change its date *and* its weekdays, and Save | No dialog. A message on the start date asks to undo one of the changes |
| 11 | Open an occurrence, change its date only, and Save | Only "This event" is offered |
| 12 | Delete the Oct 28 occurrence of "Gym" with **This event**, then delete another with **All events** | Oct 28 disappears, then the whole series does |
| 13 | Reload the page | Every result above is still there |
| 14 | Set the device zone to `America/New_York` and reload | The Chicago 7:00 series shows at 8:00 AM, and all-day series don't move |
| 15 | Keyboard only: create a weekly series, toggle weekdays with Space, choose a scope with Tab and Enter | Everything works, focus is visible, and the scope result is announced |
| 16 | Screen reader on an occurrence | It reads "Gym, Wednesday, October 21, 2026, 7:00 AM to 8:00 AM, repeats" |

## Seed data via the API (optional)

```powershell
$body = @{ title='Gym'; isAllDay=$false; timeZone='America/Chicago'; start='2026-10-12T07:00'; end='2026-10-12T08:00';
           acceptAdjustedTimes=$false; recurrence=@{ frequency='weekly'; interval=1; weekdays=@('monday','wednesday','friday'); end=@{ type='never' } } } |
        ConvertTo-Json -Depth 5
Invoke-RestMethod -Method Post -Uri http://127.0.0.1:5178/api/events -ContentType 'application/json' -Body $body
```

The port is the one in `launchSettings.json` (5178).
