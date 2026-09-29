// Shapes from specs/001-event-basics/contracts/http-api.md.

/** yyyy-MM-dd */
export type DateString = string
/** ISO 8601 with offset, e.g. 2026-10-14T09:00:00-05:00 */
export type OffsetDateTimeString = string
/** yyyy-MM-ddTHH:mm wall-clock time; always sent together with a timeZone. */
export type LocalDateTimeString = string

export interface EventSummary {
  id: string
  title: string
  isAllDay: boolean
  start?: OffsetDateTimeString | null
  end?: OffsetDateTimeString | null
  startDate?: DateString | null
  endDate?: DateString | null
}

export interface DayCell {
  date: DateString
  inMonth: boolean
  isToday: boolean
  events: EventSummary[]
}

export interface MonthView {
  year: number
  month: number
  timeZone: string
  today: DateString
  weeks: { days: DayCell[] }[]
}

export interface EventDetails {
  id: string
  title: string
  location: string | null
  notes: string | null
  isAllDay: boolean
  timeZone: string
  start: OffsetDateTimeString | null
  end: OffsetDateTimeString | null
  startDate: DateString | null
  endDate: DateString | null
  version: number
}

export interface EventInput {
  title: string
  location: string | null
  notes: string | null
  isAllDay: boolean
  timeZone: string
  start: LocalDateTimeString | null
  end: LocalDateTimeString | null
  startDate: DateString | null
  endDate: DateString | null
  acceptAdjustedTimes: boolean
  version?: number
}

export type FieldErrors = Record<string, string[]>

export type ApiResult<T> =
  | { kind: 'ok'; value: T }
  | { kind: 'validation'; errors: FieldErrors }
  | { kind: 'dstAdjustment'; adjustedStart: LocalDateTimeString; adjustedEnd: LocalDateTimeString; timeZone: string }
  | { kind: 'conflict' }
  | { kind: 'notFound' }
  | { kind: 'saveFailed' }
  | { kind: 'network' }
