// Shapes from specs/001-event-basics/contracts/http-api.md.

/** yyyy-MM-dd */
export type DateString = string
/** ISO 8601 with offset, e.g. 2026-10-14T09:00:00-05:00 */
export type OffsetDateTimeString = string
/** yyyy-MM-ddTHH:mm wall-clock time; always sent together with a timeZone. */
export type LocalDateTimeString = string

// Recurring events: specs/003-recurring-events/contracts/http-api.md.

export type Weekday = 'sunday' | 'monday' | 'tuesday' | 'wednesday' | 'thursday' | 'friday' | 'saturday'

export type Frequency = 'daily' | 'weekly' | 'monthly' | 'yearly'

export type MonthlyPattern = { type: 'dayOfMonth' } | { type: 'weekdayPosition'; ordinal: 1 | 2 | 3 | 4 | -1 }

export type RepeatEnd = { type: 'never' } | { type: 'until'; until: DateString } | { type: 'count'; count: number }

export interface Recurrence {
  frequency: Frequency
  interval: number
  /** Weekly only. */
  weekdays: Weekday[]
  /** Monthly only. */
  monthly: MonthlyPattern | null
  end: RepeatEnd
  /** In responses: the zone the series repeats in. */
  timeZone?: string
}

/** Where a change to one occurrence of a series applies (FR-015). */
export type EditScope = 'this' | 'following' | 'all'

export interface EventSummary {
  /** For an occurrence, the series id. */
  id: string
  title: string
  isAllDay: boolean
  start?: OffsetDateTimeString | null
  end?: OffsetDateTimeString | null
  startDate?: DateString | null
  endDate?: DateString | null
  isRecurring?: boolean
  /** The occurrence's original date; with `id` it identifies the occurrence. */
  occurrenceDate?: DateString | null
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
  /** For a series: covers the series and all its changed or deleted occurrences. */
  version: number
  recurrence?: Recurrence | null
  occurrenceDate?: DateString | null
  /** The first occurrence's start (timed) or start date (all-day). */
  seriesStart?: OffsetDateTimeString | null
  seriesStartDate?: DateString | null
  isException?: boolean
  exceptionCount?: number
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
  /** null or absent: does not repeat. */
  recurrence?: Recurrence | null
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

// Day and week views: specs/002-calendar-views/contracts/http-api.md.

export interface HourMark {
  offsetMinutes: number
  /** Local wall time, HH:mm. A repeated hour appears twice; a skipped hour is absent. */
  label: string
}

export interface TimedSegment {
  /** The whole event, not clipped to the day. */
  event: EventSummary
  offsetMinutes: number
  durationMinutes: number
  continuesBefore: boolean
  continuesAfter: boolean
  column: number
  columnCount: number
}

export interface AllDayBar {
  event: EventSummary
  startIndex: number
  span: number
  lane: number
  continuesBefore: boolean
  continuesAfter: boolean
}

export interface TimelineDay {
  date: DateString
  isToday: boolean
  dayStart: OffsetDateTimeString
  dayEnd: OffsetDateTimeString
  lengthMinutes: number
  hourMarks: HourMark[]
  allDay: EventSummary[]
  timed: TimedSegment[]
}

export interface DaysView {
  timeZone: string
  today: DateString
  now: OffsetDateTimeString
  days: TimelineDay[]
  allDayBars: AllDayBar[]
}
