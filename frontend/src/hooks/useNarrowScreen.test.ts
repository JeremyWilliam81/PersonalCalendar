import { act, renderHook } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { setNarrowViewport } from '../test/viewport'
import { useNarrowScreen } from './useNarrowScreen'

// Phone-sized layouts start at 599 px and below (research V6, FR-004a).
describe('useNarrowScreen', () => {
  it('follows the media query and updates without remounting', () => {
    setNarrowViewport(true)
    const { result } = renderHook(() => useNarrowScreen())
    expect(result.current).toBe(true)

    act(() => setNarrowViewport(false))
    expect(result.current).toBe(false)
  })
})
