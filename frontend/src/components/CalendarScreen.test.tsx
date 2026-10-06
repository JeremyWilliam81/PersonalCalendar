import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiContext } from '../api/ApiContext'
import type { CalendarApi } from '../api/client'
import { CHICAGO, daysView, octoberMonth, octoberWeekContent, stubApi } from '../test/fixtures'
import { setNarrowViewport } from '../test/viewport'
import { CalendarScreen } from './CalendarScreen'
import { LiveRegionProvider } from './LiveRegion'

function apiWithSeed(overrides: Partial<CalendarApi> = {}) {
  return stubApi({
    getDays: vi.fn(async (_tz: string, start: string, count: 1 | 7) => ({
      kind: 'ok' as const,
      value: daysView(start, count, octoberWeekContent()),
    })),
    ...overrides,
  })
}

function renderScreen(api: CalendarApi = apiWithSeed(), today = '2026-10-14') {
  return render(
    <ApiContext.Provider value={api}>
      <LiveRegionProvider>
        <CalendarScreen timeZone={CHICAGO} today={today} />
      </LiveRegionProvider>
    </ApiContext.Provider>,
  )
}

function viewButton(name: 'Day' | 'Week' | 'Month') {
  return screen.getByRole('button', { name })
}

describe('CalendarScreen: switching views (US1)', () => {
  it('keeps the selected date when switching between month, day and week (FR-002)', async () => {
    const user = userEvent.setup()
    const api = apiWithSeed()
    renderScreen(api)
    await screen.findByRole('grid', { name: 'October 2026' })
    expect(viewButton('Month')).toHaveAttribute('aria-pressed', 'true')

    await user.click(viewButton('Day'))
    expect(api.getDays).toHaveBeenLastCalledWith(CHICAGO, '2026-10-14', 1)
    expect(await screen.findByRole('heading', { name: /^Wednesday, October 14, 2026/ })).toBeInTheDocument()
    expect(viewButton('Day')).toHaveAttribute('aria-pressed', 'true')

    await user.click(viewButton('Week'))
    expect(api.getDays).toHaveBeenLastCalledWith(CHICAGO, '2026-10-11', 7)
    expect(await screen.findByRole('heading', { name: 'October 11 – 17, 2026' })).toBeInTheDocument()

    await user.click(viewButton('Month'))
    expect(api.getMonth).toHaveBeenLastCalledWith(CHICAGO, 2026, 10)
    expect(await screen.findByRole('grid', { name: 'October 2026' })).toBeInTheDocument()
  })

  it('announces the new period with the view name', async () => {
    const user = userEvent.setup()
    renderScreen()
    await screen.findByRole('grid', { name: 'October 2026' })

    await user.click(viewButton('Week'))

    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Week of October 11 – 17, 2026'))
  })

  it('switches the week between the time grid and the stacked list without reloading or changing the date', async () => {
    const user = userEvent.setup()
    const api = apiWithSeed()
    renderScreen(api)
    await screen.findByRole('grid', { name: 'October 2026' })
    await user.click(viewButton('Week'))
    await screen.findByRole('button', { name: /^Dentist,/ })
    expect(document.querySelector('.time-grid')).not.toBeNull()
    const calls = vi.mocked(api.getDays).mock.calls.length

    act(() => setNarrowViewport(true))
    expect(screen.getAllByRole('region')).toHaveLength(7)
    expect(document.querySelector('.time-grid')).toBeNull()

    act(() => setNarrowViewport(false))
    expect(document.querySelector('.time-grid')).not.toBeNull()
    expect(api.getDays).toHaveBeenCalledTimes(calls)
    expect(screen.getByRole('heading', { name: 'October 11 – 17, 2026' })).toBeInTheDocument()
  })

  it('reloads the current view after saving an event and stays on it (FR-006)', async () => {
    const user = userEvent.setup()
    const api = apiWithSeed()
    renderScreen(api)
    await screen.findByRole('grid', { name: 'October 2026' })
    await user.click(viewButton('Day'))
    await screen.findByRole('button', { name: /^Dentist,/ })
    const calls = vi.mocked(api.getDays).mock.calls.length

    await user.click(screen.getByRole('button', { name: 'New event' }))
    await user.type(screen.getByLabelText(/Title/), 'Call')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.getDays).toHaveBeenCalledTimes(calls + 1))
    expect(viewButton('Day')).toHaveAttribute('aria-pressed', 'true')
    expect(screen.getByRole('heading', { name: /^Wednesday, October 14, 2026/ })).toBeInTheDocument()
  })

  it('uses the month data for the month view only', async () => {
    const api = apiWithSeed({ getMonth: vi.fn(async () => ({ kind: 'ok' as const, value: octoberMonth() })) })
    renderScreen(api)

    await screen.findByRole('grid', { name: 'October 2026' })
    expect(api.getDays).not.toHaveBeenCalled()
  })
})

