import { describe, expect, it } from 'vitest'
import type { Recurrence } from '../api/types'
import { defaultRecurrence, describeRule, firstOccurrence, monthlyOptions, weekdayOf } from './recurrence'

// Same dates as backend/tests/PersonalCalendar.Domain.Tests/RepeatRuleTests.cs and RecurrenceExpandTests.cs.

function rule(overrides: Partial<Recurrence>): Recurrence {
  return { frequency: 'daily', interval: 1, weekdays: [], monthly: null, end: { type: 'never' }, ...overrides }
}

describe('monthlyOptions', () => {
  it('offers the day number and the weekday position', () => {
    expect(monthlyOptions('2026-10-14')).toEqual([
      { type: 'dayOfMonth', label: 'On day 14' },
      { type: 'weekdayPosition', ordinal: 2, label: 'On the second Wednesday' },
    ])
  })

  it('offers only "last" for a fifth weekday', () => {
    expect(monthlyOptions('2026-10-30')).toEqual([
      { type: 'dayOfMonth', label: 'On day 30' },
      { type: 'weekdayPosition', ordinal: -1, label: 'On the last Friday' },
    ])
  })

  it('offers both the fourth and the last weekday in the month’s last week', () => {
    expect(monthlyOptions('2026-10-28')).toEqual([
      { type: 'dayOfMonth', label: 'On day 28' },
      { type: 'weekdayPosition', ordinal: 4, label: 'On the fourth Wednesday' },
      { type: 'weekdayPosition', ordinal: -1, label: 'On the last Wednesday' },
    ])
  })
})

describe('firstOccurrence', () => {
  it('moves a weekly start to the first chosen weekday', () => {
    expect(firstOccurrence('2026-10-06', rule({ frequency: 'weekly', weekdays: ['thursday'] }))).toBe('2026-10-08')
  })

  it('keeps a weekly start that is a chosen weekday', () => {
    expect(firstOccurrence('2026-10-06', rule({ frequency: 'weekly', weekdays: ['tuesday', 'thursday'] }))).toBe('2026-10-06')
  })

  it.each(['daily', 'monthly', 'yearly'] as const)('keeps the start for %s', (frequency) => {
    expect(firstOccurrence('2026-10-06', rule({ frequency }))).toBe('2026-10-06')
  })
})

describe('describeRule', () => {
  it.each<[string, Recurrence, string]>([
    ['daily', rule({}), 'Daily'],
    ['every 3 days', rule({ interval: 3 }), 'Every 3 days'],
    [
      'weekly on three days',
      rule({ frequency: 'weekly', weekdays: ['friday', 'monday', 'wednesday'] }),
      'Weekly on Monday, Wednesday, and Friday',
    ],
    [
      'every 2 weeks with a count',
      rule({ frequency: 'weekly', interval: 2, weekdays: ['tuesday', 'thursday'], end: { type: 'count', count: 6 } }),
      'Every 2 weeks on Tuesday and Thursday, 6 times',
    ],
    ['once', rule({ end: { type: 'count', count: 1 } }), 'Daily, once'],
  ])('%s', (_name, recurrence, expected) => {
    expect(describeRule(recurrence, '2026-10-06')).toBe(expected)
  })

  it('mentions the last day of shorter months for days 29–31', () => {
    expect(describeRule(rule({ frequency: 'monthly', monthly: { type: 'dayOfMonth' } }), '2027-01-31')).toBe(
      'Monthly on day 31, or the last day of shorter months',
    )
  })

  it('names the last weekday and the end date', () => {
    expect(
      describeRule(
        rule({ frequency: 'monthly', monthly: { type: 'weekdayPosition', ordinal: -1 }, end: { type: 'until', until: '2026-12-31' } }),
        '2026-10-30',
      ),
    ).toBe('Monthly on the last Friday, until December 31, 2026')
  })

  it('every 3 months on day 15', () => {
    expect(describeRule(rule({ frequency: 'monthly', interval: 3, monthly: { type: 'dayOfMonth' } }), '2027-01-15')).toBe(
      'Every 3 months on day 15',
    )
  })

  it('yearly on February 29 only in leap years', () => {
    expect(describeRule(rule({ frequency: 'yearly' }), '2028-02-29')).toBe('Yearly on February 29 (leap years only)')
  })

  it('yearly on another day', () => {
    expect(describeRule(rule({ frequency: 'yearly', interval: 2 }), '2027-03-14')).toBe('Every 2 years on March 14')
  })
})

describe('defaultRecurrence', () => {
  it('preselects the start weekday for weekly', () => {
    expect(defaultRecurrence('weekly', '2026-10-06')).toEqual(rule({ frequency: 'weekly', weekdays: ['tuesday'] }))
  })

  it('preselects the day number for monthly', () => {
    expect(defaultRecurrence('monthly', '2026-10-06')).toEqual(rule({ frequency: 'monthly', monthly: { type: 'dayOfMonth' } }))
  })

  it('names the weekday of a date', () => {
    expect(weekdayOf('2026-10-11')).toBe('sunday')
  })
})
