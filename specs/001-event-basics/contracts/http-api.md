# HTTP API Contract: Event Basics

The API is served at `http://127.0.0.1:<port>/api` and is reachable from the local machine only (research R1). It uses JSON with camelCase names and does not require authentication.

**Conventions**
- An instant is sent as ISO 8601 with an offset, e.g. `2026-10-14T09:00:00-05:00`, and always comes with a `timeZone`. A date is sent as `yyyy-MM-dd`.
- The local input values in `EventInput` (`start`, `end`) are wall-clock times *paired in the same payload* with `timeZone`. They are never sent without it (Date & Time Standards).
- Errors are returned as RFC 9457 `application/problem+json`. Validation errors use `type: "validation"` and an `errors` map from field name to codes (see the table in [data-model.md](../data-model.md)).
- Every endpoint that depends on a zone requires `timeZone` (an IANA id). An unknown zone returns `400` with the code `timeZone.unknown`.

## GET `/api/calendar/month`

Query: `timeZone` (required), and optionally `year` and `month`. If `year` and `month` are omitted, the current month in `timeZone` is returned, which is how the Today control works.

`200`:
```json
{
  "year": 2026, "month": 10, "timeZone": "America/Chicago", "today": "2026-09-29",
  "weeks": [
    { "days": [
      { "date": "2026-10-14", "inMonth": true, "isToday": false,
        "events": [
          { "id": "3f2c…", "title": "Vacation", "isAllDay": true,
            "startDate": "2026-10-12", "endDate": "2026-10-16" },
          { "id": "9a1b…", "title": "Dentist", "isAllDay": false,
            "start": "2026-10-14T09:00:00-05:00", "end": "2026-10-14T10:00:00-05:00" }
        ] }
    ] }
  ]
}
```
- The grid runs from Sunday to Saturday and has 4 to 6 weeks. The `events` in each day are already in display order (research R6).
- `400`: an unknown zone, or a `year`/`month` outside 1–9998 / 1–12. (Year 9999 is excluded because its December grid would run past the last date NodaTime supports.)

## GET `/api/events/{id}`

Query: `timeZone` (required).

`200` returns `EventDetails`:
```json
{ "id": "9a1b…", "title": "Dentist", "location": "Main St Clinic", "notes": null,
  "isAllDay": false, "timeZone": "America/Chicago",
  "start": "2026-10-14T09:00:00-05:00", "end": "2026-10-14T10:00:00-05:00",
  "startDate": null, "endDate": null, "version": 3 }
```
For an all-day event, `start` and `end` are `null` and `startDate` and `endDate` are set. Fields with no value are `null`, and the UI leaves them out (FR-009).

`404` if the event does not exist.

## POST `/api/events`

Body `EventInput`:
```json
{ "title": "Dentist", "location": null, "notes": null, "isAllDay": false,
  "timeZone": "America/Chicago",
  "start": "2026-10-14T09:00", "end": "2026-10-14T10:00",
  "startDate": null, "endDate": null,
  "acceptAdjustedTimes": false }
```
- A timed event uses `start` and `end` (local `yyyy-MM-ddTHH:mm`). An all-day event uses `startDate` and `endDate`. Fields that don't apply to the event type are ignored.

Responses:
- `201`: returns `EventDetails` with a `Location` header.
- `400 validation`: `{ "type": "validation", "errors": { "end": ["end.notAfterStart"] } }`
- `422 dst-adjustment-required` (research R4). Nothing is saved:
  ```json
  { "type": "dst-adjustment-required",
    "adjustedStart": "2026-03-08T03:30", "adjustedEnd": "2026-03-08T04:00",
    "timeZone": "America/Chicago" }
  ```
  The client shows the adjusted times and resends the request with `acceptAdjustedTimes: true`.
- `500 save-failed`: the database is unavailable or full. The client keeps the form contents (FR-014).

## PUT `/api/events/{id}`

The body is `EventInput` plus `"version": <n>`. The responses are the same as POST, plus:
- `200`: returns the updated `EventDetails` with `version` n+1.
- `404`: not found.
- `409 concurrency-conflict`: `version` is stale (research R8).

## DELETE `/api/events/{id}?version=<n>`

- `204`: deleted permanently.
- `404`: not found.
- `409 concurrency-conflict`: `version` is stale.
