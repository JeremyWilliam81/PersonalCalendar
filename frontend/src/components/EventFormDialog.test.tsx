import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import { ApiContext } from '../api/ApiContext'
import type { CalendarApi } from '../api/client'
import type { EventDetails } from '../api/types'
import { CHICAGO, dentistDetails, recurringDetails, stubApi } from '../test/fixtures'
import { EventFormDialog } from './EventFormDialog'
import { LiveRegionProvider } from './LiveRegion'

const NOW = new Date(2026, 9, 14, 14, 20) // 14:20 local (TZ fixed to America/Chicago)

function renderForm(
  api: CalendarApi,
  props: {
    onSaved?: (details: EventDetails) => void
    onClose?: () => void
    event?: EventDetails
    initialTimes?: { start: string; end: string }
  } = {},
) {
  const onSaved = props.onSaved ?? vi.fn()
  const onClose = props.onClose ?? vi.fn()
  const view = render(
    <ApiContext.Provider value={api}>
      <LiveRegionProvider>
        <EventFormDialog
          initialDate="2026-10-14"
          timeZone={CHICAGO}
          now={NOW}
          event={props.event}
          initialTimes={props.initialTimes}
          onSaved={onSaved}
          onClose={onClose}
        />
      </LiveRegionProvider>
    </ApiContext.Provider>,
  )
  return { ...view, onSaved, onClose }
}

async function setValue(user: ReturnType<typeof userEvent.setup>, label: string, value: string) {
  const input = screen.getByLabelText(label)
  await user.clear(input)
  if (value) await user.type(input, value)
}

