# PersonalCalendar

A single-user personal calendar with a clean-architecture .NET backend and a React frontend, built with spec-driven development using [GitHub Spec Kit](https://github.com/github/spec-kit). Feature specs, plans and tasks live in [`specs/`](specs/); project principles are in [`.specify/memory/constitution.md`](.specify/memory/constitution.md).

Everything runs on your own machine: the API listens on **127.0.0.1 only**, needs no account, and stores events in a local SQLite file.

## Prerequisites

- .NET SDK 10.x (`dotnet --version`)
- Node.js 24.x (`node --version`)

One-time setup from the repository root:

```powershell
dotnet tool restore                          # dotnet-ef, for creating migrations
dotnet restore backend/PersonalCalendar.slnx
npm ci --prefix frontend
```

## Run

**Development** (two terminals, with hot reload for the UI):

```powershell
dotnet run --project backend/src/PersonalCalendar.Api     # API on http://127.0.0.1:5178
npm run dev --prefix frontend                             # UI on http://localhost:5173 (proxies /api)
```

**Everyday use** (a single process):

```powershell
npm run build --prefix frontend      # builds the UI into backend/src/PersonalCalendar.Api/wwwroot
dotnet run --project backend/src/PersonalCalendar.Api -c Release
```

Then open http://127.0.0.1:5178.

## Test

```powershell
dotnet test backend/PersonalCalendar.slnx      # domain, application and API integration tests
npm test --prefix frontend                    # component, formatting and accessibility (axe) tests
npm run lint --prefix frontend
```

Date/time tests never depend on the machine's clock or zone: the backend uses NodaTime's `FakeClock` and explicit zones, and the frontend tests run with `TZ=America/Chicago` and the `en-US` locale.

## Views & navigation

The calendar has **day**, **week** and **month** views. The address always names the view and the selected date, so you can refresh or bookmark any view:

- `/{day|week|month}/{yyyy-MM-dd}`: for example `/week/2026-10-14`
- `/week` (no date): that view for today. `/` is this month.

On a touchscreen, swipe left or right to go to the next or previous period; the Previous and Next buttons do the same. Tap a day in the month view to open it, or tap empty time in the day or week view to start an event there.

Phone layouts (stacked week list, month markers) apply below 600 px wide. Because the API listens on 127.0.0.1 only, check them with your browser's device emulation (for example 390 × 844 with touch enabled) rather than a real phone.

## Data

Events are stored in `%LOCALAPPDATA%\PersonalCalendar\calendar.db` (created on first run; the schema is migrated at startup). Delete that file to start with an empty calendar. Override the location with the `ConnectionStrings__Calendar` environment variable, e.g. `Data Source=C:\path\to\calendar.db`.

## Layout

```text
backend/
  src/PersonalCalendar.Domain          calendar rules and date/time logic (NodaTime), no infrastructure
  src/PersonalCalendar.Application     use cases and ports
  src/PersonalCalendar.Infrastructure  EF Core + SQLite persistence and migrations
  src/PersonalCalendar.Api             Minimal API endpoints; serves the built UI
  tests/                               xUnit tests per layer
frontend/                              React + TypeScript (Vite)
specs/001-event-basics/                spec, plan, contracts, tasks for the first feature
```
