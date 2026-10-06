import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import type { EventSummary, MonthView as MonthViewData } from '../api/types'
import { allDaySummary, octoberMonth, recurringSummary, timedSummary } from '../test/fixtures'
import { setNarrowViewport } from '../test/viewport'
import { MonthView } from './MonthView'

const dentist = timedSummary('e1', 'Dentist', '2026-10-14T09:00:00-05:00', '2026-10-14T10:00:00-05:00')

interface HarnessProps {
  month?: MonthViewData
  onMoveFocus?: (date: string, changeMonth: boolean) => void
  onOpenDay?: (date: string) => void
  onOpenEvent?: (event: EventSummary, date: string) => void
  maxVisible?: number
}

/** Holds the focused date like the real container does. */
function Harness({ month = octoberMonth({ '2026-10-14': [dentist] }), ...props }: HarnessProps) {
  const [focusedDate, setFocusedDate] = useState('2026-10-14')
  return (
    <>
      <h2 id="period-title">October 2026</h2>
      <MonthView
        month={month}
        focusedDate={focusedDate}
        titleId="period-title"
        onMoveFocus={(date, changeMonth) => {
          props.onMoveFocus?.(date, changeMonth)
          if (!changeMonth) setFocusedDate(date)
        }}
        onOpenDay={props.onOpenDay ?? vi.fn()}
        onOpenEvent={props.onOpenEvent ?? vi.fn()}
        maxVisible={props.maxVisible}
      />
    </>
  )
}

function cell(name: string) {
  // Day cells read the full date, "today" where it applies, and what tapping does (FR-013).
  return screen.getByRole('gridcell', {
    name: (n) => n === `${name}, open in day view` || n === `${name}, today, open in day view`,
  })
}

