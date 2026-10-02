import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import { octoberWeek } from '../test/fixtures'
import { WeekList } from './WeekList'

function renderList(overrides: Partial<Parameters<typeof WeekList>[0]> = {}) {
  const props = {
    week: octoberWeek(),
    selectedDate: '2026-10-14',
    onMoveDate: vi.fn(),
    onOpenDay: vi.fn(),
    onOpenEvent: vi.fn(),
    ...overrides,
  }
  return { props, ...render(<WeekList {...props} />) }
}

function section(name: string) {
  return screen.getByRole('region', { name })
}

describe('WeekList (narrow, FR-004a)', () => {
  it('stacks seven day sections from Sunday to Saturday', () => {
    renderList()

    expect(screen.getAllByRole('region').map((r) => r.getAttribute('aria-label'))).toEqual([
      'Sunday, October 11, 2026',
      'Monday, October 12, 2026',
      'Tuesday, October 13, 2026',
      'Wednesday, October 14, 2026',
      'Thursday, October 15, 2026',
      'Friday, October 16, 2026',
      'Saturday, October 17, 2026',
    ])
  })

  it('lists all-day events first, then timed events with their time range, as full-width buttons', async () => {
    const user = userEvent.setup()
    const { props } = renderList()

    const fri = within(section('Friday, October 16, 2026')).getAllByRole('button')
    expect(fri[1]).toHaveTextContent('All day')
    expect(fri[1]).toHaveTextContent('Trip')

    const wed = within(section('Wednesday, October 14, 2026')).getAllByRole('button').slice(1)
    expect(wed.map((b) => b.textContent)).toEqual([
      '9:00 AM – 10:30 AMDentist',
      '9:15 AM – 11:00 AMLunch',
      '9:30 AM – 9:45 AMStandup',
      '10:00 PM – 1:00 AMLate show',
    ])
    await user.click(wed[0])
    expect(props.onOpenEvent).toHaveBeenCalledWith('e1', '2026-10-14')
  })

  it('says "No events" on an empty day', () => {
    renderList()

    expect(within(section('Monday, October 12, 2026')).getByText('No events')).toBeInTheDocument()
  })

  it('opens a day from its heading', async () => {
    const user = userEvent.setup()
    const { props } = renderList()

    await user.click(screen.getByRole('button', { name: 'Thursday, October 15, 2026, open in day view' }))

    expect(props.onOpenDay).toHaveBeenCalledWith('2026-10-15')
  })

  it('moves between day headings with the arrow keys (FR-024)', async () => {
    const user = userEvent.setup()
    const { props } = renderList()
    screen.getByRole('button', { name: 'Saturday, October 17, 2026, open in day view' }).focus()

    await user.keyboard('{ArrowLeft}')
    expect(screen.getByRole('button', { name: 'Friday, October 16, 2026, open in day view' })).toHaveFocus()
    expect(props.onMoveDate).toHaveBeenLastCalledWith('2026-10-16', false)

    screen.getByRole('button', { name: 'Saturday, October 17, 2026, open in day view' }).focus()
    await user.keyboard('{ArrowRight}')
    expect(props.onMoveDate).toHaveBeenLastCalledWith('2026-10-18', true)
  })

  it('has no axe violations', async () => {
    const { container } = renderList()

    expect((await axe(container)).violations).toEqual([])
  })
})