describe('EventFormDialog (create)', () => {
  it('labels every field, presets the chosen day and focuses the title', () => {
    renderForm(stubApi())

    expect(screen.getByRole('dialog', { name: 'New event' })).toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toHaveFocus()
    expect(screen.getByLabelText('Start')).toHaveValue('2026-10-14T15:00')
    expect(screen.getByLabelText('End')).toHaveValue('2026-10-14T16:00')
    expect(screen.getByLabelText('Location')).toHaveValue('')
    expect(screen.getByLabelText('Notes')).toHaveValue('')
  })

  it('uses the times of a tapped slot instead of the defaults (FR-013b)', () => {
    renderForm(stubApi(), { initialTimes: { start: '2026-10-14T14:00', end: '2026-10-14T15:00' } })

    expect(screen.getByLabelText('Start')).toHaveValue('2026-10-14T14:00')
    expect(screen.getByLabelText('End')).toHaveValue('2026-10-14T15:00')
  })

  it('sends the input with the device time zone', async () => {
    const user = userEvent.setup()
    const api = stubApi()
    renderForm(api)

    await user.type(screen.getByLabelText('Title'), 'Dentist')
    await user.type(screen.getByLabelText('Location'), 'Main St Clinic')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(api.createEvent).toHaveBeenCalledWith({
      title: 'Dentist',
      location: 'Main St Clinic',
      notes: null,
      isAllDay: false,
      timeZone: CHICAGO,
      start: '2026-10-14T15:00',
      end: '2026-10-14T16:00',
      startDate: null,
      endDate: null,
      acceptAdjustedTimes: false,
      recurrence: null,
    })
  })

  it('shows server validation errors next to their fields and focuses the first one', async () => {
    const user = userEvent.setup()
    const api = stubApi({
      createEvent: vi.fn(async () => ({
        kind: 'validation' as const,
        errors: { title: ['title.required'], end: ['end.notAfterStart'] },
      })),
    })
    renderForm(api)

    await user.click(screen.getByRole('button', { name: 'Save' }))

    const title = screen.getByLabelText('Title')
    const end = screen.getByLabelText('End')
    expect(title).toHaveAttribute('aria-invalid', 'true')
    expect(title).toHaveAccessibleDescription('Title is required.')
    expect(end).toHaveAttribute('aria-invalid', 'true')
    expect(end).toHaveAccessibleDescription('End must be after start.')
    expect(title).toHaveFocus()
    expect(screen.getByRole('status')).toHaveTextContent('2 errors. Fix the highlighted fields.')
  })

  it('asks to confirm a time adjusted for a daylight saving gap, then resends', async () => {
    const user = userEvent.setup()
    const createEvent = vi
      .fn()
      .mockResolvedValueOnce({
        kind: 'dstAdjustment',
        adjustedStart: '2027-03-14T03:30',
        adjustedEnd: '2027-03-14T04:00',
        timeZone: CHICAGO,
      })
      .mockResolvedValueOnce({ kind: 'ok', value: dentistDetails })
    renderForm(stubApi({ createEvent }))

    await user.type(screen.getByLabelText('Title'), 'Early')
    await setValue(user, 'Start', '2027-03-14T02:30')
    await setValue(user, 'End', '2027-03-14T04:00')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(
      await screen.findByText(
        "2:30 AM doesn't exist on Sunday, March 14, 2027 because of the daylight saving time change. The event will start at 3:30 AM.",
      ),
    ).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Save with adjusted time' }))

    expect(createEvent).toHaveBeenLastCalledWith(expect.objectContaining({ acceptAdjustedTimes: true }))
  })

  it('keeps everything the user typed when saving fails', async () => {
    const user = userEvent.setup()
    renderForm(stubApi({ createEvent: vi.fn(async () => ({ kind: 'saveFailed' as const })) }))

    await user.type(screen.getByLabelText('Title'), 'Dentist')
    await user.type(screen.getByLabelText('Notes'), 'Bring card')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(
      await screen.findByText("Couldn't save the event. Your changes are still here. Try again."),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toHaveValue('Dentist')
    expect(screen.getByLabelText('Notes')).toHaveValue('Bring card')
  })

  it('treats an unreachable API as a failed save', async () => {
    const user = userEvent.setup()
    renderForm(stubApi({ createEvent: vi.fn(async () => ({ kind: 'network' as const })) }))

    await user.type(screen.getByLabelText('Title'), 'Dentist')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(
      await screen.findByText("Couldn't save the event. Your changes are still here. Try again."),
    ).toBeInTheDocument()
  })

  it('asks before discarding changes and can keep editing', async () => {
    const user = userEvent.setup()
    const { onClose } = renderForm(stubApi())

    await user.type(screen.getByLabelText('Title'), 'Dentist')
    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    const confirm = screen.getByRole('dialog', { name: 'Discard your changes?' })
    await user.click(within(confirm).getByRole('button', { name: 'Keep editing' }))
    expect(onClose).not.toHaveBeenCalled()
    expect(screen.getByLabelText('Title')).toHaveValue('Dentist')

    await user.keyboard('{Escape}')
    await user.click(
      within(screen.getByRole('dialog', { name: 'Discard your changes?' })).getByRole('button', { name: 'Discard' }),
    )
    expect(onClose).toHaveBeenCalled()
  })

  it('closes without asking when nothing changed', async () => {
    const user = userEvent.setup()
    const { onClose } = renderForm(stubApi())

    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(onClose).toHaveBeenCalled()
    expect(screen.queryByRole('dialog', { name: 'Discard your changes?' })).not.toBeInTheDocument()
  })

  it('announces a successful save and reports the saved event', async () => {
    const user = userEvent.setup()
    const { onSaved } = renderForm(stubApi())

    await user.type(screen.getByLabelText('Title'), 'Dentist')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(dentistDetails))
    expect(screen.getByRole('status')).toHaveTextContent(
      'Saved Dentist, Wednesday, October 14, 2026, 9:00 AM to 10:00 AM.',
    )
  })

  it('has no axe violations', async () => {
    const { container } = renderForm(stubApi())

    expect((await axe(container)).violations).toEqual([])
  })
})