describe('MonthView', () => {
  it('renders a labelled grid with Sunday-first weekday headers', () => {
    render(<Harness />)

    expect(screen.getByRole('grid', { name: 'October 2026' })).toBeInTheDocument()
    const headers = screen.getAllByRole('columnheader').map((h) => h.getAttribute('aria-label'))
    expect(headers).toEqual(['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'])
  })

  it('makes exactly one day cell tabbable (roving tabindex)', () => {
    render(<Harness />)

    const tabbable = screen.getAllByRole('gridcell').filter((c) => c.tabIndex === 0)
    expect(tabbable).toHaveLength(1)
    expect(tabbable[0]).toHaveAccessibleName('Wednesday, October 14, 2026, open in day view')
  })

  it('moves focus with arrow, Home and End keys', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    cell('Wednesday, October 14, 2026').focus()

    await user.keyboard('{ArrowRight}')
    expect(cell('Thursday, October 15, 2026')).toHaveFocus()
    await user.keyboard('{ArrowDown}')
    expect(cell('Thursday, October 22, 2026')).toHaveFocus()
    await user.keyboard('{ArrowUp}{ArrowLeft}')
    expect(cell('Wednesday, October 14, 2026')).toHaveFocus()
    await user.keyboard('{Home}')
    expect(cell('Sunday, October 11, 2026')).toHaveFocus()
    await user.keyboard('{End}')
    expect(cell('Saturday, October 17, 2026')).toHaveFocus()
    expect(cell('Saturday, October 17, 2026').tabIndex).toBe(0)
  })

  it('changes month with Page Up / Page Down and when moving past the grid edge', async () => {
    const user = userEvent.setup()
    const onMoveFocus = vi.fn()
    render(<Harness onMoveFocus={onMoveFocus} />)
    cell('Wednesday, October 14, 2026').focus()

    await user.keyboard('{PageDown}')
    expect(onMoveFocus).toHaveBeenLastCalledWith('2026-11-14', true)
    await user.keyboard('{PageUp}')
    expect(onMoveFocus).toHaveBeenLastCalledWith('2026-09-14', true)

    await user.keyboard('{End}{ArrowDown}{ArrowDown}{ArrowDown}')
    expect(onMoveFocus).toHaveBeenLastCalledWith('2026-11-07', true)
  })

  it('opens the focused day in the day view with Enter or Space', async () => {
    const user = userEvent.setup()
    const onOpenDay = vi.fn()
    render(<Harness onOpenDay={onOpenDay} />)
    cell('Wednesday, October 14, 2026').focus()

    await user.keyboard('{Enter}')
    expect(onOpenDay).toHaveBeenLastCalledWith('2026-10-14')
    await user.keyboard('{ArrowRight} ')
    expect(onOpenDay).toHaveBeenLastCalledWith('2026-10-15')
  })

  it('opens a day when its number or empty space is tapped, but not when one of its events is (FR-013)', async () => {
    const user = userEvent.setup()
    const onOpenDay = vi.fn()
    const onOpenEvent = vi.fn()
    render(<Harness onOpenDay={onOpenDay} onOpenEvent={onOpenEvent} />)

    await user.click(cell('Wednesday, October 14, 2026'))
    expect(onOpenDay).toHaveBeenLastCalledWith('2026-10-14')

    await user.click(within(cell('Wednesday, October 14, 2026')).getByText('14'))
    expect(onOpenDay).toHaveBeenCalledTimes(2)

    await user.click(screen.getByRole('button', { name: /^Dentist,/ }))
    expect(onOpenEvent).toHaveBeenCalledOnce()
    expect(onOpenDay).toHaveBeenCalledTimes(2)

    await user.click(cell('Wednesday, September 30, 2026'))
    expect(onOpenDay).toHaveBeenLastCalledWith('2026-09-30')
  })

  it('shows events as buttons with the full date and time in their accessible name', async () => {
    const user = userEvent.setup()
    const onOpenEvent = vi.fn()
    render(<Harness onOpenEvent={onOpenEvent} />)

    const button = screen.getByRole('button', { name: 'Dentist, Wednesday, October 14, 2026, 9:00 AM to 10:00 AM' })
    expect(button).toHaveTextContent('9:00 AM')
    expect(button).toHaveTextContent('Dentist')
    await user.click(button)

    expect(onOpenEvent).toHaveBeenCalledWith(expect.objectContaining({ id: 'e1' }), '2026-10-14')
  })

  it('lets Tab move from the focused day into its event buttons', async () => {
    const user = userEvent.setup()
    render(<Harness />)
    cell('Wednesday, October 14, 2026').focus()

    await user.tab()

    expect(screen.getByRole('button', { name: /^Dentist,/ })).toHaveFocus()
  })

  it('shows an "N more" button that lists every event of a crowded day', async () => {
    const user = userEvent.setup()
    const many = Array.from({ length: 6 }, (_, i) =>
      timedSummary(`e${i}`, `Event ${i}`, `2026-10-14T0${i + 1}:00:00-05:00`, `2026-10-14T0${i + 1}:30:00-05:00`),
    )
    render(<Harness month={octoberMonth({ '2026-10-14': many })} maxVisible={3} />)

    const dayCell = cell('Wednesday, October 14, 2026')
    expect(within(dayCell).getAllByRole('button', { name: /^Event/ })).toHaveLength(3)
    await user.click(screen.getByRole('button', { name: '3 more events on Wednesday, October 14, 2026' }))

    const dialog = screen.getByRole('dialog', { name: 'Wednesday, October 14, 2026' })
    expect(within(dialog).getAllByRole('button', { name: /^Event/ })).toHaveLength(6)
  })

  it('shows all-day events without a time, in the all-day style, with the date range in their name', () => {
    const vacation = allDaySummary('v1', 'Vacation', '2026-10-12', '2026-10-16')
    render(<Harness month={octoberMonth({ '2026-10-14': [vacation, dentist] })} />)

    const button = screen.getByRole('button', {
      name: 'Vacation, all day, Monday, October 12 to Friday, October 16, 2026',
    })
    expect(button).toHaveClass('all-day')
    expect(button).toHaveTextContent(/^Vacation$/)
  })

  it('marks days outside the month and today', () => {
    render(<Harness />)

    expect(cell('Wednesday, September 30, 2026')).toHaveAttribute('data-outside-month', 'true')
    expect(cell('Tuesday, September 29, 2026')).toHaveAttribute('aria-current', 'date')
  })

  it('has no axe violations', async () => {
    const { container } = render(<Harness />)

    const results = await axe(container)

    expect(results.violations).toEqual([])
  })
})

