import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import App from './App'
import { ApiContext } from './api/ApiContext'
import { allDaySummary, daysView, octoberMonth, octoberWeekContent, stubApi, timedSummary } from './test/fixtures'
import { setNarrowViewport } from './test/viewport'

const month = octoberMonth({
  '2026-10-14': [
    allDaySummary('v1', 'Vacation', '2026-10-12', '2026-10-16'),
    timedSummary('e1', 'Dentist', '2026-10-14T09:00:00-05:00', '2026-10-14T10:00:00-05:00'),
  ],
})

function renderApp() {
  const api = stubApi({ getMonth: vi.fn(async () => ({ kind: 'ok' as const, value: month })) })
  return render(
    <ApiContext.Provider value={api}>
      <App />
    </ApiContext.Provider>,
  )
}

describe('App (whole page)', () => {
  // Only Date is faked, so user-event timers keep working. Oct 1 is "today" in every US zone.
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-10-01T17:00:00Z'))
  })
  afterEach(() => {
    vi.useRealTimers()
  })

  it('has no axe violations with the month view, the details dialog and the form dialog open in turn', async () => {
    const user = userEvent.setup()
    const { container } = renderApp()
    await screen.findByRole('grid', { name: 'October 2026' })
    expect((await axe(container)).violations).toEqual([])

    await user.click(screen.getByRole('button', { name: /^Dentist,/ }))
    await screen.findByRole('heading', { name: 'Dentist' })
    expect((await axe(container)).violations).toEqual([])

    await user.click(screen.getByRole('button', { name: 'Edit' }))
    await screen.findByRole('dialog', { name: 'Edit event' })
    expect((await axe(container)).violations).toEqual([])
  })

  it('tabs from the header controls into the grid and then out of it', async () => {
    const user = userEvent.setup()
    renderApp()
    await screen.findByRole('grid', { name: 'October 2026' })

    await user.tab()
    expect(screen.getByRole('button', { name: 'Previous month' })).toHaveFocus()
    for (const name of ['Today', 'Next month', 'Go to date', 'Day', 'Week', 'Month', 'New event']) {
      await user.tab()
      expect(screen.getByRole('button', { name })).toHaveFocus()
    }
    await user.tab()
    expect(screen.getByRole('gridcell', { name: 'Thursday, October 1, 2026, open in day view' })).toHaveFocus()
    await user.tab()
    expect(document.body).toHaveFocus()
  })
})

describe('App (whole page): every view', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date('2026-10-14T15:00:00Z'))
  })
  afterEach(() => {
    vi.useRealTimers()
  })

  function renderWithDays() {
    const api = stubApi({
      getMonth: vi.fn(async () => ({ kind: 'ok' as const, value: month })),
      getDays: vi.fn(async (_tz: string, start: string, count: 1 | 7) => ({
        kind: 'ok' as const,
        value: daysView(start, count, octoberWeekContent()),
      })),
    })
    return render(
      <ApiContext.Provider value={api}>
        <App />
      </ApiContext.Provider>,
    )
  }

  it('has no axe violations in the day, wide week, narrow week and month views, or with Go to date open', async () => {
    const user = userEvent.setup()
    const { container } = renderWithDays()
    await screen.findByRole('grid')

    await user.click(screen.getByRole('button', { name: 'Day' }))
    await screen.findByRole('button', { name: /^Dentist,/ })
    expect((await axe(container)).violations).toEqual([])

    await user.click(screen.getByRole('button', { name: 'Week' }))
    await screen.findByRole('gridcell', { name: /^Wednesday, October 14, 2026/ })
    expect((await axe(container)).violations).toEqual([])

    act(() => setNarrowViewport(true))
    expect((await axe(container)).violations).toEqual([])
    act(() => setNarrowViewport(false))

    await user.click(screen.getByRole('button', { name: 'Go to date' }))
    expect((await axe(container)).violations).toEqual([])
  })

  it('adds no keyboard shortcuts beyond the Constitution IV baseline', async () => {
    const user = userEvent.setup()
    renderWithDays()
    await screen.findByRole('grid')
    const path = window.location.pathname

    document.body.focus()
    await user.keyboard('dwmtn')

    expect(window.location.pathname).toBe(path)
    expect(screen.getByRole('button', { name: 'Month' })).toHaveAttribute('aria-pressed', 'true')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })
})
