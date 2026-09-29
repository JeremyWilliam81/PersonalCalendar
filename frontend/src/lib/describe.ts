import type { EventSummary } from '../api/types'
import { formatAllDayRange, formatTimedRange } from './format'

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
