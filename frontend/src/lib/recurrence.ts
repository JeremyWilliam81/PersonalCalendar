import type { DateString, Frequency, MonthlyPattern, Recurrence, Weekday } from '../api/types'
import { addDays, dayOfWeek } from './dates'
import { formatFullDate } from './format'
import { getLocale } from './locale'

// Zone-free helpers for the repeat controls (research S10). The server is authoritative; these only preview
// what it will do, using the same rules as backend Domain/Events/RepeatRule.cs and Recurrence.cs.

/** Sunday first, matching the week layout. */
export const WEEKDAYS: Weekday[] = ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday']

export type MonthlyOption = MonthlyPattern & { label: string }

const ORDINAL_WORDS: Record<number, string> = { 1: 'first', 2: 'second', 3: 'third', 4: 'fourth', [-1]: 'last' }

function utc(date: DateString): Date {
  return new Date(`${date}T00:00:00Z`)
}

function dayNumber(date: DateString): number {
  return Number(date.slice(8, 10))
}

function daysInMonth(date: DateString): number {
  const [year, month] = date.split('-').map(Number)
  return new Date(Date.UTC(year, month, 0)).getUTCDate()
}

export function weekdayOf(date: DateString): Weekday {
  return WEEKDAYS[dayOfWeek(date)]
}

/** The weekday's name in the device locale, e.g. "Wednesday". */
export function weekdayName(day: Weekday, width: 'long' | 'narrow' = 'long'): string {
  // 2026-10-11 is a Sunday.
  const date = addDays('2026-10-11', WEEKDAYS.indexOf(day))
  return new Intl.DateTimeFormat(getLocale(), { weekday: width, timeZone: 'UTC' }).format(utc(date))
}

/**
 * "On day 14" plus the start's weekday position: first to fourth, and "last" when the start is in the month's
 * last 7 days. A fifth weekday offers only "last", because many months have no fifth one (FR-003).
 */
export function monthlyOptions(start: DateString): MonthlyOption[] {
  const day = dayNumber(start)
  const name = weekdayName(weekdayOf(start))
  const options: MonthlyOption[] = [{ type: 'dayOfMonth', label: `On day ${day}` }]
  const position = Math.ceil(day / 7)
  if (position <= 4) {
    options.push({ type: 'weekdayPosition', ordinal: position as 1 | 2 | 3 | 4, label: `On the ${ORDINAL_WORDS[position]} ${name}` })
  }
  if (day > daysInMonth(start) - 7) {
    options.push({ type: 'weekdayPosition', ordinal: -1, label: `On the last ${name}` })
  }
  return options
}

/** The start date, or for weekly rules the first chosen weekday on or after it (FR-009). */
export function firstOccurrence(start: DateString, recurrence: Recurrence): DateString {
  if (recurrence.frequency !== 'weekly' || recurrence.weekdays.length === 0) return start
  let date = start
  while (!recurrence.weekdays.includes(weekdayOf(date))) date = addDays(date, 1)
  return date
}

export function defaultRecurrence(frequency: Frequency, start: DateString): Recurrence {
  return {
    frequency,
    interval: 1,
    weekdays: frequency === 'weekly' ? [weekdayOf(start)] : [],
    monthly: frequency === 'monthly' ? { type: 'dayOfMonth' } : null,
    end: { type: 'never' },
  }
}

const UNITS: Record<Frequency, [string, string]> = {
  daily: ['day', 'days'],
  weekly: ['week', 'weeks'],
  monthly: ['month', 'months'],
  yearly: ['year', 'years'],
}

export function intervalUnit(frequency: Frequency, interval: number): string {
  return UNITS[frequency][interval === 1 ? 0 : 1]
}

function list(items: string[]): string {
  return new Intl.ListFormat(getLocale(), { style: 'long', type: 'conjunction' }).format(items)
}

function longDate(date: DateString, withYear: boolean): string {
  return new Intl.DateTimeFormat(getLocale(), {
    month: 'long',
    day: 'numeric',
    year: withYear ? 'numeric' : undefined,
    timeZone: 'UTC',
  }).format(utc(date))
}

/**
 * A plain-language summary (FR-008), e.g. "Every 2 weeks on Tuesday and Thursday, 6 times".
 * Monthly and yearly days come from <paramref name="first"/>, the series' first occurrence.
 */
export function describeRule(recurrence: Recurrence, first: DateString): string {
  const { frequency, interval } = recurrence
  const adverb = { daily: 'Daily', weekly: 'Weekly', monthly: 'Monthly', yearly: 'Yearly' }[frequency]
  const every = interval === 1 ? adverb : `Every ${interval} ${intervalUnit(frequency, interval)}`

  let on = ''
  if (frequency === 'weekly') {
    const days = WEEKDAYS.filter((day) => recurrence.weekdays.includes(day)).map((day) => weekdayName(day))
    if (days.length > 0) on = ` on ${list(days)}`
  } else if (frequency === 'monthly') {
    const monthly = recurrence.monthly ?? { type: 'dayOfMonth' }
    on =
      monthly.type === 'dayOfMonth'
        ? ` on day ${dayNumber(first)}${dayNumber(first) > 28 ? ', or the last day of shorter months' : ''}`
        : ` on the ${ORDINAL_WORDS[monthly.ordinal]} ${weekdayName(weekdayOf(first))}`
  } else if (frequency === 'yearly') {
    on = ` on ${longDate(first, false)}${first.slice(5) === '02-29' ? ' (leap years only)' : ''}`
  }

  const end = recurrence.end
  const ending =
    end.type === 'count'
      ? end.count === 1
        ? ', once'
        : `, ${end.count} times`
      : end.type === 'until'
        ? `, until ${longDate(end.until, true)}`
        : ''

  return `${every}${on}${ending}`
}

/** The summary plus the adjusted first date when it differs from the start: "… Starts Thursday, October 8, 2026." */
export function describeRuleFrom(recurrence: Recurrence, start: DateString): string {
  const first = firstOccurrence(start, recurrence)
  const summary = describeRule(recurrence, first)
  return first === start ? summary : `${summary}. Starts ${formatFullDate(first)}.`
}