async function openView(user: ReturnType<typeof userEvent.setup>, view: 'Day' | 'Week' | 'Month') {
  await user.click(viewButton(view))
}

function heading(name: string | RegExp) {
  return screen.getByRole('heading', { level: 2, name })
}

describe('CalendarScreen: moving through time (US2)', () => {
  it('steps one day, one week, or one clamped month (FR-008, FR-009)', async () => {
    const user = userEvent.setup()
    const api = apiWithSeed()
    renderScreen(api, '2026-12-30')
    await screen.findByRole('grid')

    await openView(user, 'Day')
    await user.click(screen.getByRole('button', { name: 'Next day' }))
    expect(api.getDays).toHaveBeenLastCalledWith(CHICAGO, '2026-12-31', 1)

    await openView(user, 'Week')
    expect(heading('December 27, 2026 – January 2, 2027')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Next week' }))
    expect(heading('January 3 – 9, 2027')).toBeInTheDocument()
    expect(api.getDays).toHaveBeenLastCalledWith(CHICAGO, '2027-01-03', 7)
  })

  it('clamps the selected date when moving by month', async () => {
    const user = userEvent.setup()
    const api = apiWithSeed()
    renderScreen(api, '2026-01-31')
    await screen.findByRole('grid')

    await user.click(screen.getByRole('button', { name: 'Next month' }))
    expect(api.getMonth).toHaveBeenLastCalledWith(CHICAGO, 2026, 2)
    await openView(user, 'Day')
    expect(heading(/^Saturday, February 28, 2026/)).toBeInTheDocument()
  })

  it('returns to today in the same view (FR-010)', async () => {
    const user = userEvent.setup()
    renderScreen(apiWithSeed(), '2026-10-14')
    await screen.findByRole('grid')
    await openView(user, 'Week')
    await user.click(screen.getByRole('button', { name: 'Next week' }))
    await user.click(screen.getByRole('button', { name: 'Next week' }))

    await user.click(screen.getByRole('button', { name: 'Today' }))

    expect(heading('October 11 – 17, 2026')).toBeInTheDocument()
    expect(viewButton('Week')).toHaveAttribute('aria-pressed', 'true')
  })

  it('disables Previous and Next at the ends of the supported range (FR-012)', async () => {
    const user = userEvent.setup()
    renderScreen(apiWithSeed(), '1900-01-01')
    await screen.findByRole('grid')
    await openView(user, 'Day')
    expect(screen.getByRole('button', { name: 'Previous day' })).toBeDisabled()
  })

  it('goes to a chosen date in the same view (FR-011)', async () => {
    const user = userEvent.setup()
    renderScreen()
    await screen.findByRole('grid')
    await openView(user, 'Week')

    await user.click(screen.getByRole('button', { name: 'Go to date' }))
    fireEvent.change(screen.getByLabelText('Date'), { target: { value: '2027-03-03' } })
    await user.click(screen.getByRole('button', { name: 'Go' }))

    expect(heading('February 28 – March 6, 2027')).toBeInTheDocument()
    expect(viewButton('Week')).toHaveAttribute('aria-pressed', 'true')
    await waitFor(() =>
      expect(screen.getByRole('gridcell', { name: 'Wednesday, March 3, 2027, open in day view' })).toHaveFocus(),
    )
  })

  it('treats a touch swipe on the view like Next, with native vertical scrolling (FR-008a)', async () => {
    const user = userEvent.setup()
    renderScreen()
    await screen.findByRole('grid')
    await openView(user, 'Day')
    const body = document.querySelector<HTMLElement>('.view-body')!
    expect(body.style.touchAction).toBe('pan-y')

    fireEvent.pointerDown(body, { pointerId: 1, pointerType: 'touch', clientX: 300, clientY: 300 })
    fireEvent.pointerUp(body, { pointerId: 1, pointerType: 'touch', clientX: 200, clientY: 305 })

    expect(heading(/^Thursday, October 15, 2026/)).toBeInTheDocument()
  })

  it('keeps focus on the button used to navigate (FR-025)', async () => {
    const user = userEvent.setup()
    renderScreen()
    await screen.findByRole('grid')

    await user.click(screen.getByRole('button', { name: 'Next month' }))

    expect(screen.getByRole('button', { name: 'Next month' })).toHaveFocus()
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('November 2026'))
  })
})

