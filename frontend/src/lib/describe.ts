import type { EventSummary } from '../api/types'
import { formatAllDayRange, formatFullDate, formatTimedRange, localDateInZone } from './format'

type Describable = Pick<EventSummary, 'title' | 'isAllDay' | 'start' | 'end' | 'startDate' | 'endDate' | 'isRecurring'>

/** The full date/time range of an event, as screen readers should hear it (FR-020). */
export function describeRange(event: Describable, timeZone: string): string {
  if (event.isAllDay && event.startDate && event.endDate) {
    return `all day, ${formatAllDayRange(event.startDate, event.endDate)}`
  }
  if (event.start && event.end) return formatTimedRange(event.start, event.end, timeZone)
  return ''
}

/** "Dentist, Wednesday, October 14, 2026, 9:00 AM to 10:00 AM", plus ", repeats" for an occurrence (003 FR-030). */
export function describeEvent(event: Describable, timeZone: string): string {
  const range = describeRange(event, timeZone)
  const text = range ? `${event.title}, ${range}` : event.title
  return event.isRecurring ? `${text}, repeats` : text
}

/** True when a timed event began on a date before `date` in the zone, so on `date` it shows from 12:00 AM (003 FR-032). */
export function continuesFromPreviousDay(event: Pick<EventSummary, 'isAllDay' | 'start'>, date: string, timeZone: string): boolean {
  return !event.isAllDay && !!event.start && localDateInZone(event.start, timeZone) < date
}

/** {@link describeEvent}, plus ", continues from the previous day" on a day after the one it starts on. */
export function describeEventOnDay(event: Describable, timeZone: string, continuesBefore: boolean): string {
  const text = describeEvent(event, timeZone)
  return continuesBefore ? `${text}, continues from the previous day` : text
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

/** A React key for an event or one occurrence of a series; occurrences share their series id (003 research S4). */
export function eventKey(event: { id: string; occurrenceDate?: string | null }): string {
  return `${event.id}:${event.occurrenceDate ?? ''}`
}
