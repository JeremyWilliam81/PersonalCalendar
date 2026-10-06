import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import { ApiContext } from '../api/ApiContext'
import type { CalendarApi } from '../api/client'
import type { EventDetails } from '../api/types'
import { CHICAGO, dentistDetails, recurringDetails, stubApi } from '../test/fixtures'
import { EventDetailsDialog } from './EventDetailsDialog'

function renderDetails(
  api: CalendarApi,
  props: { onEdit?: (details: EventDetails) => void; onDelete?: (details: EventDetails) => void } = {},
) {
  return render(
    <ApiContext.Provider value={api}>
      <EventDetailsDialog eventId="e1" timeZone={CHICAGO} onClose={vi.fn()} {...props} />
    </ApiContext.Provider>,
  )
}

/** Opens the dialog from a button, like an event button in the grid. */
function Opener({ api }: { api: CalendarApi }) {
  const [open, setOpen] = useState(false)
  return (
    <ApiContext.Provider value={api}>
      <button type="button" onClick={() => setOpen(true)}>
        Dentist
      </button>
      {open && <EventDetailsDialog eventId="e1" timeZone={CHICAGO} onClose={() => setOpen(false)} />}
    </ApiContext.Provider>
  )
}

describe('EventDetailsDialog', () => {
  it('shows every field of the event', async () => {
    renderDetails(stubApi())

    expect(await screen.findByRole('heading', { name: 'Dentist' })).toBeInTheDocument()
    expect(screen.getByRole('dialog', { name: 'Dentist' })).toBeInTheDocument()
    expect(screen.getByText('Wednesday, October 14, 2026, 9:00 AM to 10:00 AM')).toBeInTheDocument()
    expect(screen.getByText('Main St Clinic')).toBeInTheDocument()
    expect(screen.getByText('Bring insurance card')).toBeInTheDocument()
  })

  it('leaves out empty optional fields', async () => {
    renderDetails(stubApi({ getEvent: vi.fn(async () => ({ kind: 'ok' as const, value: { ...dentistDetails, location: null, notes: null } })) }))

    await screen.findByRole('heading', { name: 'Dentist' })
    expect(screen.queryByText('Location')).not.toBeInTheDocument()
    expect(screen.queryByText('Notes')).not.toBeInTheDocument()
  })

  it('shows Edit and Delete only when handlers are provided', async () => {
    const onEdit = vi.fn()
    const { unmount } = renderDetails(stubApi())
    await screen.findByRole('heading', { name: 'Dentist' })
    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Close' })).toBeInTheDocument()
    unmount()

    renderDetails(stubApi(), { onEdit, onDelete: vi.fn() })
    await userEvent.setup().click(await screen.findByRole('button', { name: 'Edit' }))
    expect(onEdit).toHaveBeenCalledWith(dentistDetails)
    expect(screen.getByRole('button', { name: 'Delete' })).toBeInTheDocument()
  })

  it('shows the date range without times for an all-day event', async () => {
    const vacation: EventDetails = {
      ...dentistDetails,
      title: 'Vacation',
      isAllDay: true,
      start: null,
      end: null,
      startDate: '2026-10-12',
      endDate: '2026-10-16',
    }
    renderDetails(stubApi({ getEvent: vi.fn(async () => ({ kind: 'ok' as const, value: vacation })) }))

    expect(await screen.findByText('All day, Monday, October 12 to Friday, October 16, 2026')).toBeInTheDocument()
  })

  it('says so when the event no longer exists', async () => {
    renderDetails(stubApi({ getEvent: vi.fn(async () => ({ kind: 'notFound' as const })) }))

    expect(await screen.findByText('This event no longer exists.')).toBeInTheDocument()
  })

  it('returns focus to the opening control on Escape and on Close', async () => {
    const user = userEvent.setup()
    render(<Opener api={stubApi()} />)
    const opener = screen.getByRole('button', { name: 'Dentist' })

    await user.click(opener)
    await screen.findByRole('heading', { name: 'Dentist' })
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(opener).toHaveFocus()

    await user.click(opener)
    await user.click(await screen.findByRole('button', { name: 'Close' }))
    expect(opener).toHaveFocus()
  })

  it('has no axe violations', async () => {
    const { container } = renderDetails(stubApi(), { onEdit: vi.fn(), onDelete: vi.fn() })
    await screen.findByRole('heading', { name: 'Dentist' })

    expect((await axe(container)).violations).toEqual([])
  })

  it('shows an occurrence with its own date and the repeat summary (003 US2)', async () => {
    const api = stubApi({ getEvent: vi.fn(async () => ({ kind: 'ok' as const, value: recurringDetails() })) })
    const { container } = render(
      <ApiContext.Provider value={api}>
        <EventDetailsDialog eventId="s1" occurrenceDate="2026-10-21" timeZone={CHICAGO} onClose={vi.fn()} />
      </ApiContext.Provider>,
    )

    expect(await screen.findByText('Wednesday, October 21, 2026, 7:00 AM to 8:00 AM')).toBeInTheDocument()
    expect(api.getEvent).toHaveBeenCalledWith('s1', CHICAGO, '2026-10-21')
    expect(screen.getByText('Weekly on Monday, Wednesday, and Friday')).toBeInTheDocument()
    expect(container.querySelector('svg.repeat-icon')).not.toBeNull()
    expect((await axe(container)).violations).toEqual([])
  })

  it('shows no repeat summary for a one-time event', async () => {
    renderDetails(stubApi())

    await screen.findByRole('heading', { name: 'Dentist' })
    expect(screen.queryByText(/^Repeats$/)).not.toBeInTheDocument()
    expect(document.querySelector('svg.repeat-icon')).toBeNull()
  })
})
