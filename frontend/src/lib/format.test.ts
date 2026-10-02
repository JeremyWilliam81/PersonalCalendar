import { describe, expect, it } from 'vitest'
import {
  formatAllDayRange,
  formatFullDate,
  formatLocalTime,
  formatMonthTitle,
  formatTimeShort,
  formatTimedRange,
  formatWeekdayName,
  formatHourLabel,
  formatPeriodTitle,
  localDateInZone,
  toLocalInputValue,
} from './format'
import { setLocale } from './locale'

// Locale is pinned to en-US in src/test/setup.ts; zones are always explicit.
describe('format', () => {
  it('formats a short time in the given zone', () => {
    expect(formatTimeShort('2026-10-14T09:00:00-05:00', 'America/Chicago')).toBe('9:00 AM')
    expect(formatTimeShort('2026-10-14T09:00:00-05:00', 'Asia/Kolkata')).toBe('7:30 PM')
  })

  it('formats a full date without shifting it', () => {
    expect(formatFullDate('2026-10-14')).toBe('Wednesday, October 14, 2026')
    expect(formatFullDate('2028-02-29')).toBe('Tuesday, February 29, 2028')
  })

  it('formats a timed range on one day', () => {
    expect(formatTimedRange('2026-10-14T09:00:00-05:00', '2026-10-14T10:00:00-05:00', 'America/Chicago')).toBe(
      'Wednesday, October 14, 2026, 9:00 AM to 10:00 AM',
    )
  })

  it('writes the end date in full when a timed range crosses midnight', () => {
    expect(formatTimedRange('2026-10-14T22:00:00-05:00', '2026-10-15T01:00:00-05:00', 'America/Chicago')).toBe(
      'Wednesday, October 14, 2026, 10:00 PM to Thursday, October 15, 2026, 1:00 AM',
    )
  })

  it('formats an all-day range for one day and several days', () => {
    expect(formatAllDayRange('2026-10-14', '2026-10-14')).toBe('Wednesday, October 14, 2026')
    expect(formatAllDayRange('2026-10-12', '2026-10-16')).toBe('Monday, October 12 to Friday, October 16, 2026')
    expect(formatAllDayRange('2026-12-30', '2027-01-02')).toBe(
      'Wednesday, December 30, 2026 to Saturday, January 2, 2027',
    )
  })

  it('formats the month title', () => {
    expect(formatMonthTitle(2026, 10)).toBe('October 2026')
  })

  it('formats weekday names', () => {
    expect(formatWeekdayName(0, 'long')).toBe('Sunday')
    expect(formatWeekdayName(6, 'short')).toBe('Sat')
  })

  it('finds the local date of an instant in a zone', () => {
    expect(localDateInZone('2026-10-14T20:00:00Z', 'Asia/Kolkata')).toBe('2026-10-15')
    expect(localDateInZone('2026-10-14T20:00:00Z', 'America/Chicago')).toBe('2026-10-14')
  })

  it('formats a wall-clock local time without applying any zone', () => {
    expect(formatLocalTime('2027-03-14T02:30')).toBe('2:30 AM')
    expect(formatLocalTime('2027-03-14T15:05')).toBe('3:05 PM')
  })

  it('converts an instant to a datetime-local value in a zone', () => {
    expect(toLocalInputValue('2026-10-14T09:00:00-05:00', 'America/Chicago')).toBe('2026-10-14T09:00')
    expect(toLocalInputValue('2026-10-14T09:00:00-05:00', 'America/New_York')).toBe('2026-10-14T10:00')
    expect(toLocalInputValue('2026-10-14T18:30:00Z', 'Asia/Kolkata')).toBe('2026-10-15T00:00')
  })
})

// The header's period title (FR-007, contracts/ui-interaction "Shared header"). Weeks use full month names.
describe('formatPeriodTitle', () => {
  it('uses the full date for a day', () => {
    expect(formatPeriodTitle('day', '2026-10-14')).toBe('Wednesday, October 14, 2026')
  })

  it('uses a Sunday-to-Saturday range for a week, naming both months and years when crossed', () => {
    expect(formatPeriodTitle('week', '2026-10-14')).toBe('October 11 – 17, 2026')
    expect(formatPeriodTitle('week', '2026-10-28')).toBe('October 25 – 31, 2026')
    expect(formatPeriodTitle('week', '2026-09-30')).toBe('September 27 – October 3, 2026')
    expect(formatPeriodTitle('week', '2026-11-01')).toBe('November 1 – 7, 2026')
    expect(formatPeriodTitle('week', '2026-12-30')).toBe('December 27, 2026 – January 2, 2027')
    expect(formatPeriodTitle('week', '2027-03-03')).toBe('February 28 – March 6, 2027')
  })

  it('uses the month and year for a month', () => {
    expect(formatPeriodTitle('month', '2026-10-14')).toBe('October 2026')
  })
})

describe('formatHourLabel', () => {
  it('formats a wall-clock hour label in the device locale', () => {
    expect(formatHourLabel('01:00')).toBe('1 AM')
    expect(formatHourLabel('13:00')).toBe('1 PM')
  })

  it('follows a 24-hour locale (FR-027)', () => {
    setLocale('en-GB')
    try {
      expect(formatHourLabel('01:00')).toBe('01')
      expect(formatHourLabel('13:00')).toBe('13')
    } finally {
      setLocale('en-US')
    }
  })
})
