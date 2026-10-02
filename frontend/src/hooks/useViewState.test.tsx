import { act, renderHook } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { useViewState } from './useViewState'

// History handling for the view state (research V1, data-model "State transitions").
describe('useViewState', () => {
  it('starts from the address and pushes or replaces history entries', () => {
    window.history.replaceState(null, '', '/week/2026-10-14')
    const start = window.history.length
    const { result } = renderHook(() => useViewState('2026-10-14'))
    expect(result.current.state).toEqual({ view: 'week', date: '2026-10-14' })

    act(() => result.current.setState({ view: 'week', date: '2026-10-21' }, 'push'))
    expect(window.location.pathname).toBe('/week/2026-10-21')
    expect(window.history.length).toBe(start + 1)

    act(() => result.current.setState({ view: 'week', date: '2026-10-22' }, 'replace'))
    expect(window.location.pathname).toBe('/week/2026-10-22')
    expect(window.history.length).toBe(start + 1)
  })

  it('does not push an entry when the state is unchanged', () => {
    window.history.replaceState(null, '', '/day/2026-10-14')
    const start = window.history.length
    const { result } = renderHook(() => useViewState('2026-10-14'))

    act(() => result.current.setState({ view: 'day', date: '2026-10-14' }, 'push'))

    expect(window.history.length).toBe(start)
  })

  it('follows Back and Forward', () => {
    window.history.replaceState(null, '', '/day/2026-10-14')
    const { result } = renderHook(() => useViewState('2026-10-14'))

    act(() => {
      window.history.replaceState(null, '', '/month/2026-12-01')
      window.dispatchEvent(new PopStateEvent('popstate'))
    })

    expect(result.current.state).toEqual({ view: 'month', date: '2026-12-01' })
  })

  it('corrects an invalid address and reports it', () => {
    window.history.replaceState(null, '', '/nope')
    const { result } = renderHook(() => useViewState('2026-10-14'))

    expect(result.current.state).toEqual({ view: 'month', date: '2026-10-14' })
    expect(result.current.error).toBe('invalid-link')
    expect(window.location.pathname).toBe('/month/2026-10-14')

    act(() => result.current.clearError())
    expect(result.current.error).toBeNull()
  })
})
