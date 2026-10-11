import { describe, expect, it } from 'vitest'
import { allDaySummary, recurringSummary, timedSummary } from '../test/fixtures'
import { continuesFromPreviousDay, describeEvent, eventKey } from './describe'

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

describe('continuesFromPreviousDay (003 FR-032)', () => {
  const evening = timedSummary('e1', 'Party', '2026-10-08T18:00:00-05:00', '2026-10-09T02:00:00-05:00')

  it('is false on the day the event starts and true on the days after', () => {
    expect(continuesFromPreviousDay(evening, '2026-10-08', 'America/Chicago')).toBe(false)
    expect(continuesFromPreviousDay(evening, '2026-10-09', 'America/Chicago')).toBe(true)
  })

  it('reads the start date in the view zone', () => {
    // 6:00 PM in Chicago is 4:30 AM on the 9th in Kolkata, so the event starts on the 9th there.
    expect(continuesFromPreviousDay(evening, '2026-10-09', 'Asia/Kolkata')).toBe(false)
  })

  it('is false for all-day events', () => {
    expect(continuesFromPreviousDay(allDaySummary('a1', 'Trip', '2026-10-16', '2026-10-20'), '2026-10-17', 'America/Chicago')).toBe(false)
  })
})
