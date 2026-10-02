import { fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import { CHICAGO, dentist, fallBackDay, october14, segment, standup, timelineDay } from '../test/fixtures'
import { TimeGrid } from './TimeGrid'

function block(name: RegExp) {
  return screen.getByRole('button', { name })
}

describe('TimeGrid', () => {
  it('places events by elapsed minutes at 0.8 px per minute, side by side when they overlap', () => {
    render(<TimeGrid days={october14().days} timeZone={CHICAGO} onOpenEvent={vi.fn()} />)

    const dentistBlock = block(/^Dentist,/)
    expect(dentistBlock.style.top).toBe('432px')
    expect(dentistBlock.style.height).toBe('72px')
    expect(parseFloat(dentistBlock.style.left)).toBeCloseTo(0)

    const lunchBlock = block(/^Lunch,/)
    expect(parseFloat(lunchBlock.style.left)).toBeCloseTo(33.333, 2)
    expect(parseFloat(lunchBlock.style.width)).toBeCloseTo(33.333, 2)
  })

  it('keeps very short events at least 24 px tall', () => {
    render(<TimeGrid days={october14().days} timeZone={CHICAGO} onOpenEvent={vi.fn()} />)

    expect(block(/^Standup,/).style.height).toBe('24px')
  })

  it('names each event with its full date and time range, and marks clipped segments', () => {
    render(<TimeGrid days={october14().days} timeZone={CHICAGO} onOpenEvent={vi.fn()} />)

    expect(screen.getByRole('button', { name: 'Dentist, Wednesday, October 14, 2026, 9:00 AM to 10:30 AM' })).toBeInTheDocument()
    expect(block(/^Late show,/)).toHaveAttribute('data-continues-after', 'true')
  })

  it('opens an event when its block is tapped', async () => {
    const user = userEvent.setup()
    const onOpenEvent = vi.fn()
    render(<TimeGrid days={october14().days} timeZone={CHICAGO} onOpenEvent={onOpenEvent} />)

    await user.click(block(/^Dentist,/))

    expect(onOpenEvent).toHaveBeenCalledWith('e1', '2026-10-14')
  })

  it('labels a 25-hour fall-back day with the 1 AM hour twice and a taller column', () => {
    const { container } = render(<TimeGrid days={fallBackDay().days} timeZone={CHICAGO} onOpenEvent={vi.fn()} />)

    expect(screen.getAllByText('1 AM')).toHaveLength(2)
    expect(container.querySelector<HTMLElement>('.time-column')!.style.height).toBe('1200px')
  })

  it('offers a full-size list for clusters of three or more overlapping events', async () => {
    const user = userEvent.setup()
    render(<TimeGrid days={october14().days} timeZone={CHICAGO} onOpenEvent={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: '3 events between 9:00 AM and 11:00 AM on Wednesday, October 14, 2026' }))

    const dialog = screen.getByRole('dialog', { name: 'Wednesday, October 14, 2026' })
    expect(within(dialog).getAllByRole('button', { name: /^(Dentist|Lunch|Standup),/ })).toHaveLength(3)
  })

  it('shows no cluster chip for two overlapping events', () => {
    const day = timelineDay('2026-10-14', {
      timed: [segment(dentist, 540, 90, { columnCount: 2 }), segment(standup, 570, 15, { column: 1, columnCount: 2 })],
    })
    render(<TimeGrid days={[day]} timeZone={CHICAGO} onOpenEvent={vi.fn()} />)

    expect(screen.queryByRole('button', { name: /events between/ })).not.toBeInTheDocument()
  })

  it('scrolls to 8 AM when the day has no timed events', () => {
    const { container } = render(<TimeGrid days={[timelineDay('2026-10-20')]} timeZone={CHICAGO} onOpenEvent={vi.fn()} />)

    expect(container.querySelector('.time-scroll')!.scrollTop).toBe(8 * 48 - 16)
  })

  it('scrolls to the earliest timed event', () => {
    const { container } = render(<TimeGrid days={october14().days} timeZone={CHICAGO} onOpenEvent={vi.fn()} />)

    expect(container.querySelector('.time-scroll')!.scrollTop).toBe(432 - 16)
  })

  it('has no axe violations', async () => {
    const { container } = render(<TimeGrid days={october14().days} timeZone={CHICAGO} onOpenEvent={vi.fn()} />)

    expect((await axe(container)).violations).toEqual([])
  })
})

// Tapping empty time starts an event at that half-hour (FR-013b, research V9).
describe('TimeGrid empty-time taps', () => {
  function background(container: HTMLElement) {
    return container.querySelector<HTMLElement>('.time-slots')!
  }

  it('starts an event at the 30-minute slot under the tap', () => {
    const onCreateAt = vi.fn()
    const { container } = render(
      <TimeGrid days={october14().days} timeZone={CHICAGO} onOpenEvent={vi.fn()} onCreateAt={onCreateAt} />,
    )

    fireEvent.click(background(container), { clientY: 14 * 24 + 5 })

    expect(onCreateAt).toHaveBeenCalledWith('2026-10-14T07:00')
  })

  it('counts slots from the real start of a fall-back day', () => {
    const onCreateAt = vi.fn()
    const { container } = render(
      <TimeGrid days={fallBackDay().days} timeZone={CHICAGO} onOpenEvent={vi.fn()} onCreateAt={onCreateAt} />,
    )

    for (const slot of [2, 4, 6]) fireEvent.click(background(container), { clientY: slot * 24 + 1 })

    expect(onCreateAt.mock.calls.map((c) => c[0])).toEqual(['2026-11-01T01:00', '2026-11-01T01:00', '2026-11-01T02:00'])
  })

  it('keeps the background out of the accessibility tree and the Tab order', () => {
    const { container } = render(
      <TimeGrid days={october14().days} timeZone={CHICAGO} onOpenEvent={vi.fn()} onCreateAt={vi.fn()} />,
    )

    expect(background(container)).toHaveAttribute('aria-hidden', 'true')
    expect(background(container)).not.toHaveAttribute('tabindex')
  })

  it('does not start an event when an event block is tapped', async () => {
    const user = userEvent.setup()
    const onCreateAt = vi.fn()
    render(<TimeGrid days={october14().days} timeZone={CHICAGO} onOpenEvent={vi.fn()} onCreateAt={onCreateAt} />)

    await user.click(screen.getByRole('button', { name: /^Dentist,/ }))

    expect(onCreateAt).not.toHaveBeenCalled()
  })
})