describe('EventFormDialog (all day)', () => {
  it('switches to date fields that keep the dates, and sends dates only', async () => {
    const user = userEvent.setup()
    const api = stubApi()
    renderForm(api)

    await user.type(screen.getByLabelText('Title'), 'Vacation')
    await user.click(screen.getByRole('checkbox', { name: 'All day' }))

    expect(screen.queryByLabelText('Start')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Start date')).toHaveAttribute('type', 'date')
    expect(screen.getByLabelText('Start date')).toHaveValue('2026-10-14')
    expect(screen.getByLabelText('End date')).toHaveValue('2026-10-14')

    await setValue(user, 'End date', '2026-10-16')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(api.createEvent).toHaveBeenCalledWith(
      expect.objectContaining({
        isAllDay: true,
        startDate: '2026-10-14',
        endDate: '2026-10-16',
        start: null,
        end: null,
      }),
    )
  })

  it('requires new times when switching back to timed', async () => {
    const user = userEvent.setup()
    const api = stubApi({
      createEvent: vi.fn(async () => ({
        kind: 'validation' as const,
        errors: { start: ['start.required'], end: ['end.required'] },
      })),
    })
    renderForm(api)

    const allDay = screen.getByRole('checkbox', { name: 'All day' })
    await user.click(allDay)
    await user.click(allDay)

    expect(screen.getByLabelText('Start')).toHaveAttribute('type', 'datetime-local')
    expect(screen.getByLabelText('Start')).toHaveValue('')
    expect(screen.getByLabelText('End')).toHaveValue('')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(screen.getByLabelText('Start')).toHaveAccessibleDescription('Start is required.')
  })

  it('shows the end-date error on the End date field', async () => {
    const user = userEvent.setup()
    renderForm(
      stubApi({
        createEvent: vi.fn(async () => ({ kind: 'validation' as const, errors: { endDate: ['endDate.beforeStart'] } })),
      }),
    )

    await user.click(screen.getByRole('checkbox', { name: 'All day' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    const endDate = screen.getByLabelText('End date')
    expect(endDate).toHaveAttribute('aria-invalid', 'true')
    expect(endDate).toHaveAccessibleDescription('End date must be on or after the start date.')
    expect(endDate).toHaveFocus()
  })

  it('pre-fills an all-day event in edit mode', () => {
    renderForm(stubApi(), { event: { ...dentistDetails, isAllDay: true, start: null, end: null, startDate: '2026-10-12', endDate: '2026-10-16' } })

    expect(screen.getByRole('checkbox', { name: 'All day' })).toBeChecked()
    expect(screen.getByLabelText('Start date')).toHaveValue('2026-10-12')
    expect(screen.getByLabelText('End date')).toHaveValue('2026-10-16')
  })
})

describe('EventFormDialog (edit)', () => {
  it('pre-fills every field from the event, in the device zone', () => {
    renderForm(stubApi(), { event: dentistDetails })

    expect(screen.getByRole('dialog', { name: 'Edit event' })).toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toHaveValue('Dentist')
    expect(screen.getByLabelText('Start')).toHaveValue('2026-10-14T09:00')
    expect(screen.getByLabelText('End')).toHaveValue('2026-10-14T10:00')
    expect(screen.getByLabelText('Location')).toHaveValue('Main St Clinic')
    expect(screen.getByLabelText('Notes')).toHaveValue('Bring insurance card')
  })

  it('saves with PUT and the version it was loaded with', async () => {
    const user = userEvent.setup()
    const api = stubApi()
    renderForm(api, { event: dentistDetails })

    await setValue(user, 'Start', '2026-10-15T09:00')
    await setValue(user, 'End', '2026-10-15T10:00')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(api.updateEvent).toHaveBeenCalledWith(
      'e1',
      expect.objectContaining({ title: 'Dentist', start: '2026-10-15T09:00', end: '2026-10-15T10:00', version: 1 }),
    )
    expect(api.createEvent).not.toHaveBeenCalled()
  })

  it('offers to reload when the event was changed elsewhere', async () => {
    const user = userEvent.setup()
    const reloaded: EventDetails = { ...dentistDetails, title: 'Dentist (moved)', version: 2 }
    const updateEvent = vi.fn().mockResolvedValueOnce({ kind: 'conflict' }).mockResolvedValueOnce({ kind: 'ok', value: reloaded })
    const api = stubApi({ updateEvent, getEvent: vi.fn(async () => ({ kind: 'ok' as const, value: reloaded })) })
    renderForm(api, { event: dentistDetails })

    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('This event was changed in another window.', { selector: 'p' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Reload event' }))

    await waitFor(() => expect(screen.getByLabelText('Title')).toHaveValue('Dentist (moved)'))
    expect(screen.queryByText('This event was changed in another window.', { selector: 'p' })).not.toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(updateEvent).toHaveBeenLastCalledWith('e1', expect.objectContaining({ version: 2 }))
  })

  it('asks before discarding edits and sends nothing after Discard', async () => {
    const user = userEvent.setup()
    const api = stubApi()
    const { onClose } = renderForm(api, { event: dentistDetails })

    await user.type(screen.getByLabelText('Title'), ' again')
    await user.click(screen.getByRole('button', { name: 'Cancel' }))
    await user.click(
      within(screen.getByRole('dialog', { name: 'Discard your changes?' })).getByRole('button', { name: 'Discard' }),
    )

    expect(onClose).toHaveBeenCalled()
    expect(api.updateEvent).not.toHaveBeenCalled()
  })
})

describe('EventFormDialog (repeat, US1)', () => {
  it('creates a weekly series on the chosen weekdays', async () => {
    const user = userEvent.setup()
    const api = stubApi()
    renderForm(api)

    await user.type(screen.getByLabelText('Title'), 'Gym')
    await user.click(screen.getByRole('radio', { name: 'Weekly' }))
    await user.click(screen.getByRole('button', { name: 'Monday' }))
    await user.click(screen.getByRole('button', { name: 'Friday' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(api.createEvent).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Gym',
        recurrence: {
          frequency: 'weekly',
          interval: 1,
          weekdays: ['monday', 'wednesday', 'friday'],
          monthly: null,
          end: { type: 'never' },
        },
      }),
    )
  })

  it('sends no recurrence for an event that does not repeat', async () => {
    const user = userEvent.setup()
    const api = stubApi()
    renderForm(api)

    await user.type(screen.getByLabelText('Title'), 'Call')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(api.createEvent).toHaveBeenCalledWith(expect.objectContaining({ recurrence: null }))
  })

  it('turns a one-time event into a daily series without asking for a scope', async () => {
    const user = userEvent.setup()
    const api = stubApi()
    renderForm(api, { event: dentistDetails })

    await user.click(screen.getByRole('radio', { name: 'Daily' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(api.updateEvent).toHaveBeenCalledWith(
      'e1',
      expect.objectContaining({ version: 1, recurrence: expect.objectContaining({ frequency: 'daily' }) }),
    )
    expect(screen.queryByRole('dialog', { name: /recurring event/ })).not.toBeInTheDocument()
  })

  it('shows server rule errors next to the repeat control', async () => {
    const user = userEvent.setup()
    const api = stubApi({
      createEvent: vi.fn(async () => ({
        kind: 'validation' as const,
        errors: { 'recurrence.interval': ['recurrence.interval.outOfRange'] },
      })),
    })
    renderForm(api)
    await user.type(screen.getByLabelText('Title'), 'Gym')
    await user.click(screen.getByRole('radio', { name: 'Daily' }))

    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(await screen.findByRole('spinbutton', { name: 'Repeat every' })).toHaveAccessibleDescription(
      'Enter a number from 1 to 99.',
    )
  })

  it('asks before discarding unsaved repeat changes', async () => {
    const user = userEvent.setup()
    const { onClose } = renderForm(stubApi(), { event: dentistDetails })

    await user.click(screen.getByRole('radio', { name: 'Yearly' }))
    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(screen.getByRole('dialog', { name: 'Discard your changes?' })).toBeInTheDocument()
    expect(onClose).not.toHaveBeenCalled()
  })
})

describe('EventFormDialog (series edits, US3)', () => {
  const scopeDialog = () => screen.queryByRole('dialog', { name: 'Change recurring event' })
  const choiceNames = () =>
    within(scopeDialog()!)
      .getAllByRole('button')
      .map((b) => b.textContent)
      .filter((name) => name !== 'Cancel')

  it('opens straight away with the occurrence’s date and time and the series rule', () => {
    renderForm(stubApi(), { event: recurringDetails() })

    expect(screen.getByLabelText('Start')).toHaveValue('2026-10-21T07:00')
    expect(screen.getByRole('radio', { name: 'Weekly' })).toBeChecked()
    expect(screen.getByRole('button', { name: 'Monday' })).toHaveAttribute('aria-pressed', 'true')
    expect(scopeDialog()).not.toBeInTheDocument()
  })

  it('asks where a time change applies and saves with that scope', async () => {
    const user = userEvent.setup()
    const api = stubApi({ updateEvent: vi.fn(async () => ({ kind: 'ok' as const, value: recurringDetails({ version: 5 }) })) })
    renderForm(api, { event: recurringDetails() })

    await setValue(user, 'Start', '2026-10-21T18:00')
    await setValue(user, 'End', '2026-10-21T19:00')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(choiceNames()).toEqual(['This event', 'This and following events', 'All events'])
    await user.click(screen.getByRole('button', { name: 'This event' }))

    expect(api.updateEvent).toHaveBeenCalledWith(
      's1',
      expect.objectContaining({ start: '2026-10-21T18:00', version: 4, recurrence: expect.objectContaining({ frequency: 'weekly' }) }),
      { occurrence: '2026-10-21', scope: 'this' },
    )
    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent('Changed this event.'))
  })

  it('offers only "This event" when the date changed', async () => {
    const user = userEvent.setup()
    renderForm(stubApi(), { event: recurringDetails() })

    await setValue(user, 'Start', '2026-10-22T07:00')
    await setValue(user, 'End', '2026-10-22T08:00')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(choiceNames()).toEqual(['This event'])
  })

  it('hides "This event" when the repeat options changed', async () => {
    const user = userEvent.setup()
    renderForm(stubApi(), { event: recurringDetails() })

    await user.click(screen.getByRole('button', { name: 'Tuesday' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(choiceNames()).toEqual(['This and following events', 'All events'])
  })

  it('refuses a date change together with a repeat change, without asking (FR-016a)', async () => {
    const user = userEvent.setup()
    const api = stubApi()
    renderForm(api, { event: recurringDetails() })

    await setValue(user, 'Start', '2026-10-22T07:00')
    await setValue(user, 'End', '2026-10-22T08:00')
    await user.click(screen.getByRole('button', { name: 'Tuesday' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(scopeDialog()).not.toBeInTheDocument()
    expect(screen.getByLabelText('Start')).toHaveAccessibleDescription(
      'A new date applies only to this event, but repeat changes apply to the series. Undo one of them.',
    )
    expect(api.updateEvent).not.toHaveBeenCalled()
  })

  it('warns before a rule change discards single-event changes', async () => {
    const user = userEvent.setup()
    const api = stubApi()
    renderForm(api, { event: recurringDetails({ exceptionCount: 2 }) })

    await user.click(screen.getByRole('button', { name: 'Tuesday' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.click(screen.getByRole('button', { name: 'All events' }))

    expect(screen.getByText('Changes you made to single events in this series will be lost.')).toBeInTheDocument()
    expect(api.updateEvent).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Save anyway' }))
    expect(api.updateEvent).toHaveBeenCalledWith('s1', expect.anything(), { occurrence: '2026-10-21', scope: 'all' })
  })

  it('does not warn about a title-only change to all events', async () => {
    const user = userEvent.setup()
    const api = stubApi()
    renderForm(api, { event: recurringDetails({ exceptionCount: 2 }) })

    await setValue(user, 'Title', 'Swim')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.click(screen.getByRole('button', { name: 'All events' }))

    expect(api.updateEvent).toHaveBeenCalledWith('s1', expect.objectContaining({ title: 'Swim' }), { occurrence: '2026-10-21', scope: 'all' })
  })

  it('returns to the form with the edits kept on Cancel', async () => {
    const user = userEvent.setup()
    const api = stubApi()
    renderForm(api, { event: recurringDetails() })

    await setValue(user, 'Title', 'Swim')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.click(within(scopeDialog()!).getByRole('button', { name: 'Cancel' }))

    expect(scopeDialog()).not.toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toHaveValue('Swim')
    expect(api.updateEvent).not.toHaveBeenCalled()
  })

  it('shows a scope error from the server on the form', async () => {
    const user = userEvent.setup()
    const api = stubApi({
      updateEvent: vi.fn(async () => ({ kind: 'validation' as const, errors: { scope: ['scope.dateChangeRequiresThis'] } })),
    })
    renderForm(api, { event: recurringDetails() })

    await setValue(user, 'Title', 'Swim')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await user.click(screen.getByRole('button', { name: 'All events' }))

    expect(await screen.findByText('A new date can only apply to this event.')).toBeInTheDocument()
  })
})

describe('EventFormDialog (stop repeating, US5)', () => {
  it.each([
    ['All events', 'all', 'All events except the first will be removed.'],
    ['This and following events', 'following', 'This and following events will be removed, and this one kept as a single event.'],
  ] as const)('warns before %s stop repeating, then sends no rule', async (choice, scope, warning) => {
    const user = userEvent.setup()
    const api = stubApi()
    renderForm(api, { event: recurringDetails() })

    await user.click(screen.getByRole('radio', { name: 'Does not repeat' }))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    const dialog = screen.getByRole('dialog', { name: 'Change recurring event' })
    expect(within(dialog).queryByRole('button', { name: 'This event' })).not.toBeInTheDocument()
    await user.click(within(dialog).getByRole('button', { name: choice }))
    expect(screen.getByText(warning)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Save anyway' }))
    expect(api.updateEvent).toHaveBeenCalledWith('s1', expect.objectContaining({ recurrence: null }), { occurrence: '2026-10-21', scope })
  })
})

