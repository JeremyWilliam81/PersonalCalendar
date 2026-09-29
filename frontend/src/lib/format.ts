import { getLocale } from './locale'

// All user-facing date/time text goes through Intl in the device locale (FR-021).
// Plain dates (all-day events, grid days) are formatted in UTC so they never shift.

type WeekdayWidth = 'long' | 'short'

function normalize(text: string): string {
  // Newer ICU puts a narrow no-break space before AM/PM; a plain space reads identically.
  return text.replace(/ /g, ' ')
}

function dateOnlyToUtc(date: string): Date {
  return new Date(`${date}T00:00:00Z`)
}

export function formatTimeShort(isoWithOffset: string, timeZone: string): string {
  return normalize(
    new Intl.DateTimeFormat(getLocale(), { timeStyle: 'short', timeZone }).format(new Date(isoWithOffset)),
  )
}

/** A wall-clock time (yyyy-MM-ddTHH:mm) exactly as written, with no zone applied. */
export function formatLocalTime(localDateTime: string): string {
  return normalize(
    new Intl.DateTimeFormat(getLocale(), { timeStyle: 'short', timeZone: 'UTC' }).format(
      new Date(`${localDateTime}:00Z`),
    ),
  )
}

export function formatFullDate(date: string): string {
  return new Intl.DateTimeFormat(getLocale(), { dateStyle: 'full', timeZone: 'UTC' }).format(dateOnlyToUtc(date))
}

function formatFullDateOfInstant(isoWithOffset: string, timeZone: string): string {
  return new Intl.DateTimeFormat(getLocale(), { dateStyle: 'full', timeZone }).format(new Date(isoWithOffset))
}

export function formatTimedRange(start: string, end: string, timeZone: string): string {
  const startText = `${formatFullDateOfInstant(start, timeZone)}, ${formatTimeShort(start, timeZone)}`
  const sameDay = localDateInZone(start, timeZone) === localDateInZone(end, timeZone)
  const endText = sameDay
    ? formatTimeShort(end, timeZone)
    : `${formatFullDateOfInstant(end, timeZone)}, ${formatTimeShort(end, timeZone)}`
  return `${startText} to ${endText}`
}

export function formatAllDayRange(startDate: string, endDate: string): string {
  if (startDate === endDate) return formatFullDate(startDate)
  if (startDate.slice(0, 4) !== endDate.slice(0, 4)) {
    return `${formatFullDate(startDate)} to ${formatFullDate(endDate)}`
  }
  const withoutYear = new Intl.DateTimeFormat(getLocale(), {
    weekday: 'long',
    month: 'long',
    day: 'numeric',
    timeZone: 'UTC',
  }).format(dateOnlyToUtc(startDate))
  return `${withoutYear} to ${formatFullDate(endDate)}`
}

export function formatMonthTitle(year: number, month: number): string {
  return new Intl.DateTimeFormat(getLocale(), { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(
    new Date(Date.UTC(year, month - 1, 1)),
  )
}

/** 0 = Sunday. */
export function formatWeekdayName(dayIndex: number, width: WeekdayWidth): string {
  // 2026-10-04 is a Sunday.
  return new Intl.DateTimeFormat(getLocale(), { weekday: width, timeZone: 'UTC' }).format(
    new Date(Date.UTC(2026, 9, 4 + dayIndex)),
  )
}

function partsInZone(isoWithOffset: string, timeZone: string): Record<string, string> {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(new Date(isoWithOffset))
  return Object.fromEntries(parts.map((p) => [p.type, p.value]))
}

/** The calendar date (yyyy-MM-dd) of an instant in a zone. */
export function localDateInZone(isoWithOffset: string, timeZone: string): string {
  const p = partsInZone(isoWithOffset, timeZone)
  return `${p.year}-${p.month}-${p.day}`
}

/** An instant as a `datetime-local` input value (yyyy-MM-ddTHH:mm) in a zone. */
export function toLocalInputValue(isoWithOffset: string, timeZone: string): string {
  const p = partsInZone(isoWithOffset, timeZone)
  return `${p.year}-${p.month}-${p.day}T${p.hour}:${p.minute}`
}
