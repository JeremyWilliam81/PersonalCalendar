# Quickstart & Validation: Event Basics

This guide explains how to run the feature and check that it works from start to finish. For the details behind these steps, see [contracts/http-api.md](./contracts/http-api.md), [contracts/ui-interaction.md](./contracts/ui-interaction.md), and [data-model.md](./data-model.md).

## Prerequisites

- .NET SDK 10.x (`dotnet --version`)
- Node.js 24.x (`node --version`)
- From the repository root, run once:
  - `dotnet restore backend/PersonalCalendar.slnx`
  - `npm ci --prefix frontend`

## Automated checks

```powershell
dotnet test backend/PersonalCalendar.slnx      # domain + application + API integration tests
npm test --prefix frontend                    # Vitest + Testing Library + axe
npm run lint --prefix frontend
```
**Expected result**: all tests pass. The domain tests include the date/time hazard cases required by Constitution I (DST gaps and overlaps in `America/Chicago`, `Asia/Kolkata`, `Australia/Adelaide`, 2028-02-29, events crossing midnight, and month and year boundaries).

## Run the app

**Development** (two terminals):
```powershell
dotnet run --project backend/src/PersonalCalendar.Api     # http://127.0.0.1:5178
npm run dev --prefix frontend                             # http://localhost:5173 (proxies /api)
```
**Single process** (everyday use):
```powershell
npm run build --prefix frontend      # outputs to backend/src/PersonalCalendar.Api/wwwroot
dotnet run --project backend/src/PersonalCalendar.Api -c Release
```
The database file is created at `%LOCALAPPDATA%\PersonalCalendar\calendar.db`. To start from an empty calendar, delete that file.

## Manual validation scenarios

The steps are keyboard-only unless noted.

| # | Scenario | Steps | Expected result |
|---|---|---|---|
| 1 | Create and see an event (US1) | Tab into the grid, arrow to Oct 14, press Enter, type the title "Dentist", set 9:00 to 10:00, and Save | "Dentist" appears on Oct 14, and the screen reader announces it was saved |
| 2 | End not after start (FR-003) | Create an event with the end equal to the start | The error "End must be after start." appears on the End field, focus moves there, and nothing is saved |
| 3 | Persistence (FR-013) | Stop the API, start it again, and reload the page | Every event is still there with the same details |
| 4 | Details (US2) | Tab to "Dentist" and press Enter | The details dialog shows every field, with empty fields left out. Escape returns focus to the event |
| 5 | Edit and delete (US3) | Edit the time to Oct 15. Then delete it, choosing Cancel first and then Delete | The event moves to Oct 15. After the delete, it is gone and stays gone after a restart |
| 6 | All-day across several days (US4) | Create an all-day event from Oct 20 to 23 | It shows as all-day on the 20th through the 23rd, above any timed events |
| 7 | DST gap (FR-017) | Set the OS zone to America/Chicago and create an event on 2027-03-14 at 02:30 | You are asked to confirm 3:30 AM, and after you confirm, it is saved at 3:30 |
| 8 | Zone change (clarification Q2) | Create an event at 9:00 AM, change the OS zone to America/New_York, and reload | It shows at 10:00 AM. All-day events have not moved |
| 9 | Locale (FR-021) | Switch the browser language to en-GB and reload | Dates show as day/month and times use the 24-hour clock |
| 10 | Overflow | Add 6 events on one day | An "N more" button appears and lists all of the day's events |
| 11 | Screen reader | Use NVDA or Narrator to go through scenarios 1, 4, and 6 | Full dates and times are read out, and every control has a name |
| 12 | Save failure (FR-014) | While the form is open, stop the API and click Save | The "Couldn't save" message appears and the form still has your input |
| 13 | Two tabs (R8) | Open the same event in two tabs, save in one, then save in the other | The second tab shows the conflict message |

## Timing check (SC-004)

The API integration test `MonthView_With5000Events_RespondsQuickly` adds 5,000 events to a temporary database and checks that `GET /api/calendar/month` responds in under 500 ms. That leaves the rest of the 1-second budget for rendering. To check by hand, point the app at a copy of that seeded database and switch months. **Expected result**: each month appears in under 1 second.
