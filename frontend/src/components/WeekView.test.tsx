import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import type { DaysView, EventSummary } from '../api/types'
import { allDaySummary, daysView, octoberWeek } from '../test/fixtures'
import { WeekView } from './WeekView'

interface HarnessProps {
  week?: DaysView
  onMoveDate?: (date: string, leavesPeriod: boolean) => void
  onOpenDay?: (date: string) => void
  onOpenEvent?: (event: EventSummary, date: string) => void
}

function Harness({ week = octoberWeek(), ...props }: HarnessProps) {
  const [selected, setSelected] = useState('2026-10-14')
  return (
    <>
      <h2 id="period-title">October 11 – 17, 2026</h2>
      <WeekView
        week={week}
        titleId="period-title"
        selectedDate={selected}
        onMoveDate={(date, leavesPeriod) => {
          props.onMoveDate?.(date, leavesPeriod)
          if (!leavesPeriod) setSelected(date)
        }}
        onOpenDay={props.onOpenDay ?? vi.fn()}
        onOpenEvent={props.onOpenEvent ?? vi.fn()}
      />
    </>
  )
}

function heading(name: string) {
  return screen.getByRole('gridcell', { name: `${name}, open in day view` })
}

describe('WeekView (wide)', () => {
  it('shows seven day headings from Sunday to Saturday with one in the Tab order', () => {
    render(<Harness />)

    const cells = screen.getAllByRole('gridcell')
    expect(cells.map((c) => c.textContent)).toEqual(['Sun11', 'Mon12', 'Tue13', 'Wed14', 'Thu15', 'Fri16', 'Sat17'])
    expect(cells.filter((c) => c.tabIndex === 0)).toEqual([heading('Wednesday, October 14, 2026')])
  })

  it('moves between headings with the arrow keys and leaves the week past Saturday', async () => {
    const user = userEvent.setup()
    const onMoveDate = vi.fn()
    render(<Harness onMoveDate={onMoveDate} />)
    heading('Wednesday, October 14, 2026').focus()

    await user.keyboard('{ArrowRight}{ArrowRight}{ArrowRight}')
    expect(heading('Saturday, October 17, 2026')).toHaveFocus()
    await user.keyboard('{ArrowRight}')
    expect(onMoveDate).toHaveBeenLastCalledWith('2026-10-18', true)
  })

  it('opens a day from its heading by tap or Enter', async () => {
    const user = userEvent.setup()
    const onOpenDay = vi.fn()
    render(<Harness onOpenDay={onOpenDay} />)

    await user.click(heading('Thursday, October 15, 2026'))
    expect(onOpenDay).toHaveBeenLastCalledWith('2026-10-15')

    heading('Wednesday, October 14, 2026').focus()
    await user.keyboard('{Enter}')
    expect(onOpenDay).toHaveBeenLastCalledWith('2026-10-14')
  })

  it('draws a multi-day all-day event as one bar, squared off where it continues', () => {
    render(<Harness />)

    const bar = screen.getByRole('button', { name: /^Trip,/ })
    expect(bar.style.gridColumn).toBe('6 / span 2')
    expect(bar).toHaveAttribute('data-continues-after', 'true')
    expect(bar).toHaveTextContent('continues into the next week')
    expect(screen.getAllByRole('button', { name: /^Trip,/ })).toHaveLength(1)
  })

  it('opens events from the time grid', async () => {
    const user = userEvent.setup()
    const onOpenEvent = vi.fn()
    render(<Harness onOpenEvent={onOpenEvent} />)

    await user.click(screen.getByRole('button', { name: /^Dentist,/ }))

    expect(onOpenEvent).toHaveBeenCalledWith(expect.objectContaining({ id: 'e1' }), '2026-10-14')
  })

  it('has no axe violations', async () => {
    const { container } = render(<Harness />)

    expect((await axe(container)).violations).toEqual([])
  })

  it('shows the repeat icon on an all-day bar of a series', () => {
    const event = { ...allDaySummary('s2', 'Class', '2026-10-13', '2026-10-13'), isRecurring: true, occurrenceDate: '2026-10-13' }
    const week = daysView('2026-10-11', 7, {
      allDay: { '2026-10-13': [event] },
      bars: [{ event, startIndex: 2, span: 1, lane: 0, continuesBefore: false, continuesAfter: false }],
    })
    render(<Harness week={week} />)

    const bar = screen.getByRole('button', { name: /^Class,.*repeats/ })
    expect(bar.querySelector('svg.repeat-icon')).not.toBeNull()
  })
})
