import { describe, expect, it } from 'vitest'
import { todayIn } from './today'

// "Today" is the calendar date of an injected instant in an explicit zone (research V2, FR-015).
describe('todayIn', () => {
  it('reads the date in the given zone, not the machine zone', () => {
    const instant = new Date('2026-10-15T04:30:00Z')
    expect(todayIn('America/Chicago', instant)).toBe('2026-10-14')
    expect(todayIn('Asia/Kolkata', instant)).toBe('2026-10-15')
  })

  it('handles a half-hour offset zone exactly at its midnight', () => {
    expect(todayIn('Australia/Adelaide', new Date('2026-10-14T13:29:00Z'))).toBe('2026-10-14')
    expect(todayIn('Australia/Adelaide', new Date('2026-10-14T13:30:00Z'))).toBe('2026-10-15')
  })

  it('is correct on a fall-back day', () => {
    expect(todayIn('America/Chicago', new Date('2026-11-01T05:30:00Z'))).toBe('2026-11-01')
  })
})
