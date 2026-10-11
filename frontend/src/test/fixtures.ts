import { vi } from 'vitest'
import type { CalendarApi } from '../api/client'
import type {
  AllDayBar,
  DayCell,
  DaysView,
  EventDetails,
  EventSummary,
  HourMark,
  MonthView,
  Recurrence,
  TimedSegment,
  TimelineDay,
} from '../api/types'
import { addDays } from '../lib/dates'

export const CHICAGO = 'America/Chicago'

export function timedSummary(id: string, title: string, start: string, end: string): EventSummary {
  return { id, title, isAllDay: false, start, end, startDate: null, endDate: null, isRecurring: false, occurrenceDate: null }
}

export function allDaySummary(id: string, title: string, startDate: string, endDate: string): EventSummary {
  return { id, title, isAllDay: true, start: null, end: null, startDate, endDate, isRecurring: false, occurrenceDate: null }
}

/** October 2026 in Chicago (grid 2026-09-27 .. 2026-10-31) with the given events per date. */
export function octoberMonth(eventsByDate: Record<string, EventSummary[]> = {}, today = '2026-09-29'): MonthView {
  const weeks: { days: DayCell[] }[] = []
  let date = '2026-09-27'
  for (let w = 0; w < 5; w++) {
    const days: DayCell[] = []
    for (let d = 0; d < 7; d++) {
      days.push({
        date,
        inMonth: date.startsWith('2026-10'),
        isToday: date === today,
        events: eventsByDate[date] ?? [],
      })
      date = addDays(date, 1)
    }
    weeks.push({ days })
  }
  return { year: 2026, month: 10, timeZone: CHICAGO, today, weeks }
}

export const dentistDetails: EventDetails = {
  id: 'e1',
  title: 'Dentist',
  location: 'Main St Clinic',
  notes: 'Bring insurance card',
  isAllDay: false,
  timeZone: CHICAGO,
  start: '2026-10-14T09:00:00-05:00',
  end: '2026-10-14T10:00:00-05:00',
  startDate: null,
  endDate: null,
  version: 1,
}

/** "Gym" weekly on Monday, Wednesday and Friday at 7:00 AM from 2026-10-12 (quickstart row 1). */
export const gymRecurrence: Recurrence = {
  frequency: 'weekly',
  interval: 1,
  weekdays: ['monday', 'wednesday', 'friday'],
  monthly: null,
  end: { type: 'never' },
  timeZone: CHICAGO,
}

export function recurringSummary(overrides: Partial<EventSummary> = {}): EventSummary {
  return {
    ...timedSummary('s1', 'Gym', '2026-10-21T07:00:00-05:00', '2026-10-21T08:00:00-05:00'),
    isRecurring: true,
    occurrenceDate: '2026-10-21',
    ...overrides,
  }
}

export function recurringDetails(overrides: Partial<EventDetails> = {}): EventDetails {
  return {
    id: 's1',
    title: 'Gym',
    location: null,
    notes: null,
    isAllDay: false,
    timeZone: CHICAGO,
    start: '2026-10-21T07:00:00-05:00',
    end: '2026-10-21T08:00:00-05:00',
    startDate: null,
    endDate: null,
    version: 4,
    recurrence: gymRecurrence,
    occurrenceDate: '2026-10-21',
    seriesStart: '2026-10-12T07:00:00-05:00',
    seriesStartDate: null,
    isException: false,
    exceptionCount: 0,
    ...overrides,
  }
}

export function stubApi(overrides: Partial<CalendarApi> = {}): CalendarApi {
  return {
    getMonth: vi.fn(async () => ({ kind: 'ok' as const, value: octoberMonth() })),
    getDays: vi.fn(async (_tz: string, start: string, count: 1 | 7) => ({ kind: 'ok' as const, value: daysView(start, count) })),
    getEvent: vi.fn(async () => ({ kind: 'ok' as const, value: dentistDetails })),
    createEvent: vi.fn(async () => ({ kind: 'ok' as const, value: dentistDetails })),
    updateEvent: vi.fn(async () => ({ kind: 'ok' as const, value: { ...dentistDetails, version: 2 } })),
    deleteEvent: vi.fn(async () => ({ kind: 'ok' as const, value: null })),
    ...overrides,
  }
}

// Day and week views (contracts/http-api.md "GET /api/calendar/days"). Chicago is on CDT (-05:00) until
// 2026-11-01, so these builders use -05:00 for October dates; fallBackDay() covers the DST change.

