import type { EventSummary } from '../api/types'
import { formatAllDayRange, formatFullDate, formatTimedRange } from './format'

type Describable = Pick<EventSummary, 'title' | 'isAllDay' | 'start' | 'end' | 'startDate' | 'endDate'>

/** The full date/time range of an event, as screen readers should hear it (FR-020). */
export function describeRange(event: Describable, timeZone: string): string {
  if (event.isAllDay && event.startDate && event.endDate) {
    return `all day, ${formatAllDayRange(event.startDate, event.endDate)}`
  }
  if (event.start && event.end) return formatTimedRange(event.start, event.end, timeZone)
  return ''
}

/** "Dentist, Wednesday, October 14, 2026, 9:00 AM to 10:00 AM" */
export function describeEvent(event: Describable, timeZone: string): string {
  const range = describeRange(event, timeZone)
  return range ? `${event.title}, ${range}` : event.title
}

interface DayDescription {
  isToday?: boolean
  /** Included on phone-sized month cells, where events show only as markers (FR-003a). */
  eventCount?: number
}

/** "Wednesday, October 14, 2026, today, 5 events, open in day view" (FR-013, FR-026). */
export function describeDay(date: string, { isToday = false, eventCount }: DayDescription = {}): string {
  const parts = [formatFullDate(date)]
  if (isToday) parts.push('today')
  if (eventCount !== undefined) parts.push(eventCount === 0 ? 'no events' : eventCount === 1 ? '1 event' : `${eventCount} events`)
  parts.push('open in day view')
  return parts.join(', ')
}