describe('CalendarScreen: opening days and creating events (US3)', () => {
  it('opens a tapped month day in the day view with focus on its heading', async () => {
    const user = userEvent.setup()
    renderScreen()
    await screen.findByRole('grid')

    await user.click(screen.getByRole('gridcell', { name: 'Thursday, October 15, 2026, open in day view' }))

    expect(viewButton('Day')).toHaveAttribute('aria-pressed', 'true')
    await waitFor(() => expect(heading(/^Thursday, October 15, 2026/)).toHaveFocus())
  })

  it('opens a day from the phone week list heading', async () => {
    const user = userEvent.setup()
    setNarrowViewport(true)
    renderScreen()
    await screen.findByRole('grid')
    await openView(user, 'Week')

    await user.click(await screen.findByRole('button', { name: 'Friday, October 16, 2026, open in day view' }))

    expect(heading(/^Friday, October 16, 2026/)).toBeInTheDocument()
  })

  it('starts a new event on the selected date from the New event button in every view', async () => {
    const user = userEvent.setup()
    renderScreen()
    await screen.findByRole('grid')
    await openView(user, 'Week')

    await user.click(screen.getByRole('button', { name: 'New event' }))

    expect((screen.getByLabelText('Start') as HTMLInputElement).value).toMatch(/^2026-10-14T/)
  })

  it('starts a one-hour event at a tapped empty slot in the day view', async () => {
    const user = userEvent.setup()
    renderScreen()
    await screen.findByRole('grid')
    await openView(user, 'Day')
    await screen.findByRole('button', { name: /^Dentist,/ })

    fireEvent.click(document.querySelector('.time-slots')!, { clientY: 28 * 24 + 2 })

    expect(screen.getByLabelText('Start')).toHaveValue('2026-10-14T14:00')
    expect(screen.getByLabelText('End')).toHaveValue('2026-10-14T15:00')
  })
})

describe('CalendarScreen: today (US4)', () => {
  const todayContent = { ...octoberWeekContent(), today: '2026-10-14' }

  function apiWithToday() {
    return apiWithSeed({
      getMonth: vi.fn(async () => ({ kind: 'ok' as const, value: octoberMonth({}, '2026-10-14') })),
      getDays: vi.fn(async (_tz: string, start: string, count: 1 | 7) => ({
        kind: 'ok' as const,
        value: daysView(start, count, todayContent),
      })),
    })
  }

  it('highlights only today in the month view and reads it as today (FR-014, FR-026)', async () => {
    renderScreen(apiWithToday())
    await screen.findByRole('grid')

    const today = screen.getByRole('gridcell', { name: 'Wednesday, October 14, 2026, today, open in day view' })
    expect(today).toHaveAttribute('aria-current', 'date')
    expect(document.querySelectorAll('[aria-current="date"]')).toHaveLength(1)
  })

  it('highlights today in the wide and narrow week views', async () => {
    const user = userEvent.setup()
    renderScreen(apiWithToday())
    await screen.findByRole('grid')
    await openView(user, 'Week')

    expect(await screen.findByRole('gridcell', { name: 'Wednesday, October 14, 2026, today, open in day view' })).toHaveAttribute(
      'aria-current',
      'date',
    )
    act(() => setNarrowViewport(true))
    expect(screen.getByRole('button', { name: 'Wednesday, October 14, 2026, today, open in day view' })).toHaveAttribute(
      'aria-current',
      'date',
    )
  })

  it('shows the current-time line and says "today" in the day view title', async () => {
    const user = userEvent.setup()
    renderScreen(apiWithToday())
    await screen.findByRole('grid')
    await openView(user, 'Day')

    expect(await screen.findByRole('heading', { name: 'Wednesday, October 14, 2026, today' })).toBeInTheDocument()
    expect(document.querySelector('.now-line')).not.toBeNull()
  })

  it('shows no highlight in a period without today', async () => {
    const user = userEvent.setup()
    renderScreen(apiWithSeed({ getMonth: vi.fn(async () => ({ kind: 'ok' as const, value: octoberMonth({}, '2026-12-01') })) }))
    await screen.findByRole('grid')
    await user.click(screen.getByRole('button', { name: 'Next month' }))

    await waitFor(() => expect(document.querySelectorAll('[aria-current="date"]')).toHaveLength(0))
  })

  it('reloads the period when the date rolls over, without changing the view or selected date (FR-015)', async () => {
    const user = userEvent.setup()
    const api = apiWithToday()
    const view = renderScreen(api)
    await screen.findByRole('grid')
    await openView(user, 'Week')
    await screen.findByRole('button', { name: /^Dentist,/ })
    const calls = vi.mocked(api.getDays).mock.calls.length

    view.rerender(
      <ApiContext.Provider value={api}>
        <LiveRegionProvider>
          <CalendarScreen timeZone={CHICAGO} today="2026-10-15" />
        </LiveRegionProvider>
      </ApiContext.Provider>,
    )

    await waitFor(() => expect(api.getDays).toHaveBeenCalledTimes(calls + 1))
    expect(api.getDays).toHaveBeenLastCalledWith(CHICAGO, '2026-10-11', 7)
    expect(heading('October 11 – 17, 2026')).toBeInTheDocument()
  })
})

