import { render } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { NowLine } from './NowLine'

// The current time on the time scale (FR-016), positioned by elapsed time, not the wall clock.
describe('NowLine', () => {
  it('sits at the elapsed minutes since the day started, also on a fall-back day', () => {
    // 01:30 CST on 2026-11-01 is 150 minutes after 00:00 CDT.
    const { container } = render(<NowLine dayStart="2026-11-01T00:00:00-05:00" now={new Date('2026-11-01T07:30:00Z')} />)

    const line = container.querySelector<HTMLElement>('.now-line')!
    expect(line.style.top).toBe('120px')
    expect(line).toHaveAttribute('aria-hidden', 'true')
  })
})
