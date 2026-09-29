import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import type { MonthView as MonthViewData } from '../api/types'
import { allDaySummary, octoberMonth, timedSummary } from '../test/fixtures'
import { MonthView } from './MonthView'

const dentist = timedSummary('e1', 'Dentist', '2026-10-14T09:00:00-05:00', '2026-10-14T10:00:00-05:00')

interface HarnessProps {
  month?: MonthViewData
  onMoveFocus?: (date: string, changeMonth: boolean) => void
  onCreate?: (date: string) => void
  onOpenEvent?: (id: string, date: string) => void
  maxVisible?: number
}

/** Holds the focused date like the real container does. */
function Harness({ month = octoberMonth({ '2026-10-14': [dentist] }), ...props }: HarnessProps) {
  const [focusedDate, setFocusedDate] = useState('2026-10-14')
  return (
    <MonthView
      month={month}
      focusedDate={focusedDate}
      onMoveFocus={(date, changeMonth) => {
        props.onMoveFocus?.(date, changeMonth)
        if (!changeMonth) setFocusedDate(date)
      }}
      onPreviousMonth={vi.fn()}
      onNextMonth={vi.fn()}
      onToday={vi.fn()}
      onCreate={props.onCreate ?? vi.fn()}
      onOpenEvent={props.onOpenEvent ?? vi.fn()}
      maxVisible={props.maxVisible}
    />
  )
}

function cell(name: string) {
  return screen.getByRole('gridcell', { name })
}

describe('MonthView', () => {
  it('renders a labelled grid with Sunday-first weekday headers', () => {
    render(<Harness />)

    expect(screen.getByRole('grid', { name: 'October 2026' })).toBeInTheDocument()
    const headers = screen.getAllByRole('columnheader').map((h) => h.getAttribute('aria-label'))
    expect(headers).toEqual(['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'])
  })

  it('has named month navigation buttons', () => {
    render(<Harness />)

    expect(screen.getByRole('button', { name: 'Previous month' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Today' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Next month' })).toBeInTheDocument()
  })

  it('makes exactly one day cell tabbable (roving tabindex)', () => {
    render(<Harness />)

    const tabbable = screen.getAllByRole('gridcell').filter((c) => c.tabIndex === 0)
    expect(tabbable).toHaveLength(1)
    expect(tabbable[0]).toHaveAccessibleName('Wednesday, October 14, 2026')
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

  it('starts creating an event on the focused day with Enter or Space', async () => {
    const user = userEvent.setup()
    const onCreate = vi.fn()
    render(<Harness onCreate={onCreate} />)
    cell('Wednesday, October 14, 2026').focus()

    await user.keyboard('{Enter}')
    expect(onCreate).toHaveBeenLastCalledWith('2026-10-14')
    await user.keyboard('{ArrowRight} ')
    expect(onCreate).toHaveBeenLastCalledWith('2026-10-15')
  })

  it('shows events as buttons with the full date and time in their accessible name', async () => {
    const user = userEvent.setup()
    const onOpenEvent = vi.fn()
    render(<Harness onOpenEvent={onOpenEvent} />)

    const button = screen.getByRole('button', { name: 'Dentist, Wednesday, October 14, 2026, 9:00 AM to 10:00 AM' })
    expect(button).toHaveTextContent('9:00 AM')
    expect(button).toHaveTextContent('Dentist')
    await user.click(button)

    expect(onOpenEvent).toHaveBeenCalledWith('e1', '2026-10-14')
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
