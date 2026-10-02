# HTTP API Contract: Calendar Views and Navigation

This contract adds one endpoint to the API from [001 http-api](../../001-event-basics/contracts/http-api.md). Its conventions apply unchanged: loopback only, camelCase JSON, ISO 8601 with offsets, `timeZone` required, and errors as RFC 9457 problem details. No existing endpoint changes.

## GET `/api/calendar/days`

Returns one day (`count=1`, the day view) or seven consecutive days (`count=7`, the week view in both layouts), laid out on a time-of-day scale (research V3–V5).

**Query**:

| Name | Required | Format | Notes |
|---|---|---|---|
| `timeZone` | yes | IANA id | |
| `start` | yes | `yyyy-MM-dd` | The first day. For the week view the client sends the Sunday, and the server does not move it to a Sunday. |
| `count` | yes | `1` or `7` | |

**`200`** (`America/Chicago`, `count=1`, on the fall-back day 2026-11-01, abbreviated):

```json
{
  "timeZone": "America/Chicago",
  "today": "2026-11-01",
  "now": "2026-11-01T01:20:00-06:00",
  "days": [
    {
      "date": "2026-11-01",
      "isToday": true,
      "dayStart": "2026-11-01T00:00:00-05:00",
      "dayEnd": "2026-11-02T00:00:00-06:00",
      "lengthMinutes": 1500,
      "hourMarks": [
        { "offsetMinutes": 0,   "label": "00:00" },
        { "offsetMinutes": 60,  "label": "01:00" },
        { "offsetMinutes": 120, "label": "01:00" },
        { "offsetMinutes": 180, "label": "02:00" }
      ],
      "allDay": [
        { "id": "3f2c…", "title": "Vacation", "isAllDay": true, "startDate": "2026-10-30", "endDate": "2026-11-02" }
      ],
      "timed": [
        { "event": { "id": "9a1b…", "title": "Late show", "isAllDay": false,
                     "start": "2026-10-31T22:00:00-05:00", "end": "2026-11-01T01:00:00-05:00" },
          "offsetMinutes": 0, "durationMinutes": 60,
          "continuesBefore": true, "continuesAfter": false,
          "column": 0, "columnCount": 1 }
      ]
    }
  ],
  "allDayBars": []
}
```

The response follows these rules (the full definitions are in [data-model.md](../data-model.md)):
- `hourMarks[].label` is a local wall time (`HH:mm`). The client formats it for display in the device locale.
  - A repeated hour appears twice, with different offsets.
  - A skipped hour does not appear.
  - The client positions everything with `offsetMinutes`, never with the label.
- `dayStart` and `dayEnd` are the real instants where the day begins and ends, so `lengthMinutes = (dayEnd − dayStart)`.
- `timed` is in display order. Each segment is clipped to the day, and `event` is the event's complete, unclipped `EventSummary`, so its accessible name shows the full range.
- `allDayBars` (only when `count=7`) has entries shaped like `{ "event": EventSummary, "startIndex": 0-6, "span": 1-7, "lane": n, "continuesBefore": bool, "continuesAfter": bool }`. Each day's `allDay` list is still filled in, for the phone week list.
- `today` and `now` come from the server clock in `timeZone`. They are the source of truth for `isToday` in this response. The client's `useToday` handles changes while the page stays open (research V11).

**`400 validation`**: the `errors` codes are `timeZone.unknown`, `start.invalid`, `start.outOfRange` (a year outside 1–9998, as for the month endpoint; the 1900–2199 navigation range is a UI rule), and `count.invalid`.

**Performance**: with 5,000 stored events, a `count=7` response takes 300 ms or less on the developer's machine, which leaves room for SC-002 (1 s end to end). An extension to `PerformanceTests` checks this.

## Client routes (served by the existing SPA fallback)

These are not API endpoints. They are listed here because the server must keep serving `index.html` for them.

| Path | Served | Notes |
|---|---|---|
| `/`, `/day`, `/week`, `/month`, `/{view}/{yyyy-MM-dd}` | `index.html` (`MapFallbackToFile`, from feature 001) | The client parses the path (data-model `parsePath`). |
| Any other path outside `/api` | `index.html` | The client shows its "That link couldn't be opened" message and corrects the address. |
| `/api/...` with no matching endpoint | `404` | Unchanged from 001. |

An API integration test checks that `GET /week/2026-10-14` returns `index.html` with status `200`.
