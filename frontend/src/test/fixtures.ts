import { vi } from 'vitest'
import type { CalendarApi } from '../api/client'
import type { DayCell, EventDetails, EventSummary, MonthView } from '../api/types'
import { addDays } from '../lib/dates'

export const CHICAGO = 'America/Chicago'

export function timedSummary(id: string, title: string, start: string, end: string): EventSummary {
  return { id, title, isAllDay: false, start, end, startDate: null, endDate: null }
}

export function allDaySummary(id: string, title: string, startDate: string, endDate: string): EventSummary {
  return { id, title, isAllDay: true, start: null, end: null, startDate, endDate }
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

export function stubApi(overrides: Partial<CalendarApi> = {}): CalendarApi {
  return {
    getMonth: vi.fn(async () => ({ kind: 'ok' as const, value: octoberMonth() })),
    getEvent: vi.fn(async () => ({ kind: 'ok' as const, value: dentistDetails })),
    createEvent: vi.fn(async () => ({ kind: 'ok' as const, value: dentistDetails })),
    updateEvent: vi.fn(async () => ({ kind: 'ok' as const, value: { ...dentistDetails, version: 2 } })),
    deleteEvent: vi.fn(async () => ({ kind: 'ok' as const, value: null })),
    ...overrides,
  }
}