function pad(n: number): string {
  return String(n).padStart(2, '0')
}

function hourMarks(count = 24): HourMark[] {
  return Array.from({ length: count }, (_, h) => ({ offsetMinutes: h * 60, label: `${pad(h)}:00` }))
}

export function segment(
  event: EventSummary,
  offsetMinutes: number,
  durationMinutes: number,
  layout: Partial<Omit<TimedSegment, 'event' | 'offsetMinutes' | 'durationMinutes'>> = {},
): TimedSegment {
  return {
    event,
    offsetMinutes,
    durationMinutes,
    continuesBefore: false,
    continuesAfter: false,
    column: 0,
    columnCount: 1,
    ...layout,
  }
}

export function timelineDay(date: string, content: Partial<TimelineDay> = {}): TimelineDay {
  return {
    date,
    isToday: false,
    dayStart: `${date}T00:00:00-05:00`,
    dayEnd: `${addDays(date, 1)}T00:00:00-05:00`,
    lengthMinutes: 1440,
    hourMarks: hourMarks(),
    allDay: [],
    timed: [],
    ...content,
  }
}

export interface DaysContent {
  timed?: Record<string, TimedSegment[]>
  allDay?: Record<string, EventSummary[]>
  bars?: AllDayBar[]
  today?: string
}

export function daysView(start: string, count: 1 | 7, content: DaysContent = {}): DaysView {
  const today = content.today ?? '2026-09-29'
  const days = Array.from({ length: count }, (_, i) => {
    const date = addDays(start, i)
    return timelineDay(date, {
      isToday: date === today,
      timed: content.timed?.[date] ?? [],
      allDay: content.allDay?.[date] ?? [],
    })
  })
  return { timeZone: CHICAGO, today, now: `${today}T10:00:00-05:00`, days, allDayBars: count === 7 ? (content.bars ?? []) : [] }
}

export const dentist = timedSummary('e1', 'Dentist', '2026-10-14T09:00:00-05:00', '2026-10-14T10:30:00-05:00')
export const lunch = timedSummary('e2', 'Lunch', '2026-10-14T09:15:00-05:00', '2026-10-14T11:00:00-05:00')
export const standup = timedSummary('e3', 'Standup', '2026-10-14T09:30:00-05:00', '2026-10-14T09:45:00-05:00')
export const lateShow = timedSummary('e4', 'Late show', '2026-10-14T22:00:00-05:00', '2026-10-15T01:00:00-05:00')
export const trip = allDaySummary('a1', 'Trip', '2026-10-16', '2026-10-20')

/** The quickstart seed data for October 11–17, 2026 (quickstart.md "Seed data"). */
export function octoberWeekContent(): DaysContent {
  return {
    timed: {
      '2026-10-14': [
        segment(dentist, 540, 90, { column: 0, columnCount: 3 }),
        segment(lunch, 555, 105, { column: 1, columnCount: 3 }),
        segment(standup, 570, 15, { column: 2, columnCount: 3 }),
        segment(lateShow, 1320, 120, { continuesAfter: true }),
      ],
      '2026-10-15': [segment(lateShow, 0, 60, { continuesBefore: true })],
    },
    allDay: { '2026-10-16': [trip], '2026-10-17': [trip] },
    bars: [{ event: trip, startIndex: 5, span: 2, lane: 0, continuesBefore: false, continuesAfter: true }],
  }
}

export function octoberWeek(): DaysView {
  return daysView('2026-10-11', 7, octoberWeekContent())
}

export function october14(): DaysView {
  return daysView('2026-10-14', 1, octoberWeekContent())
}

/** 2026-11-01 in Chicago: 25 hours long, the 1 AM hour happens twice. */
export function fallBackDay(): DaysView {
  const marks: HourMark[] = [
    { offsetMinutes: 0, label: '00:00' },
    { offsetMinutes: 60, label: '01:00' },
    { offsetMinutes: 120, label: '01:00' },
    ...Array.from({ length: 22 }, (_, i) => ({ offsetMinutes: 180 + i * 60, label: `${pad(i + 2)}:00` })),
  ]
  return {
    timeZone: CHICAGO,
    today: '2026-09-29',
    now: '2026-09-29T10:00:00-05:00',
    days: [
      timelineDay('2026-11-01', {
        dayStart: '2026-11-01T00:00:00-05:00',
        dayEnd: '2026-11-02T00:00:00-06:00',
        lengthMinutes: 1500,
        hourMarks: marks,
      }),
    ],
    allDayBars: [],
  }
}