// Phone-sized month cells show markers instead of event titles (FR-003a).
describe('MonthView on a phone-sized screen', () => {
  const five = [
    allDaySummary('a1', 'Trip', '2026-10-14', '2026-10-15'),
    ...[9, 10, 11, 12].map((h) =>
      timedSummary(`t${h}`, `Event ${h}`, `2026-10-14T${h}:00:00-05:00`, `2026-10-14T${h}:30:00-05:00`),
    ),
  ]

  it('shows up to three markers and a "+N" count, with the count in the day name', () => {
    setNarrowViewport(true)
    render(<Harness month={octoberMonth({ '2026-10-14': five })} />)

    const day = screen.getByRole('gridcell', { name: 'Wednesday, October 14, 2026, 5 events, open in day view' })
    const markers = day.querySelectorAll('.marker')
    expect(markers).toHaveLength(3)
    expect(markers[0]).toHaveAttribute('data-kind', 'all-day')
    expect(markers[1]).toHaveAttribute('data-kind', 'timed')
    expect(day).toHaveTextContent('+2')
    expect(within(day).queryByRole('button')).not.toBeInTheDocument()
    expect(screen.getByRole('gridcell', { name: 'Thursday, October 1, 2026, no events, open in day view' })).toBeInTheDocument()
  })

  it('opens the day when a marker is tapped', async () => {
    const user = userEvent.setup()
    setNarrowViewport(true)
    const onOpenDay = vi.fn()
    render(<Harness month={octoberMonth({ '2026-10-14': five })} onOpenDay={onOpenDay} />)

    const day = screen.getByRole('gridcell', { name: /^Wednesday, October 14, 2026, 5 events/ })
    await user.click(day.querySelector('.marker')!)

    expect(onOpenDay).toHaveBeenCalledWith('2026-10-14')
  })

  it('has no axe violations', async () => {
    setNarrowViewport(true)
    const { container } = render(<Harness month={octoberMonth({ '2026-10-14': five })} />)

    expect((await axe(container)).violations).toEqual([])
  })

  it('marks an occurrence of a series with the repeat icon and says "repeats" (003 FR-011)', () => {
    setNarrowViewport(false)
    const gym = recurringSummary({ start: '2026-10-14T07:00:00-05:00', end: '2026-10-14T08:00:00-05:00', occurrenceDate: '2026-10-14' })
    render(<Harness month={octoberMonth({ '2026-10-14': [gym, dentist] })} />)

    const label = screen.getByRole('button', { name: /^Gym,/ })
    expect(label).toHaveAccessibleName('Gym, Wednesday, October 14, 2026, 7:00 AM to 8:00 AM, repeats')
    expect(label.querySelector('svg.repeat-icon')).not.toBeNull()
    expect(screen.getByRole('button', { name: /^Dentist,/ }).querySelector('svg.repeat-icon')).toBeNull()
  })

  it('counts occurrences in the phone markers without showing the icon (003 US2 scenario 4)', () => {
    setNarrowViewport(true)
    const gym = recurringSummary({ occurrenceDate: '2026-10-14' })
    const { container } = render(<Harness month={octoberMonth({ '2026-10-14': [gym, dentist] })} />)

    expect(cell('Wednesday, October 14, 2026, 2 events')).toBeInTheDocument()
    expect(container.querySelector('svg.repeat-icon')).toBeNull()
  })
})
