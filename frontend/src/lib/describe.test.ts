import { describe, expect, it } from 'vitest'
import { recurringSummary, timedSummary } from '../test/fixtures'
import { describeEvent, eventKey } from './describe'

describe('describeEvent', () => {
  it('says that an occurrence repeats (FR-030)', () => {
    expect(describeEvent(recurringSummary(), 'America/Chicago')).toBe(
      'Gym, Wednesday, October 21, 2026, 7:00 AM to 8:00 AM, repeats',
    )
  })

  it('says nothing extra for a one-time event', () => {
    expect(describeEvent(timedSummary('e1', 'Dentist', '2026-10-14T09:00:00-05:00', '2026-10-14T10:00:00-05:00'), 'America/Chicago')).toBe(
      'Dentist, Wednesday, October 14, 2026, 9:00 AM to 10:00 AM',
    )
  })
})

describe('eventKey', () => {
  it('tells occurrences of one series apart', () => {
    expect(eventKey(recurringSummary({ occurrenceDate: '2026-10-21' }))).not.toBe(eventKey(recurringSummary({ occurrenceDate: '2026-10-23' })))
  })
})
