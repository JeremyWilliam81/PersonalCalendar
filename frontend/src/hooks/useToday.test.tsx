import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useToday } from './useToday'

// "Today" follows the clock and the device zone while the page stays open (FR-015, research V11).
describe('useToday', () => {
  let instant: Date
  let zone: string

  beforeEach(() => {
    vi.useFakeTimers()
    instant = new Date('2026-10-15T04:59:30Z') // 23:59:30 in Chicago
    zone = 'America/Chicago'
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  function render() {
    return renderHook(() => useToday({ now: () => instant, readZone: () => zone }))
  }

  it('starts with the zone, today and now', () => {
    const { result } = render()

    expect(result.current.timeZone).toBe('America/Chicago')
    expect(result.current.today).toBe('2026-10-14')
    expect(result.current.now).toEqual(instant)
  })

  it('moves to the next date within 30 seconds of midnight', () => {
    const { result } = render()

    instant = new Date('2026-10-15T05:00:05Z')
    act(() => vi.advanceTimersByTime(30_000))

    expect(result.current.today).toBe('2026-10-15')
  })

  it('re-checks immediately when the page becomes visible again', () => {
    const { result } = render()

    instant = new Date('2026-10-15T09:00:00Z')
    act(() => document.dispatchEvent(new Event('visibilitychange')))

    expect(result.current.today).toBe('2026-10-15')
  })

  it('follows a change of device time zone', () => {
    const { result } = render()

    zone = 'America/New_York'
    act(() => vi.advanceTimersByTime(30_000))

    expect(result.current.timeZone).toBe('America/New_York')
    expect(result.current.today).toBe('2026-10-15')
  })
})
