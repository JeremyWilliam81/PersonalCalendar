import { describe, expect, it } from 'vitest'
import { parsePath, periodOf, stepPeriod, toPath } from './viewState'

// Zone-free calendar-date arithmetic for the three views (research V2, data-model "ViewState").
describe('periodOf', () => {
  it('is the date itself for the day view', () => {
    expect(periodOf({ view: 'day', date: '2026-10-14' })).toEqual({ first: '2026-10-14', last: '2026-10-14' })
  })

  it('is the Sunday-to-Saturday week for the week view, also across years', () => {
    expect(periodOf({ view: 'week', date: '2026-10-14' })).toEqual({ first: '2026-10-11', last: '2026-10-17' })
    expect(periodOf({ view: 'week', date: '2026-12-30' })).toEqual({ first: '2026-12-27', last: '2027-01-02' })
  })

  it('is the whole month for the month view, including leap February', () => {
    expect(periodOf({ view: 'month', date: '2028-02-10' })).toEqual({ first: '2028-02-01', last: '2028-02-29' })
  })
})

describe('stepPeriod', () => {
  it('moves by one day, one week, or one clamped month (FR-009)', () => {
    expect(stepPeriod({ view: 'day', date: '2028-02-28' }, 1)).toEqual({ view: 'day', date: '2028-02-29' })
    expect(stepPeriod({ view: 'week', date: '2026-12-30' }, 1)).toEqual({ view: 'week', date: '2027-01-06' })
    expect(stepPeriod({ view: 'month', date: '2026-01-31' }, 1)).toEqual({ view: 'month', date: '2026-02-28' })
    expect(stepPeriod({ view: 'month', date: '2026-03-31' }, -1)).toEqual({ view: 'month', date: '2026-02-28' })
    expect(stepPeriod({ view: 'month', date: '2026-03-31' }, 1)).toEqual({ view: 'month', date: '2026-04-30' })
  })

  it('returns null when the new selected date would leave 1900-01-01 .. 2199-12-31 (FR-012)', () => {
    expect(stepPeriod({ view: 'day', date: '1900-01-01' }, -1)).toBeNull()
    expect(stepPeriod({ view: 'week', date: '2199-12-30' }, 1)).toBeNull()
    expect(stepPeriod({ view: 'month', date: '2199-12-15' }, 1)).toBeNull()
  })

  it('allows stepping while the new selected date is in range, even if its week shows days before 1900', () => {
    expect(stepPeriod({ view: 'week', date: '1900-01-10' }, -1)).toEqual({ view: 'week', date: '1900-01-03' })
    expect(stepPeriod({ view: 'week', date: '1900-01-03' }, -1)).toBeNull()
  })
})

// The address names the view and selected date: /{view}/{yyyy-MM-dd} (FR-017 – FR-022, research V1).
describe('toPath and parsePath', () => {
  const today = '2026-10-14'

  it('formats the canonical address', () => {
    expect(toPath({ view: 'week', date: '2026-10-14' })).toBe('/week/2026-10-14')
  })

  it('reads valid addresses, with today filling in a missing date', () => {
    expect(parsePath('/', today)).toEqual({ state: { view: 'month', date: today } })
    expect(parsePath('/week', today)).toEqual({ state: { view: 'week', date: today } })
    expect(parsePath('/week/', today)).toEqual({ state: { view: 'week', date: today } })
    expect(parsePath('/day/2027-03-03', today)).toEqual({ state: { view: 'day', date: '2027-03-03' } })
    expect(parsePath('/month/2028-02-29', today)).toEqual({ state: { view: 'month', date: '2028-02-29' } })
  })

  it('falls back to this month and reports an invalid link for anything else', () => {
    for (const path of [
      '/Week/2026-10-14',
      '/fortnight/2026-10-14',
      '/day/2026-02-30',
      '/day/2027-02-29',
      '/day/1899-12-31',
      '/day/2026-10-14/extra',
      '/day/20261014',
    ]) {
      expect(parsePath(path, today), path).toEqual({ state: { view: 'month', date: today }, error: 'invalid-link' })
    }
  })

  it('round-trips', () => {
    for (const state of [
      { view: 'day', date: '1900-01-01' },
      { view: 'week', date: '2199-12-31' },
      { view: 'month', date: '2028-02-29' },
    ] as const) {
      expect(parsePath(toPath(state), today).state).toEqual(state)
    }
  })
})
