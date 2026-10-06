import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiContext } from '../api/ApiContext'
import type { CalendarApi } from '../api/client'
import { CHICAGO, octoberMonth, recurringDetails, recurringSummary, stubApi, timedSummary } from '../test/fixtures'
import { CalendarScreen } from './CalendarScreen'
import { LiveRegionProvider } from './LiveRegion'

const dentist = timedSummary('e1', 'Dentist', '2026-10-14T09:00:00-05:00', '2026-10-14T10:00:00-05:00')

function renderScreen(api: CalendarApi) {
  return render(
    <ApiContext.Provider value={api}>
      <LiveRegionProvider>
        <CalendarScreen timeZone={CHICAGO} today="2026-10-14" />
      </LiveRegionProvider>
    </ApiContext.Provider>,
  )
}

async function openDeleteConfirmation(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole('button', { name: /^Dentist,/ }))
  await user.click(await screen.findByRole('button', { name: 'Delete' }))
  return screen.getByRole('dialog', { name: 'Delete "Dentist"?' })
}

describe('Deleting an event (US3)', () => {
  it('asks for confirmation with focus on Cancel, and Cancel keeps the event', async () => {
    const user = userEvent.setup()
    const api = stubApi({ getMonth: vi.fn(async () => ({ kind: 'ok' as const, value: octoberMonth({ '2026-10-14': [dentist] }) })) })
    renderScreen(api)

    const confirm = await openDeleteConfirmation(user)
    expect(confirm).toHaveAccessibleDescription("This can't be undone.")
    expect(within(confirm).getByRole('button', { name: 'Cancel' })).toHaveFocus()

    await user.click(within(confirm).getByRole('button', { name: 'Cancel' }))
    expect(api.deleteEvent).not.toHaveBeenCalled()
    expect(screen.queryByRole('dialog', { name: 'Delete "Dentist"?' })).not.toBeInTheDocument()
    expect(screen.getByRole('dialog', { name: 'Dentist' })).toBeInTheDocument()
  })

  it('deletes with the version, announces it, closes the dialogs and refreshes the month', async () => {
    const user = userEvent.setup()
    const getMonth = vi
      .fn()
      .mockResolvedValueOnce({ kind: 'ok', value: octoberMonth({ '2026-10-14': [dentist] }) })
      .mockResolvedValue({ kind: 'ok', value: octoberMonth() })
    const api = stubApi({ getMonth })
    renderScreen(api)

    const confirm = await openDeleteConfirmation(user)
    await user.click(within(confirm).getByRole('button', { name: 'Delete' }))

    expect(api.deleteEvent).toHaveBeenCalledWith('e1', 1)
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Deleted Dentist.'))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    await waitFor(() => expect(screen.queryByRole('button', { name: /^Dentist,/ })).not.toBeInTheDocument())
    expect(getMonth).toHaveBeenCalledTimes(2)
    await waitFor(() => expect(screen.getByRole('gridcell', { name: 'Wednesday, October 14, 2026, open in day view' })).toHaveFocus())
  })

  it('shows the conflict message when the event changed elsewhere', async () => {
    const user = userEvent.setup()
    const api = stubApi({
      getMonth: vi.fn(async () => ({ kind: 'ok' as const, value: octoberMonth({ '2026-10-14': [dentist] }) })),
      deleteEvent: vi.fn(async () => ({ kind: 'conflict' as const })),
    })
    renderScreen(api)

    const confirm = await openDeleteConfirmation(user)
    await user.click(within(confirm).getByRole('button', { name: 'Delete' }))

    expect(await screen.findByText(/This event was changed in another window\./, { selector: 'p' })).toBeInTheDocument()
    expect(screen.getByRole('dialog', { name: 'Dentist' })).toBeInTheDocument()
  })
})

describe('Deleting an occurrence of a series (003 US4)', () => {
  const gym = recurringSummary({ start: '2026-10-14T07:00:00-05:00', end: '2026-10-14T08:00:00-05:00', occurrenceDate: '2026-10-14' })

  function seriesApi() {
    return stubApi({
      getMonth: vi.fn(async () => ({ kind: 'ok' as const, value: octoberMonth({ '2026-10-14': [gym] }) })),
      getEvent: vi.fn(async () => ({
        kind: 'ok' as const,
        value: recurringDetails({ occurrenceDate: '2026-10-14', start: gym.start!, end: gym.end! }),
      })),
    })
  }

  async function openScopeChoice(user: ReturnType<typeof userEvent.setup>) {
    await user.click(await screen.findByRole('button', { name: /^Gym,/ }))
    await user.click(await screen.findByRole('button', { name: 'Delete' }))
    return screen.getByRole('dialog', { name: 'Delete recurring event' })
  }

  it.each([
    ['This event', 'this', 'Deleted this event.'],
    ['This and following events', 'following', 'Deleted this and following events.'],
    ['All events', 'all', 'Deleted all events.'],
  ] as const)('%s deletes with that scope and announces it', async (choice, scope, announcement) => {
    const user = userEvent.setup()
    const api = seriesApi()
    renderScreen(api)

    const dialog = await openScopeChoice(user)
    expect(screen.queryByRole('dialog', { name: 'Delete "Gym"?' })).not.toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: choice }))

    expect(api.deleteEvent).toHaveBeenCalledWith('s1', 4, { occurrence: '2026-10-14', scope })
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(announcement))
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('deletes nothing on Cancel', async () => {
    const user = userEvent.setup()
    const api = seriesApi()
    renderScreen(api)

    const dialog = await openScopeChoice(user)
    await user.click(within(dialog).getByRole('button', { name: 'Cancel' }))

    expect(api.deleteEvent).not.toHaveBeenCalled()
    expect(screen.getByRole('dialog', { name: 'Gym' })).toBeInTheDocument()
  })
})
