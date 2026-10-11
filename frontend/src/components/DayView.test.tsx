import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import { allDaySummary, daysView, october14, octoberWeekContent } from '../test/fixtures'
import { DayView } from './DayView'

function renderDay(overrides: Partial<Parameters<typeof DayView>[0]> = {}) {
  const props = { day: october14(), titleId: 'title', onOpenEvent: vi.fn(), onStep: vi.fn(), ...overrides }
  return {
    props,
    ...render(
      <>
        <h2 id="title">Wednesday, October 14, 2026</h2>
        <DayView {...props} />
      </>,
    ),
  }
}

describe('DayView', () => {
  it('shows all-day events above the time grid, then timed events in time order', async () => {
    const user = userEvent.setup()
    const content = octoberWeekContent()
    const day = daysView('2026-10-14', 1, {
      ...content,
      allDay: { '2026-10-14': [allDaySummary('a2', 'Holiday', '2026-10-14', '2026-10-14')] },
    })
    renderDay({ day })

    await user.tab()
    expect(screen.getByRole('button', { name: /^Holiday,/ })).toHaveFocus()
    await user.tab()
    expect(screen.getByRole('button', { name: /^Dentist,/ })).toHaveFocus()
    await user.tab()
    expect(screen.getByRole('button', { name: /^Lunch,/ })).toHaveFocus()
  })

  it('opens an event from its block', async () => {
    const user = userEvent.setup()
    const { props } = renderDay()

    await user.click(screen.getByRole('button', { name: /^Dentist,/ }))

    expect(props.onOpenEvent).toHaveBeenCalledWith(expect.objectContaining({ id: 'e1' }), '2026-10-14')
  })

  it('moves to the previous or next day with the arrow keys (FR-024)', async () => {
    const user = userEvent.setup()
    const { props } = renderDay()
    screen.getByRole('button', { name: /^Dentist,/ }).focus()

    await user.keyboard('{ArrowRight}')
    expect(props.onStep).toHaveBeenLastCalledWith(1)
    await user.keyboard('{ArrowLeft}')
    expect(props.onStep).toHaveBeenLastCalledWith(-1)
  })

  it('has no axe violations', async () => {
    const { container } = renderDay()

    expect((await axe(container)).violations).toEqual([])
  })
})
