import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiContext } from '../api/ApiContext'
import type { CalendarApi } from '../api/client'
import { CHICAGO, octoberMonth, stubApi, timedSummary } from '../test/fixtures'
import { CalendarScreen } from './CalendarScreen'
import { LiveRegionProvider } from './LiveRegion'

const dentist = timedSummary('e1', 'Dentist', '2026-10-14T09:00:00-05:00', '2026-10-14T10:00:00-05:00')

function renderScreen(api: CalendarApi) {
  return render(
    <ApiContext.Provider value={api}>
      <LiveRegionProvider>
        <CalendarScreen timeZone={CHICAGO} />
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
    await waitFor(() => expect(screen.getByRole('gridcell', { name: 'Wednesday, October 14, 2026' })).toHaveFocus())
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
