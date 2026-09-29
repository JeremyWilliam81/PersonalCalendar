import { describe, expect, it } from 'vitest'
import { addDays, addMonthsClamped, endOfWeek, isWithin, monthOf, startOfWeek } from './dates'

// Plain calendar-date arithmetic for grid keyboard navigation. No zones are involved.
describe('dates', () => {
  it('adds days across month, year and leap-day boundaries', () => {
    expect(addDays('2026-10-31', 1)).toBe('2026-11-01')
    expect(addDays('2026-01-01', -1)).toBe('2025-12-31')
    expect(addDays('2028-02-28', 1)).toBe('2028-02-29')
    expect(addDays('2026-10-14', 7)).toBe('2026-10-21')
  })

  it('adds months keeping the day number, clamped to the last day of the month', () => {
    expect(addMonthsClamped('2026-10-14', -1)).toBe('2026-09-14')
    expect(addMonthsClamped('2026-10-31', -1)).toBe('2026-09-30')
    expect(addMonthsClamped('2028-01-31', 1)).toBe('2028-02-29')
    expect(addMonthsClamped('2026-12-15', 1)).toBe('2027-01-15')
  })

  it('finds the Sunday-to-Saturday week around a date', () => {
    expect(startOfWeek('2026-10-14')).toBe('2026-10-11')
    expect(endOfWeek('2026-10-14')).toBe('2026-10-17')
    expect(startOfWeek('2026-10-11')).toBe('2026-10-11')
  })

  it('returns the year and month of a date', () => {
    expect(monthOf('2026-10-14')).toEqual({ year: 2026, month: 10 })
  })

  it('checks whether a date is inside an inclusive range', () => {
    expect(isWithin('2026-10-14', '2026-09-27', '2026-10-31')).toBe(true)
    expect(isWithin('2026-11-01', '2026-09-27', '2026-10-31')).toBe(false)
  })
})
