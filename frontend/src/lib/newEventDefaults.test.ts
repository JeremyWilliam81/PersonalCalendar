import { describe, expect, it } from 'vitest'
import { newEventDefaults } from './newEventDefaults'

// TZ is fixed to America/Chicago in vitest.config.ts; `now` is always injected.
describe('newEventDefaults (FR-005)', () => {
  it('uses the next whole hour on the chosen day, lasting one hour', () => {
    expect(newEventDefaults('2026-10-20', new Date(2026, 9, 14, 14, 20))).toEqual({
      start: '2026-10-20T15:00',
      end: '2026-10-20T16:00',
    })
  })

  it('moves to the next hour when now is exactly on the hour', () => {
    expect(newEventDefaults('2026-10-20', new Date(2026, 9, 14, 14, 0)).start).toBe('2026-10-20T15:00')
  })

  it('wraps to midnight at the start of the chosen day late in the evening', () => {
    expect(newEventDefaults('2026-10-20', new Date(2026, 9, 14, 23, 10))).toEqual({
      start: '2026-10-20T00:00',
      end: '2026-10-20T01:00',
    })
  })

  it('ends on the next day when the default starts at 23:00', () => {
    expect(newEventDefaults('2026-10-31', new Date(2026, 9, 14, 22, 30))).toEqual({
      start: '2026-10-31T23:00',
      end: '2026-11-01T00:00',
    })
  })
})