describe('CalendarScreen: the address (US5)', () => {
  it('opens the view and date named in the address (FR-018)', async () => {
    window.history.replaceState(null, '', '/week/2026-10-14')
    const api = apiWithSeed()
    renderScreen(api)

    expect(await screen.findByRole('heading', { name: 'October 11 – 17, 2026' })).toBeInTheDocument()
    expect(api.getMonth).not.toHaveBeenCalled()
  })

  it('updates the address on navigation and follows Back (FR-017, FR-021)', async () => {
    const user = userEvent.setup()
    window.history.replaceState(null, '', '/week/2026-10-14')
    renderScreen()
    await screen.findByRole('heading', { name: 'October 11 – 17, 2026' })

    await user.click(screen.getByRole('button', { name: 'Next week' }))
    expect(window.location.pathname).toBe('/week/2026-10-21')

    act(() => {
      window.history.replaceState(null, '', '/week/2026-10-14')
      window.dispatchEvent(new PopStateEvent('popstate'))
    })
    expect(heading('October 11 – 17, 2026')).toBeInTheDocument()
    await waitFor(() => expect(heading('October 11 – 17, 2026')).toHaveFocus())
  })

  it('replaces the entry for arrow-key moves inside the week and pushes past its edge', async () => {
    const user = userEvent.setup()
    window.history.replaceState(null, '', '/week/2026-10-14')
    renderScreen()
    const wednesday = await screen.findByRole('gridcell', { name: 'Wednesday, October 14, 2026, open in day view' })
    const start = window.history.length

    wednesday.focus()
    await user.keyboard('{ArrowRight}')
    expect(window.location.pathname).toBe('/week/2026-10-15')
    expect(window.history.length).toBe(start)

    await user.keyboard('{ArrowRight}{ArrowRight}{ArrowRight}')
    expect(window.location.pathname).toBe('/week/2026-10-18')
    expect(window.history.length).toBe(start + 1)
  })

  it('falls back to this month with a notice for a link it cannot read (FR-020)', async () => {
    const user = userEvent.setup()
    window.history.replaceState(null, '', '/fortnight/x')
    renderScreen()

    expect(await screen.findByText("That link couldn't be opened, so you're seeing this month.")).toBeInTheDocument()
    expect(window.location.pathname).toBe('/month/2026-10-14')
    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent("That link couldn't be opened, so you're seeing this month."),
    )

    await user.click(screen.getByRole('button', { name: 'Dismiss' }))
    expect(screen.queryByText(/That link couldn't be opened/, { selector: '.notice p' })).not.toBeInTheDocument()
  })
})
