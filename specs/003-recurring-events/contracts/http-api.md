# HTTP API Contract: Recurring Events

This extends the [001 contract](../../001-event-basics/contracts/http-api.md) and the [002 contract](../../002-calendar-views/contracts/http-api.md). All of their conventions still apply. **One-time events behave exactly as before.** Every field added here is optional or defaults to "not recurring", so the existing contract tests stay valid.

## The `recurrence` object

The same shape is used in requests and responses. `null` (or a missing field) means "does not repeat".

```json
{
  "frequency": "weekly",
  "interval": 2,
  "weekdays": ["tuesday", "thursday"],
  "monthly": null,
  "end": { "type": "count", "count": 10 }
}
```

| Field | Values | Notes |
|---|---|---|
| `frequency` | `daily` \| `weekly` \| `monthly` \| `yearly` | Required. |
| `interval` | 1–99 | The default is 1. |
| `weekdays` | Lowercase English weekday names | Weekly only, with at least one. Ignored for other frequencies. |
| `monthly` | `{ "type": "dayOfMonth" }` or `{ "type": "weekdayPosition", "ordinal": 1\|2\|3\|4\|-1 }` | Monthly only. The day number or weekday comes from the start date. |
| `end` | `{ "type": "never" }`, `{ "type": "until", "until": "2026-12-31" }`, or `{ "type": "count", "count": 10 }` | `until` is inclusive. |

Responses also include `"timeZone": "America/Chicago"` inside `recurrence`, which is the zone the series repeats in (research S3).

## `EventSummary` (month and days views, additive)

```json
{ "id": "9a1b…", "title": "Gym", "isAllDay": false,
  "start": "2026-10-21T07:00:00-05:00", "end": "2026-10-21T08:00:00-05:00",
  "startDate": null, "endDate": null,
  "isRecurring": true, "occurrenceDate": "2026-10-21" }
```

- For an occurrence, `id` is the **series** id, and `occurrenceDate` is its original date (research S4). The pair identifies the occurrence in every request below.
- For a one-time event, `isRecurring` is `false` and `occurrenceDate` is `null`.
- A moved occurrence keeps its original `occurrenceDate`, even when `start` falls on another day.

## GET `/api/events/{id}?timeZone=…&occurrence=yyyy-MM-dd`

`occurrence` is required for a series (`400 occurrence.required`) and rejected for a one-time event (`400 occurrence.invalid`). A date the series doesn't produce, or one that was deleted, returns `404`.

`200` returns `EventDetails` with these additions:
```json
{ "...001 fields, for this occurrence...": "",
  "recurrence": { "frequency": "weekly", "interval": 1, "weekdays": ["monday","wednesday","friday"],
                  "monthly": null, "end": { "type": "never" }, "timeZone": "America/Chicago" },
  "occurrenceDate": "2026-10-21",
  "seriesStart": "2026-10-12T07:00:00-05:00",
  "seriesStartDate": null,
  "isException": false,
  "exceptionCount": 2,
  "version": 7 }
```
- `start`, `end`, `startDate`, and `endDate` are **this occurrence's** values.
- `seriesStart` and `seriesStartDate` are the first occurrence's values. The form uses them to fill in the monthly options and the weekday default for a series edit.
- `version` is the series version. It covers the series and all of its exceptions.

## POST `/api/events`

The body is `EventInput` plus an optional `recurrence`. The server normalizes the start to the first real occurrence (FR-009). The response is `201` with `EventDetails` for the first occurrence.

## PUT `/api/events/{id}?occurrence=yyyy-MM-dd&scope=this|following|all`

The body is `EventInput` (with `version`) plus `recurrence`, which is the full desired rule (`null` means "does not repeat").

| Event | Query | Result |
|---|---|---|
| One-time | none | Same as 001. Sending a non-null `recurrence` turns it into a series (`200`, details of the first occurrence). |
| One-time | `occurrence` or `scope` present | `400 occurrence.invalid` / `scope.invalid` |
| Series | missing `occurrence` / `scope` | `400 occurrence.required` / `scope.required` |
| Series, `scope=this` | `recurrence` differs from the series rule | `400 scope.thisWithRepeatChange` |
| Series, `following`/`all` | start date differs from the occurrence's current date | `400 scope.dateChangeRequiresThis` |
| Series, any scope | start date *and* `recurrence` both changed | `400 scope.dateAndRepeatChanged` |
| Series, valid | | `200` with `EventDetails` of the edited occurrence. For `following`, `id` is the **new** series id. |

- DST gap handling (`acceptAdjustedTimes`, the `dstAdjustment` result) is unchanged from 001.
- A stale `version` returns `409`. A failed save returns `500`, and nothing is changed (FR-027).

## DELETE `/api/events/{id}?version=<n>&occurrence=yyyy-MM-dd&scope=this|following|all`

The same `occurrence` and `scope` rules apply as for `PUT`. The response is `204`. When `this` removes the last visible occurrence, or `following` is used at the first occurrence, the whole series is deleted (FR-024). A stale `version` returns `409`.

## Unchanged

`GET /api/calendar/month` and `GET /api/calendar/days` keep their query parameters and shapes. Their `EventSummary`s now include occurrences, and the two new fields.
